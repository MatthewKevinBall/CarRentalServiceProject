namespace CarRental.Domain.Drivers;

/// <summary>
/// Who is driving, for one booking. A value object, not an entity: there are no customer accounts,
/// so the same person booking twice is simply two sets of details.
/// </summary>
public sealed record Driver
{
    private const int MaxNameLength = 100;

    public Driver(string fullName, EmailAddress email, DriverLicence licence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        if (fullName != fullName.Trim())
        {
            throw new ArgumentException("The name must not start or end with whitespace.", nameof(fullName));
        }

        if (fullName.Length > MaxNameLength)
        {
            throw new ArgumentException($"The name must be at most {MaxNameLength} characters.", nameof(fullName));
        }

        FullName = fullName;
        Email = email;
        Licence = licence;
    }

    /// <summary>As it appears on the licence.</summary>
    public string FullName { get; }

    public EmailAddress Email { get; }

    public DriverLicence Licence { get; }
}
