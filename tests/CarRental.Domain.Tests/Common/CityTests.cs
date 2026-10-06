using CarRental.Domain.Common;
using Shouldly;

namespace CarRental.Domain.Tests.Common;

public class CityTests
{
    // City instances aren't compile-time constants, so they can't go in [InlineData];
    // [MemberData] reads rows from this property instead.
    public static TheoryData<string, City> KnownCodes => new()
    {
        { "AKL", City.Auckland },
        { "WLG", City.Wellington },
        { "CHC", City.Christchurch },
    };

    [Fact]
    public void All_ContainsExactlyTheThreeStarterCities()
    {
        City.All.ShouldBe([City.Auckland, City.Wellington, City.Christchurch], ignoreOrder: true);
    }

    [Fact]
    public void All_HaveUniqueCodes()
    {
        // Guards future additions: a copy-pasted code would make FromCode ambiguous.
        var codes = City.All.Select(city => city.Code.ToUpperInvariant());

        codes.ShouldBeUnique();
    }

    [Theory]
    [MemberData(nameof(KnownCodes))]
    public void FromCode_WithKnownCode_ReturnsThatCity(string code, City expected)
    {
        var city = City.FromCode(code);

        city.ShouldBe(expected);
    }

    [Theory]
    [InlineData("akl")]
    [InlineData("Akl")]
    [InlineData("aKL")]
    public void FromCode_IsCaseInsensitive(string code)
    {
        var city = City.FromCode(code);

        city.ShouldBe(City.Auckland);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData("Auckland")]  // a name is not a code
    public void FromCode_WithUnknownCode_Throws(string code)
    {
        Should.Throw<ArgumentException>(() => City.FromCode(code));
    }
}
