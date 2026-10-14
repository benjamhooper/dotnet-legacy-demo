using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tallgrass.FinalBill;

public enum Rounding
{
    /// <summary>Compute the prorated amount exactly, round once at the end.</summary>
    Once,
    /// <summary>Round the daily rate to the cent first, then multiply by days. What DECIMAL(10,2) did.</summary>
    PerDay,
}

public enum MonthBasis
{
    /// <summary>Divide by the real number of days in the billing month.</summary>
    Actual,
    /// <summary>Divide by 30, always. The 2019 "temporary" change.</summary>
    Thirty,
}

/// <summary>
/// Every behavior the legacy procedure had implicitly, as a named decision.
///
/// The defaults are what a reasonable engineer would write from the tariff document.
/// They are not what production has been doing. `legacy shadow` finds out which of
/// these the business actually depends on.
/// </summary>
public sealed record ProrationPolicy
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Rounding Rounding { get; init; } = Rounding.Once;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MonthBasis MonthBasis { get; init; } = MonthBasis.Actual;

    /// <summary>Balances (either direction) smaller than this are written off to zero. Null = never.</summary>
    public decimal? SmallBalanceWriteOffUnder { get; init; }

    public string? Decision { get; init; }

    public static ProrationPolicy Default { get; } = new();

    public string Describe() =>
        $"rounding={Rounding.ToString().ToLowerInvariant()}  monthBasis={MonthBasis.ToString().ToLowerInvariant()}  " +
        $"writeOffUnder={(SmallBalanceWriteOffUnder is { } w ? w.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "none")}";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ProrationPolicy Load(string path) =>
        JsonSerializer.Deserialize<ProrationPolicy>(File.ReadAllText(path), Json)
        ?? throw new InvalidOperationException($"Could not read a proration policy from {path}.");
}
