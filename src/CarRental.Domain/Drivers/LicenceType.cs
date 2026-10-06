namespace CarRental.Domain.Drivers;

/// <summary>NZ driver licence stages. Only a full licence permits renting (see <see cref="DriverLicence.PermitsRentalUntil"/>).</summary>
public enum LicenceType
{
    // Starts at 1 so default(LicenceType), which is 0, is not a real type and can be rejected.
    Learner = 1,
    Restricted = 2,
    Full = 3,
}
