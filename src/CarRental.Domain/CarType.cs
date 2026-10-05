namespace CarRental.Domain;

/// <summary>The type of car, which sets its daily rate (rates live in pricing, not here).</summary>
public enum CarType
{
    // Starts at 1 so default(CarType), which is 0, is not a real type and can be rejected.
    Economy = 1,
    Standard = 2,
    Suv = 3,
    Premium = 4,
}
