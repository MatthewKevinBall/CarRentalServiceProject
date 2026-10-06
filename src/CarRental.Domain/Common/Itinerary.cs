namespace CarRental.Domain.Common;

/// <summary>Where and when a rental happens: picked up in one city, dropped off in another (or the same), over a period.</summary>
/// <remarks>
/// A positional record is fine here, unlike DateRange or Money: Itinerary has no rules of its own,
/// so a `with` expression skipping the constructor can't create an invalid one.
/// </remarks>
public sealed record Itinerary(City PickupCity, City DropOffCity, DateRange Period)
{
    /// <summary>The single home of the one-way rule, used by pricing (fee) and booking (relocation break).</summary>
    public bool IsOneWay => DropOffCity != PickupCity;
}
