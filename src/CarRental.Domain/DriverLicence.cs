using System.Text.RegularExpressions;

namespace CarRental.Domain;

/// <summary>
/// A driver's licence as described by the customer. Any stage of licence is valid data;
/// whether it allows renting is a separate question (<see cref="PermitsRentalUntil"/>).
/// </summary>
public sealed partial record DriverLicence
{
    public DriverLicence(string number, LicenceType type, DateOnly expiryDate)
    {
        // Validate before upper-casing: see Registration for why the order matters.
        if (!NumberPattern().IsMatch(number))
        {
            throw new ArgumentException($"'{number}' is not an NZ licence number: 2 letters followed by 6 digits.", nameof(number));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown licence type.");
        }

        Number = number.ToUpperInvariant();
        Type = type;
        ExpiryDate = expiryDate;
    }

    public string Number { get; }

    public LicenceType Type { get; }

    /// <summary>The last day the licence is valid.</summary>
    public DateOnly ExpiryDate { get; }

    /// <summary>Only a full licence that is still valid on the return date permits renting.</summary>
    public bool PermitsRentalUntil(DateOnly returnDate) =>
        Type == LicenceType.Full && ExpiryDate >= returnDate;

    // [0-9], not \d: in .NET, \d matches every Unicode digit (e.g. Arabic-Indic ١٢٣).
    [GeneratedRegex(@"^[A-Za-z]{2}[0-9]{6}\z")]
    private static partial Regex NumberPattern();
}
