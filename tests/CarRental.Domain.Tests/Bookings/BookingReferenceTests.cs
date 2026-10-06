using CarRental.Domain.Bookings;
using Shouldly;

namespace CarRental.Domain.Tests.Bookings;

public class BookingReferenceTests
{
    [Theory]
    [InlineData("K7QM4X", "K7QM4X")]
    [InlineData("k7qm4x", "K7QM4X")]
    public void Constructor_WithValidReference_StoresItUpperCase(string value, string expected)
    {
        var reference = new BookingReference(value);

        reference.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("K7QM4")]     // too short
    [InlineData("K7QM4XA")]   // too long
    [InlineData("K0QM4X")]    // zero: looks like O
    [InlineData("KOQM4X")]    // O: looks like zero
    [InlineData("K1QM4X")]    // one: looks like I or L
    [InlineData("KIQM4X")]    // I: looks like 1
    [InlineData("KLQM4X")]    // L: looks like 1
    [InlineData("kiqm4x")]    // look-alikes are excluded in lower case too
    [InlineData("K7QM-X")]    // symbol
    [InlineData(" K7QM4")]    // leading space
    [InlineData("K7QM4X ")]   // trailing space
    [InlineData("K7QM4X\n")]  // trailing newline
    [InlineData("ſ7QM4X")]    // long s: upper-cases to an ASCII 'S'
    public void Constructor_WithInvalidReference_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => new BookingReference(value));
    }
}
