using CarRental.Domain.Fleet;
using Shouldly;

namespace CarRental.Domain.Tests.Fleet;

public class RegistrationTests
{
    [Theory]
    [InlineData("abc123", "ABC123")]
    [InlineData("Ab 12", "AB 12")]
    public void Constructor_NormalisesToUpperCase(string input, string expected)
    {
        var registration = new Registration(input);

        registration.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("A")]       // shortest plate
    [InlineData("123")]     // digits only
    [InlineData("HELLO1")]  // longest plate without spaces
    [InlineData("AB 123")]  // longest plate with a space: spaces count towards the 6
    [InlineData("A B C")]   // several single spaces
    public void Constructor_WithValidPlate_KeepsItExactly(string input)
    {
        var registration = new Registration(input);

        registration.Value.ShouldBe(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ABC1234")]  // 7 characters
    [InlineData("ABC 123")]  // 7 characters including the space
    [InlineData(" AB12")]    // leading space
    [InlineData("AB12 ")]    // trailing space
    [InlineData("AB  12")]   // double space
    [InlineData("AB\t12")]   // whitespace other than a space
    [InlineData("AB12\n")]   // trailing newline
    [InlineData("ABC-12")]   // symbol
    [InlineData("ÄBC12")]    // a letter, but not one an NZ plate can contain
    [InlineData("ſAB12")]    // long s: upper-cases to an ASCII 'S'
    public void Constructor_WithInvalidPlate_Throws(string input)
    {
        Should.Throw<ArgumentException>(() => new Registration(input));
    }
}
