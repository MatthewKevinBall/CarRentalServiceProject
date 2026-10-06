using CarRental.Domain.Common;
using Shouldly;

namespace CarRental.Domain.Tests.Common;

public class ItineraryTests
{
    private static readonly DateRange _period = new(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 13));

    [Fact]
    public void IsOneWay_WhenDropOffCityDiffersFromPickupCity_IsTrue()
    {
        var itinerary = new Itinerary(City.Auckland, City.Wellington, _period);

        itinerary.IsOneWay.ShouldBeTrue();
    }

    [Fact]
    public void IsOneWay_WhenDropOffCityIsPickupCity_IsFalse()
    {
        // The drop-off city arrives as a code from the booking form, the pickup city as a known instance.
        var itinerary = new Itinerary(City.Auckland, City.FromCode("akl"), _period);

        itinerary.IsOneWay.ShouldBeFalse();
    }
}
