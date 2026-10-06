using System.Text.RegularExpressions;

namespace CarRental.Domain.Fleet;

/// <summary>
/// An NZ number plate, stored upper case. Spaces are part of the plate: "AB 12" and "AB12" are different plates.
/// </summary>
public sealed partial record Registration
{
    private const int MaxLength = 6;

    public Registration(string value)
    {
        // Validate before upper-casing: ToUpperInvariant turns some non-ASCII letters into ASCII ones
        // (the long s 'ſ' becomes 'S'), which would sneak them past the pattern.
        if (value.Length > MaxLength || !PlatePattern().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid plate: 1–{MaxLength} characters; letters and digits, with single spaces only between them.",
                nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    // Groups of letters/digits separated by single spaces.
    // \z, not $: in .NET, $ also matches just before a trailing newline.
    [GeneratedRegex(@"^[A-Za-z0-9]+( [A-Za-z0-9]+)*\z")]
    private static partial Regex PlatePattern();
}
