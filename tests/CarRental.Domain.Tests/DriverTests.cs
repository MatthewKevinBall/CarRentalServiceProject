using Shouldly;

namespace CarRental.Domain.Tests;

public class DriverTests
{
    private static Driver CreateDriver(string fullName) =>
        new(
            fullName,
            new EmailAddress("jane@example.co.nz"),
            new DriverLicence("DI123456", LicenceType.Full, new DateOnly(2030, 1, 1)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string fullName)
    {
        Should.Throw<ArgumentException>(() => CreateDriver(fullName));
    }

    [Theory]
    [InlineData(" Jane Smith")]
    [InlineData("Jane Smith ")]
    public void Constructor_WithSurroundingWhitespaceInName_Throws(string fullName)
    {
        Should.Throw<ArgumentException>(() => CreateDriver(fullName));
    }

    [Fact]
    public void Constructor_WithNameOf100Characters_IsAccepted()
    {
        Should.NotThrow(() => CreateDriver(new string('a', 100)));
    }

    [Fact]
    public void Constructor_WithNameOver100Characters_Throws()
    {
        Should.Throw<ArgumentException>(() => CreateDriver(new string('a', 101)));
    }
}
