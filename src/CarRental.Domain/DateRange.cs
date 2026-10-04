namespace CarRental.Domain;

/// <summary>
/// A period of whole days from <see cref="Start"/> up to, but not including, <see cref="End"/>.
/// </summary>
public sealed record DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentException($"End ({end:yyyy-MM-dd}) must be after start ({start:yyyy-MM-dd}).", nameof(end));
        }

        Start = start;
        End = end;
    }

    // Get-only (no `init`) so a `with` expression can't create a copy that skips the constructor's check.
    public DateOnly Start { get; }

    public DateOnly End { get; }

    // DateOnly has no subtraction operator; DayNumber counts days since 0001-01-01,
    // so month lengths and leap years are already accounted for.
    public int Days => End.DayNumber - Start.DayNumber;

    // Two half-open ranges overlap when each starts before the other ends.
    // Back-to-back ranges (one's End == the other's Start) therefore don't overlap.
    public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;
}
