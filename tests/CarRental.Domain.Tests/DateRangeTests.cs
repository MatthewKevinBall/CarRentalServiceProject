using Shouldly;

namespace CarRental.Domain.Tests;

public class DateRangeTests
{
    // Most tests use October 2026 so the test data reads as plain day numbers.
    private static DateOnly October(int day) => new(2026, 10, day);

    private static DateRange October(int startDay, int endDay) => new(October(startDay), October(endDay));

    [Fact]
    public void Days_WhenPickup10thAndReturn13th_Is3()
    {
        var range = October(10, 13);

        range.Days.ShouldBe(3);
    }

    [Fact]
    public void Days_WhenRangeCrossesMonthEnd_CountsCalendarDays()
    {
        var range = new DateRange(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 2));

        range.Days.ShouldBe(3);
    }

    [Fact]
    public void Days_WhenRangeCrossesLeapDay_IncludesFebruary29th()
    {
        var range = new DateRange(new DateOnly(2028, 2, 28), new DateOnly(2028, 3, 1));

        range.Days.ShouldBe(2);
    }

    [Fact]
    public void Constructor_WhenEndEqualsStart_Throws()
    {
        Should.Throw<ArgumentException>(() => October(10, 10));
    }

    [Fact]
    public void Constructor_WhenEndBeforeStart_Throws()
    {
        Should.Throw<ArgumentException>(() => October(13, 10));
    }

    [Fact]
    public void Equality_WhenSameDates_AreEqual()
    {
        // Two separate objects: a plain class would compare their references and say "not equal".
        var first = October(10, 13);
        var second = October(10, 13);

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WhenDifferentDates_AreNotEqual()
    {
        var first = October(10, 13);
        var second = October(10, 14);

        first.ShouldNotBe(second);
        (first != second).ShouldBeTrue();
    }

    // The fixed range is 10th → 15th: occupied days are 10th–14th, because End is exclusive.
    [Theory]
    [InlineData(8, 11)]   // overlaps the start
    [InlineData(14, 18)]  // overlaps the end
    [InlineData(11, 13)]  // inside
    [InlineData(8, 18)]   // surrounds
    [InlineData(10, 15)]  // identical
    public void Overlaps_WhenRangesShareAtLeastOneDay_ReturnsTrueFromEitherSide(int otherStartDay, int otherEndDay)
    {
        var range = October(10, 15);
        var other = October(otherStartDay, otherEndDay);

        range.Overlaps(other).ShouldBeTrue();
        other.Overlaps(range).ShouldBeTrue();
    }

    [Theory]
    [InlineData(15, 18)]  // starts the day this one ends (back-to-back)
    [InlineData(5, 10)]   // ends the day this one starts (back-to-back)
    [InlineData(20, 25)]  // entirely after
    [InlineData(1, 4)]    // entirely before
    public void Overlaps_WhenRangesShareNoDays_ReturnsFalseFromEitherSide(int otherStartDay, int otherEndDay)
    {
        var range = October(10, 15);
        var other = October(otherStartDay, otherEndDay);

        range.Overlaps(other).ShouldBeFalse();
        other.Overlaps(range).ShouldBeFalse();
    }
}
