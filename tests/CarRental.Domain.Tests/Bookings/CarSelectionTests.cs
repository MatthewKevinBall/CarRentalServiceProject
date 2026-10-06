using CarRental.Domain.Bookings;
using CarRental.Domain.Common;
using CarRental.Domain.Fleet;
using Shouldly;

namespace CarRental.Domain.Tests.Bookings;

public class CarSelectionTests
{
    private static Car CreateCar(CarType type = CarType.Suv) =>
        new(new Registration("ABC123"), "Toyota", "RAV4", type, City.Auckland);

    // ---- Matches ----

    [Fact]
    public void SpecificCar_MatchesThatCar()
    {
        var car = CreateCar();

        new CarSelection.SpecificCar(car.Id).Matches(car).ShouldBeTrue();
    }

    [Fact]
    public void SpecificCar_DoesNotMatchAnotherCar()
    {
        var chosen = CreateCar();
        var other = CreateCar();

        new CarSelection.SpecificCar(chosen.Id).Matches(other).ShouldBeFalse();
    }

    [Fact]
    public void OfType_MatchesCarOfThatType()
    {
        new CarSelection.OfType(CarType.Suv).Matches(CreateCar(CarType.Suv)).ShouldBeTrue();
    }

    [Fact]
    public void OfType_DoesNotMatchCarOfAnotherType()
    {
        new CarSelection.OfType(CarType.Economy).Matches(CreateCar(CarType.Suv)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(CarType.Economy)]
    [InlineData(CarType.Standard)]
    [InlineData(CarType.Suv)]
    [InlineData(CarType.Premium)]
    public void AnyAvailable_MatchesAnyCar(CarType type)
    {
        new CarSelection.AnyAvailable().Matches(CreateCar(type)).ShouldBeTrue();
    }

    // ---- ChargedTypeFor ----

    [Fact]
    public void SpecificCar_ChargesTheCarsType()
    {
        var car = CreateCar(CarType.Premium);

        new CarSelection.SpecificCar(car.Id).ChargedTypeFor(car).ShouldBe(CarType.Premium);
    }

    [Fact]
    public void OfType_ChargesTheCarsType()
    {
        new CarSelection.OfType(CarType.Suv).ChargedTypeFor(CreateCar(CarType.Suv)).ShouldBe(CarType.Suv);
    }

    [Theory]
    [InlineData(CarType.Economy)]  // even when the assigned car is cheaper
    [InlineData(CarType.Standard)]
    [InlineData(CarType.Suv)]
    [InlineData(CarType.Premium)]
    public void AnyAvailable_AlwaysChargesTheStandardRate(CarType assignedType)
    {
        new CarSelection.AnyAvailable().ChargedTypeFor(CreateCar(assignedType)).ShouldBe(CarType.Standard);
    }
}
