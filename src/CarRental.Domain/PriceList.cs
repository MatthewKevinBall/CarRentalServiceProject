namespace CarRental.Domain;

/// <summary>
/// The business's prices, and how to quote with them. The numbers are supplied from outside the domain
/// (configuration, from Phase 4), so a price change doesn't need a code change.
/// </summary>
public sealed class PriceList
{
    private readonly Money _economy;
    private readonly Money _standard;
    private readonly Money _suv;
    private readonly Money _premium;
    private readonly Money _oneWayFee;
    private readonly decimal _depositPercentage;

    public PriceList(Money economy, Money standard, Money suv, Money premium, Money oneWayFee, decimal depositPercentage)
    {
        // Money already rules out negative amounts and fractions of a cent; a free rental is the remaining mistake.
        ArgumentOutOfRangeException.ThrowIfZero(economy.Amount, nameof(economy));
        ArgumentOutOfRangeException.ThrowIfZero(standard.Amount, nameof(standard));
        ArgumentOutOfRangeException.ThrowIfZero(suv.Amount, nameof(suv));
        ArgumentOutOfRangeException.ThrowIfZero(premium.Amount, nameof(premium));
        ArgumentOutOfRangeException.ThrowIfNegative(depositPercentage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depositPercentage, 100m);

        _economy = economy;
        _standard = standard;
        _suv = suv;
        _premium = premium;
        _oneWayFee = oneWayFee;
        _depositPercentage = depositPercentage;
    }

    public PriceQuote Quote(CarType chargedType, Itinerary itinerary)
    {
        var dailyRate = RateFor(chargedType);
        var days = itinerary.Period.Days;
        var rentalPrice = dailyRate * days;
        var oneWayFee = itinerary.IsOneWay ? _oneWayFee : Money.Zero;

        // The deposit is on the rental price only, never on the one-way fee.
        var deposit = rentalPrice.PercentageRoundedUp(_depositPercentage);

        return new PriceQuote(dailyRate, days, rentalPrice, oneWayFee, deposit);
    }

    private Money RateFor(CarType chargedType) => chargedType switch
    {
        CarType.Economy => _economy,
        CarType.Standard => _standard,
        CarType.Suv => _suv,
        CarType.Premium => _premium,
        _ => throw new ArgumentOutOfRangeException(nameof(chargedType), chargedType, "Unknown car type."),
    };
}
