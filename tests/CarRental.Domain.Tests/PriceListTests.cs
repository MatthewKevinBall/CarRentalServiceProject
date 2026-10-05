using Shouldly;

namespace CarRental.Domain.Tests;

public class PriceListTests
{
    // Deliberately not the real prices: distinct, easy numbers make a wrong rate lookup obvious,
    // and these tests don't break when the business changes its prices.
    private static PriceList CreatePriceList(
        decimal economy = 10m,
        decimal standard = 20m,
        decimal suv = 30m,
        decimal premium = 40m,
        decimal oneWayFee = 7m,
        decimal depositPercentage = 10m) =>
        new(new Money(economy), new Money(standard), new Money(suv), new Money(premium), new Money(oneWayFee), depositPercentage);

    // A trip from Auckland for the given number of days; one-way when a different drop-off city is given.
    private static Itinerary Trip(int days, City? dropOffCity = null) =>
        new(City.Auckland, dropOffCity ?? City.Auckland, new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1).AddDays(days)));

    // ---- Constructor rules ----

    [Theory]
    [InlineData(0, 20, 30, 40)]
    [InlineData(10, 0, 30, 40)]
    [InlineData(10, 20, 0, 40)]
    [InlineData(10, 20, 30, 0)]
    public void Constructor_WithZeroDailyRate_Throws(int economy, int standard, int suv, int premium)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreatePriceList(economy, standard, suv, premium));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_WithDepositPercentageOutside0To100_Throws(int depositPercentage)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreatePriceList(depositPercentage: depositPercentage));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Constructor_WithDepositPercentageAtBoundary_IsAccepted(int depositPercentage)
    {
        Should.NotThrow(() => CreatePriceList(depositPercentage: depositPercentage));
    }

    // ---- Quote ----

    [Theory]
    [InlineData(CarType.Economy, 10)]
    [InlineData(CarType.Standard, 20)]
    [InlineData(CarType.Suv, 30)]
    [InlineData(CarType.Premium, 40)]
    public void Quote_DailyRate_IsTheChargedTypesRate(CarType chargedType, int expectedRate)
    {
        var quote = CreatePriceList().Quote(chargedType, Trip(3));

        quote.DailyRate.ShouldBe(new Money(expectedRate));
    }

    [Fact]
    public void Quote_WithUndefinedCarType_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => CreatePriceList().Quote((CarType)42, Trip(3)));
    }

    [Fact]
    public void Quote_RentalPrice_IsDailyRateTimesDays()
    {
        var quote = CreatePriceList(suv: 30m).Quote(CarType.Suv, Trip(4));

        quote.Days.ShouldBe(4);
        quote.RentalPrice.ShouldBe(new Money(120m));
    }

    [Fact]
    public void Quote_WhenDropOffCityDiffers_ChargesOneWayFee()
    {
        var quote = CreatePriceList(oneWayFee: 7m).Quote(CarType.Suv, Trip(3, dropOffCity: City.Wellington));

        quote.OneWayFee.ShouldBe(new Money(7m));
    }

    [Fact]
    public void Quote_WhenDropOffCityIsPickupCity_ChargesNoOneWayFee()
    {
        var quote = CreatePriceList(oneWayFee: 7m).Quote(CarType.Suv, Trip(3));

        quote.OneWayFee.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Quote_Deposit_IsPercentageOfRentalPriceOnly()
    {
        // One-way, so a fee exists that the deposit must ignore: 10% of 90, not of 97.
        var quote = CreatePriceList(suv: 30m, oneWayFee: 7m, depositPercentage: 10m)
            .Quote(CarType.Suv, Trip(3, dropOffCity: City.Wellington));

        quote.Deposit.ShouldBe(new Money(9m));
    }

    [Fact]
    public void Quote_Deposit_RoundsUpToTheCent()
    {
        // 10% of 164.81 is 16.481: rounding to the nearest cent would give 16.48.
        var quote = CreatePriceList(economy: 164.81m, depositPercentage: 10m)
            .Quote(CarType.Economy, Trip(1));

        quote.Deposit.ShouldBe(new Money(16.49m));
    }

    [Fact]
    public void Quote_Total_IsRentalPricePlusOneWayFeePlusDeposit()
    {
        // 90 rental + 7 one-way + 9 deposit
        var quote = CreatePriceList(suv: 30m, oneWayFee: 7m, depositPercentage: 10m)
            .Quote(CarType.Suv, Trip(3, dropOffCity: City.Wellington));

        quote.Total.ShouldBe(new Money(106m));
    }

    [Fact]
    public void Quote_UserStoryExample_SuvAucklandToWellingtonFor3Days_Totals446_50()
    {
        // The agreed prices and the worked example from docs/user-stories.md (US2).
        var priceList = new PriceList(
            economy: new Money(55m),
            standard: new Money(75m),
            suv: new Money(105m),
            premium: new Money(140m),
            oneWayFee: new Money(100m),
            depositPercentage: 10m);

        var quote = priceList.Quote(CarType.Suv, Trip(3, dropOffCity: City.Wellington));

        quote.Total.ShouldBe(new Money(446.50m));
    }
}
