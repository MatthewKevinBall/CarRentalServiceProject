using CarRental.Domain.Common;
using CarRental.Domain.Fleet;
using Shouldly;

namespace CarRental.Domain.Tests.Fleet;

public class CarTests
{
    // A valid car; each test overrides only the detail it's about.
    private static Car CreateCar(
        string make = "Toyota",
        string model = "Corolla",
        CarType type = CarType.Economy,
        City? homeCity = null) =>
        new(new Registration("ABC123"), make, model, type, homeCity ?? City.Auckland);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankMake_Throws(string make)
    {
        Should.Throw<ArgumentException>(() => CreateCar(make: make));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankModel_Throws(string model)
    {
        Should.Throw<ArgumentException>(() => CreateCar(model: model));
    }

    [Theory]
    [InlineData((CarType)0)]
    [InlineData((CarType)42)]
    public void Constructor_WithUndefinedCarType_Throws(CarType type)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateCar(type: type));
    }

    [Fact]
    public void Constructor_GivesEachCarItsOwnId()
    {
        var first = CreateCar();
        var second = CreateCar();

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public void Constructor_SetsCurrentCityToHomeCity()
    {
        var car = CreateCar(homeCity: City.Christchurch);

        car.CurrentCity.ShouldBe(City.Christchurch);
    }

    [Fact]
    public void RecordArrivalIn_ChangesCurrentCity()
    {
        var car = CreateCar(homeCity: City.Auckland);

        car.RecordArrivalIn(City.Wellington);

        car.CurrentCity.ShouldBe(City.Wellington);
    }

    [Fact]
    public void RecordArrivalIn_DoesNotChangeHomeCity()
    {
        var car = CreateCar(homeCity: City.Auckland);

        car.RecordArrivalIn(City.Wellington);

        car.HomeCity.ShouldBe(City.Auckland);
    }
}
