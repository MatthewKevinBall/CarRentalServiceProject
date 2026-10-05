namespace CarRental.Domain;

/// <summary>
/// A confirmed rental of one car. An entity: identified by <see cref="Id"/>. Never changes once created
/// (cancellations are out of scope), and only <see cref="Create"/> can make one, so every booking obeys the rules.
/// </summary>
public sealed class Booking
{
    private const int MaxRentalDays = 30;
    private const int CleaningDays = 1;
    private const int RelocationDays = 7;

    private Booking(BookingReference reference, Guid carId, Itinerary itinerary, Driver driver, PriceQuote price)
    {
        Id = Guid.CreateVersion7();
        Reference = reference;
        CarId = carId;
        Itinerary = itinerary;
        Driver = driver;
        Price = price;
    }

    public Guid Id { get; }

    public BookingReference Reference { get; }

    public Guid CarId { get; }

    public Itinerary Itinerary { get; }

    public Driver Driver { get; }

    /// <summary>The price agreed at booking time; later price changes don't affect it.</summary>
    public PriceQuote Price { get; }

    /// <summary>
    /// The days the car is unavailable to anyone else: the rental, the return day, then cleaning
    /// (back to the pickup city) or relocation home (one-way).
    /// </summary>
    public DateRange BlockedPeriod
    {
        get
        {
            var breakDays = Itinerary.IsOneWay ? RelocationDays : CleaningDays;

            // Period.End is exclusive, so +1 covers the return day itself before the break starts.
            return new DateRange(Itinerary.Period.Start, Itinerary.Period.End.AddDays(1 + breakDays));
        }
    }

    public static Result<Booking, BookingError> Create(
        BookingReference reference,
        Car car,
        CarSelection selection,
        Itinerary itinerary,
        Driver driver,
        PriceList priceList,
        DateOnly today)
    {
        // Check every rule rather than stopping at the first, so the customer hears about all problems at once.
        var errors = new List<BookingError>();

        if (itinerary.Period.Days > MaxRentalDays)
        {
            errors.Add(BookingError.RentalLongerThan30Days);
        }

        if (itinerary.Period.Start < today)
        {
            errors.Add(BookingError.PickupDateInPast);
        }

        if (!driver.Licence.PermitsRentalUntil(itinerary.Period.End))
        {
            errors.Add(BookingError.LicenceDoesNotPermitRental);
        }

        // Home city, not current city: after a one-way rental the car is relocated home.
        if (car.HomeCity != itinerary.PickupCity)
        {
            errors.Add(BookingError.CarNotBasedInPickupCity);
        }

        if (!selection.Matches(car))
        {
            errors.Add(BookingError.CarDoesNotMatchSelection);
        }

        if (errors.Count > 0)
        {
            return Result<Booking, BookingError>.Failure(errors);
        }

        var price = priceList.Quote(selection.ChargedTypeFor(car), itinerary);

        return Result<Booking, BookingError>.Success(new Booking(reference, car.Id, itinerary, driver, price));
    }

    /// <summary>Two bookings clash when they're for the same car and their blocked periods overlap.</summary>
    public bool ClashesWith(Booking other) =>
        CarId == other.CarId && BlockedPeriod.Overlaps(other.BlockedPeriod);
}
