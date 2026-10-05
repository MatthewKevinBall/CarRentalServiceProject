namespace CarRental.Domain;

/// <summary>
/// Business rules a booking request can break. Says which rule failed; wording for customers belongs in the web layer.
/// </summary>
public enum BookingError
{
    // Starts at 1 so default(BookingError), which is 0, is not a real error.
    RentalLongerThan30Days = 1,
    PickupDateInPast,
    LicenceDoesNotPermitRental,
    CarNotBasedInPickupCity,
    CarDoesNotMatchSelection,
}
