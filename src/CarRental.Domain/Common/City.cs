namespace CarRental.Domain.Common;

/// <summary>
/// A city we operate in. The constructor is private, so the static instances below are the only cities that exist.
/// </summary>
public sealed record City
{
    public static readonly City Auckland = new("AKL", "Auckland");

    public static readonly City Wellington = new("WLG", "Wellington");

    public static readonly City Christchurch = new("CHC", "Christchurch");

    // Must stay below the cities: static fields are initialised in source order,
    // so declaring this first would capture three nulls.
    public static IReadOnlyList<City> All { get; } = [Auckland, Wellington, Christchurch];

    private City(string code, string name)
    {
        Code = code;
        Name = name;
    }

    /// <summary>Stable identifier for storage and URLs (an NZ airport code).</summary>
    public string Code { get; }

    /// <summary>Display name; safe to change without touching stored data.</summary>
    public string Name { get; }

    public static City FromCode(string code) =>
        All.FirstOrDefault(city => string.Equals(city.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown city code '{code}'.", nameof(code));
}
