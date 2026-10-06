using CarRental.Domain.Drivers;
using Shouldly;

namespace CarRental.Domain.Tests.Drivers;

public class EmailAddressTests
{
    [Theory]
    [InlineData("jane@example.co.nz")]
    [InlineData("Jane.Smith@Example.com")]  // kept as entered: the part before @ can be case-sensitive
    [InlineData("a@b")]                     // email-shaped is enough
    public void Constructor_WithEmailShapedValue_KeepsItAsEntered(string value)
    {
        var email = new EmailAddress(value);

        email.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("jane.example.com")]    // no @
    [InlineData("@example.com")]        // nothing before @
    [InlineData("jane@")]               // nothing after @
    [InlineData(" jane@example.com")]   // leading space
    [InlineData("jane@example.com ")]   // trailing space
    public void Constructor_WithValueThatIsNotEmailShaped_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => new EmailAddress(value));
    }
}
