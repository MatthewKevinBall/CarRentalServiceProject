using System.Text.RegularExpressions;

namespace CarRental.Domain;

/// <summary>
/// The customer-facing booking reference, e.g. K7QM4X. Generated outside the domain (it needs randomness
/// and a uniqueness check); the domain only guarantees its format.
/// </summary>
public sealed partial record BookingReference
{
    public BookingReference(string value)
    {
        // Validate before upper-casing: see Registration for why the order matters.
        if (!ReferencePattern().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a booking reference: 6 letters or digits, excluding 0, O, 1, I and L.", nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    // Six characters with no look-alikes (no 0/O, no 1/I/L), so references are easy to read out over the phone.
    [GeneratedRegex(@"^[A-HJKMNP-Za-hjkmnp-z2-9]{6}\z")]
    private static partial Regex ReferencePattern();
}
