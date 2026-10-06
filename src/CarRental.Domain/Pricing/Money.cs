namespace CarRental.Domain.Pricing;

/// <summary>
/// An amount of New Zealand dollars, always in whole cents and never negative.
/// A struct is safe here because default(Money) is $0.00, which is a valid amount.
/// </summary>
public readonly record struct Money
{
    public static readonly Money Zero = new(0m);

    public Money(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        // Compare values, not decimal.Scale: 16.500m has a scale of 3 but is still whole cents.
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException($"{amount} includes a fraction of a cent.", nameof(amount));
        }

        Amount = amount;
    }

    public decimal Amount { get; }

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator *(Money money, int times) => new(money.Amount * times);

    /// <summary>
    /// <paramref name="percent"/>% of this amount. Any fraction of a cent is rounded up, so the business never under-collects.
    /// </summary>
    public Money PercentageRoundedUp(decimal percent) =>
        // Despite the enum's name, ToPositiveInfinity isn't about midpoints: it always rounds up.
        new(decimal.Round(Amount * percent / 100, 2, MidpointRounding.ToPositiveInfinity));
}
