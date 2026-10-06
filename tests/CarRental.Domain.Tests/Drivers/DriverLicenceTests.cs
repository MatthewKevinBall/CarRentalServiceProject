using CarRental.Domain.Drivers;
using Shouldly;

namespace CarRental.Domain.Tests.Drivers;

public class DriverLicenceTests
{
    private static readonly DateOnly _returnDate = new(2026, 10, 13);

    private static DriverLicence CreateLicence(
        string number = "DI123456",
        LicenceType type = LicenceType.Full,
        DateOnly? expiryDate = null) =>
        new(number, type, expiryDate ?? new DateOnly(2030, 1, 1));

    [Theory]
    [InlineData("DI123456", "DI123456")]
    [InlineData("di123456", "DI123456")]
    [InlineData("Di123456", "DI123456")]
    public void Constructor_WithValidNumber_StoresItUpperCase(string number, string expected)
    {
        var licence = CreateLicence(number: number);

        licence.Number.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("DI12345")]     // too short
    [InlineData("DI1234567")]   // too long
    [InlineData("D1234567")]    // 1 letter, 7 digits
    [InlineData("DIA12345")]    // 3 letters, 5 digits
    [InlineData("12345678")]    // no letters
    [InlineData("DI-23456")]    // symbol
    [InlineData("DI 123456")]   // space inside
    [InlineData(" DI123456")]   // leading space
    [InlineData("DI123456 ")]   // trailing space
    [InlineData("DI123456\n")]  // trailing newline
    [InlineData("ÄI123456")]    // non-ASCII letter
    [InlineData("ſI123456")]    // long s: upper-cases to an ASCII 'S'
    [InlineData("DI١٢٣٤٥٦")]    // Arabic-Indic digits: digits, but not 0–9
    public void Constructor_WithInvalidNumber_Throws(string number)
    {
        Should.Throw<ArgumentException>(() => CreateLicence(number: number));
    }

    [Theory]
    [InlineData((LicenceType)0)]
    [InlineData((LicenceType)42)]
    public void Constructor_WithUndefinedLicenceType_Throws(LicenceType type)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateLicence(type: type));
    }

    [Theory]
    [InlineData(LicenceType.Learner)]
    [InlineData(LicenceType.Restricted)]
    public void Constructor_WithLicenceThatCannotRent_IsStillAValidLicence(LicenceType type)
    {
        Should.NotThrow(() => CreateLicence(type: type));
    }

    [Theory]
    [InlineData(20, true)]   // expires after the return date
    [InlineData(13, true)]   // expires on the return date
    [InlineData(12, false)]  // expires the day before the return date
    public void PermitsRentalUntil_WithFullLicence_DependsOnExpiryDate(int expiryDay, bool expected)
    {
        var licence = CreateLicence(type: LicenceType.Full, expiryDate: new DateOnly(2026, 10, expiryDay));

        licence.PermitsRentalUntil(_returnDate).ShouldBe(expected);
    }

    [Theory]
    [InlineData(LicenceType.Learner)]
    [InlineData(LicenceType.Restricted)]
    public void PermitsRentalUntil_WithoutFullLicence_IsFalse(LicenceType type)
    {
        var licence = CreateLicence(type: type, expiryDate: new DateOnly(2030, 1, 1));

        licence.PermitsRentalUntil(_returnDate).ShouldBeFalse();
    }
}
