using CarRental.Domain.Common;

namespace CarRental.Domain.Fleet;

/// <summary>
/// A car in the fleet. An entity: identified by <see cref="Id"/>, not by its details,
/// so two cars with identical details are still different cars.
/// </summary>
public sealed class Car
{
    public Car(Registration registration, string make, string model, CarType type, City homeCity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(make);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        // Enums are named integers, so (CarType)42 or default(CarType) can reach us.
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown car type.");
        }

        Id = Guid.CreateVersion7();
        Registration = registration;
        Make = make;
        Model = model;
        Type = type;
        HomeCity = homeCity;
        CurrentCity = homeCity;
    }

    public Guid Id { get; }

    public Registration Registration { get; }

    public string Make { get; }

    public string Model { get; }

    public CarType Type { get; }

    public City HomeCity { get; }

    /// <summary>Where the car physically is. Starts at <see cref="HomeCity"/>.</summary>
    public City CurrentCity { get; private set; }

    public void RecordArrivalIn(City city) => CurrentCity = city;
}
