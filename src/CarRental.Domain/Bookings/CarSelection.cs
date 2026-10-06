using CarRental.Domain.Fleet;

namespace CarRental.Domain.Bookings;

/// <summary>What the customer chose: a specific car, a type of car, or any available car.</summary>
/// <remarks>
/// A closed family of records, C#'s usual stand-in for a discriminated union. The private constructor
/// stops accidental subtypes elsewhere, but not deliberate ones: records must keep a protected copy
/// constructor, which a determined subclass can chain to. Hence the explicit "unknown selection" arms.
/// </remarks>
public abstract record CarSelection
{
    private CarSelection()
    {
    }

    public bool Matches(Car car) => this switch
    {
        SpecificCar specific => car.Id == specific.CarId,
        OfType ofType => car.Type == ofType.Type,
        AnyAvailable => true,
        _ => throw UnknownSelection(),
    };

    /// <summary>The car type whose daily rate is charged.</summary>
    public CarType ChargedTypeFor(Car car) => this switch
    {
        SpecificCar or OfType => car.Type,
        AnyAvailable => CarType.Standard, // always the Standard rate, whatever car is assigned
        _ => throw UnknownSelection(),
    };

    private NotSupportedException UnknownSelection() => new($"Unknown kind of selection: {GetType().Name}.");

    public sealed record SpecificCar(Guid CarId) : CarSelection;

    public sealed record OfType(CarType Type) : CarSelection;

    public sealed record AnyAvailable : CarSelection;
}
