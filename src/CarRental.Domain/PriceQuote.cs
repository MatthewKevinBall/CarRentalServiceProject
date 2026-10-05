namespace CarRental.Domain;

/// <summary>
/// The price breakdown shown to a customer before booking (US2).
/// Only <see cref="PriceList.Quote"/> can create one, so the lines always follow the pricing rules.
/// </summary>
public sealed record PriceQuote
{
    internal PriceQuote(Money dailyRate, int days, Money rentalPrice, Money oneWayFee, Money deposit)
    {
        DailyRate = dailyRate;
        Days = days;
        RentalPrice = rentalPrice;
        OneWayFee = oneWayFee;
        Deposit = deposit;
    }

    public Money DailyRate { get; }

    public int Days { get; }

    public Money RentalPrice { get; }

    public Money OneWayFee { get; }

    public Money Deposit { get; }

    // Calculated, not stored, so the total can never disagree with the lines above it.
    public Money Total => RentalPrice + OneWayFee + Deposit;
}
