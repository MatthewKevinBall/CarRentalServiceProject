using Shouldly;

namespace CarRental.Domain.Tests;

public class BookingTests
{
    private static readonly DateOnly _today = new(2026, 10, 5);

    // Made-up prices, as in PriceListTests: easy to tell apart.
    private static readonly PriceList _priceList = new(
        economy: new Money(10m),
        standard: new Money(20m),
        suv: new Money(30m),
        premium: new Money(40m),
        oneWayFee: new Money(7m),
        depositPercentage: 10m);

    private static Car CreateCar(CarType type = CarType.Suv, City? homeCity = null) =>
        new(new Registration("ABC123"), "Toyota", "RAV4", type, homeCity ?? City.Auckland);

    private static Driver CreateDriver(LicenceType licenceType = LicenceType.Full, DateOnly? licenceExpiry = null) =>
        new(
            "Jane Smith",
            new EmailAddress("jane@example.co.nz"),
            new DriverLicence("DI123456", licenceType, licenceExpiry ?? new DateOnly(2030, 1, 1)));

    // An October 2026 trip; returns to the pickup city unless a different drop-off city is given.
    private static Itinerary October(int pickupDay, int returnDay, City? pickupCity = null, City? dropOffCity = null) =>
        new(
            pickupCity ?? City.Auckland,
            dropOffCity ?? pickupCity ?? City.Auckland,
            new DateRange(new DateOnly(2026, 10, pickupDay), new DateOnly(2026, 10, returnDay)));

    // A valid booking request; each test changes only the detail it's about.
    private static Result<Booking, BookingError> Book(
        Car? car = null,
        CarSelection? selection = null,
        Itinerary? itinerary = null,
        Driver? driver = null)
    {
        car ??= CreateCar();

        return Booking.Create(
            new BookingReference("K7QM4X"),
            car,
            selection ?? new CarSelection.SpecificCar(car.Id),
            itinerary ?? October(10, 13),
            driver ?? CreateDriver(),
            _priceList,
            _today);
    }

    // ---- A valid booking ----

    [Fact]
    public void Create_WithValidRequest_Succeeds()
    {
        Book().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_AssignsTheGivenCar()
    {
        var car = CreateCar();

        var booking = Book(car: car).Value;

        booking.CarId.ShouldBe(car.Id);
    }

    [Fact]
    public void Create_GivesEachBookingItsOwnId()
    {
        var first = Book().Value;
        var second = Book().Value;

        first.Id.ShouldNotBe(second.Id);
    }

    // ---- Rental length: 1 to 30 days ----

    [Fact]
    public void Create_WhenRentalIs30Days_Succeeds()
    {
        var itinerary = new Itinerary(
            City.Auckland, City.Auckland, new DateRange(new DateOnly(2026, 10, 10), new DateOnly(2026, 11, 9)));

        Book(itinerary: itinerary).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WhenRentalIsLongerThan30Days_FailsWithRentalLongerThan30Days()
    {
        var itinerary = new Itinerary(
            City.Auckland, City.Auckland, new DateRange(new DateOnly(2026, 10, 10), new DateOnly(2026, 11, 10)));

        Book(itinerary: itinerary).Errors.ShouldBe([BookingError.RentalLongerThan30Days]);
    }

    // ---- Pickup date: not before today ----

    [Fact]
    public void Create_WhenPickupIsToday_Succeeds()
    {
        Book(itinerary: October(5, 8)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WhenPickupIsBeforeToday_FailsWithPickupDateInPast()
    {
        Book(itinerary: October(4, 8)).Errors.ShouldBe([BookingError.PickupDateInPast]);
    }

    // ---- Driver's licence ----

    [Fact]
    public void Create_WhenLicenceIsNotFull_FailsWithLicenceDoesNotPermitRental()
    {
        var driver = CreateDriver(licenceType: LicenceType.Restricted);

        Book(driver: driver).Errors.ShouldBe([BookingError.LicenceDoesNotPermitRental]);
    }

    [Fact]
    public void Create_WhenLicenceExpiresDuringTheRental_FailsWithLicenceDoesNotPermitRental()
    {
        // Valid on the pickup date (10th), expired by the return date (13th).
        var driver = CreateDriver(licenceExpiry: new DateOnly(2026, 10, 12));

        Book(driver: driver, itinerary: October(10, 13)).Errors.ShouldBe([BookingError.LicenceDoesNotPermitRental]);
    }

    // ---- Car location: based in the pickup city ----

    [Fact]
    public void Create_WhenCarIsBasedInAnotherCity_FailsWithCarNotBasedInPickupCity()
    {
        var car = CreateCar(homeCity: City.Wellington);

        Book(car: car, itinerary: October(10, 13, pickupCity: City.Auckland))
            .Errors.ShouldBe([BookingError.CarNotBasedInPickupCity]);
    }

    [Fact]
    public void Create_WhenCarIsBasedInPickupCityButCurrentlyElsewhere_Succeeds()
    {
        // Availability uses the home city: after a one-way rental, the car is relocated home.
        var car = CreateCar(homeCity: City.Auckland);
        car.RecordArrivalIn(City.Wellington);

        Book(car: car, itinerary: October(10, 13, pickupCity: City.Auckland)).IsSuccess.ShouldBeTrue();
    }

    // ---- Selection ----

    [Fact]
    public void Create_WhenCarDoesNotMatchSelection_FailsWithCarDoesNotMatchSelection()
    {
        var suv = CreateCar(CarType.Suv);

        Book(car: suv, selection: new CarSelection.OfType(CarType.Economy))
            .Errors.ShouldBe([BookingError.CarDoesNotMatchSelection]);
    }

    [Fact]
    public void Create_PricesAtTheSelectionsChargedType()
    {
        // An SUV booked as "any available" is charged the Standard rate (20), not the SUV rate (30).
        var suv = CreateCar(CarType.Suv);

        var booking = Book(car: suv, selection: new CarSelection.AnyAvailable()).Value;

        booking.Price.DailyRate.ShouldBe(new Money(20m));
    }

    [Fact]
    public void Create_PricesTheBookingsItinerary()
    {
        // A one-way trip must include the one-way fee in the stored price.
        var booking = Book(itinerary: October(10, 13, pickupCity: City.Auckland, dropOffCity: City.Wellington)).Value;

        booking.Price.OneWayFee.ShouldBe(new Money(7m));
    }

    // ---- Several problems at once ----

    [Fact]
    public void Create_WithSeveralProblems_ReportsAllOfThem()
    {
        var learner = CreateDriver(licenceType: LicenceType.Learner);
        var wellingtonCar = CreateCar(homeCity: City.Wellington);

        var result = Book(car: wellingtonCar, driver: learner, itinerary: October(4, 8, pickupCity: City.Auckland));

        result.Errors.ShouldBe(
            [BookingError.PickupDateInPast, BookingError.LicenceDoesNotPermitRental, BookingError.CarNotBasedInPickupCity],
            ignoreOrder: true);
    }

    // ---- Blocked period: rental + return day + cleaning (1 day) or relocation (7 days) ----

    [Fact]
    public void BlockedPeriod_WhenReturnedToPickupCity_AddsOneCleaningDay()
    {
        // Returned on the 13th, cleaned on the 14th, next pickup the 15th.
        var booking = Book(itinerary: October(10, 13)).Value;

        booking.BlockedPeriod.ShouldBe(new DateRange(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void BlockedPeriod_WhenOneWay_AddsSevenDaysOfRelocation()
    {
        // Returned in Wellington on the 13th, relocated 14th–20th, next pickup in Auckland the 21st.
        var booking = Book(itinerary: October(10, 13, pickupCity: City.Auckland, dropOffCity: City.Wellington)).Value;

        booking.BlockedPeriod.ShouldBe(new DateRange(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 21)));
    }

    // ---- ClashesWith ----

    // The existing booking is 10th → 13th, back to Auckland: blocked 10th–14th (the 14th is cleaning).
    [Theory]
    [InlineData(11, 12, true)]   // during the rental
    [InlineData(14, 16, true)]   // picked up on the cleaning day
    [InlineData(15, 18, false)]  // the first day after cleaning
    [InlineData(7, 9, true)]     // returned the 9th, cleaned the 10th: not ready for the 10th pickup
    [InlineData(5, 8, false)]    // returned the 8th, cleaned the 9th: ready for the 10th
    public void ClashesWith_SameCar_DependsOnBlockedPeriods(int otherPickupDay, int otherReturnDay, bool expected)
    {
        var car = CreateCar();
        var existing = Book(car: car, itinerary: October(10, 13)).Value;
        var other = Book(car: car, itinerary: October(otherPickupDay, otherReturnDay)).Value;

        existing.ClashesWith(other).ShouldBe(expected);
    }

    [Fact]
    public void ClashesWith_DifferentCar_IsFalse()
    {
        var existing = Book(car: CreateCar(), itinerary: October(10, 13)).Value;
        var other = Book(car: CreateCar(), itinerary: October(10, 13)).Value;

        existing.ClashesWith(other).ShouldBeFalse();
    }

    [Theory]
    [InlineData(20, 22, true)]   // the car is still being relocated
    [InlineData(21, 23, false)]  // back in Auckland
    public void ClashesWith_AfterOneWayRental_IncludesRelocation(int otherPickupDay, int otherReturnDay, bool expected)
    {
        var car = CreateCar();
        var existing = Book(car: car, itinerary: October(10, 13, pickupCity: City.Auckland, dropOffCity: City.Wellington)).Value;
        var other = Book(car: car, itinerary: October(otherPickupDay, otherReturnDay)).Value;

        existing.ClashesWith(other).ShouldBe(expected);
    }
}
