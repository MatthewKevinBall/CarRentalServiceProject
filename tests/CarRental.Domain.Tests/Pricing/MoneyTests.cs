using CarRental.Domain.Pricing;
using Shouldly;

namespace CarRental.Domain.Tests.Pricing;

public class MoneyTests
{
    // decimal can't be used in attributes, so decimal theory rows come from TheoryData.
    public static TheoryData<decimal> WholeCentAmounts => new() { 0m, 0.01m, 16.5m, 16.500m, 446.50m };

    public static TheoryData<decimal> FractionOfACentAmounts => new() { 0.001m, 16.485m };

    public static TheoryData<decimal, decimal, decimal> Percentages => new()
    {
        // amount, percent, expected
        { 315.00m, 10m, 31.50m },   // exact: no rounding needed
        { 164.81m, 10m, 16.49m },   // 16.481 rounds UP, not to the nearest cent
        { 100.00m, 0m, 0.00m },
    };

    [Theory]
    [MemberData(nameof(WholeCentAmounts))]
    public void Constructor_WithWholeCents_IsAccepted(decimal amount)
    {
        Should.NotThrow(() => new Money(amount));
    }

    [Theory]
    [MemberData(nameof(FractionOfACentAmounts))]
    public void Constructor_WithFractionOfACent_Throws(decimal amount)
    {
        Should.Throw<ArgumentException>(() => new Money(amount));
    }

    [Fact]
    public void Constructor_WithNegativeAmount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Money(-0.01m));
    }

    [Fact]
    public void Add_ReturnsSumOfAmounts()
    {
        var sum = new Money(10.25m) + new Money(5.80m);

        sum.ShouldBe(new Money(16.05m));
    }

    [Fact]
    public void Multiply_ByWholeNumber_ReturnsAmountTimesNumber()
    {
        var product = new Money(54.95m) * 3;

        product.ShouldBe(new Money(164.85m));
    }

    [Theory]
    [MemberData(nameof(Percentages))]
    public void PercentageRoundedUp_ReturnsPercentageRoundedUpToTheCent(decimal amount, decimal percent, decimal expected)
    {
        var result = new Money(amount).PercentageRoundedUp(percent);

        result.ShouldBe(new Money(expected));
    }
}
