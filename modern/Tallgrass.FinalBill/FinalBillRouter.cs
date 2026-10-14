using System.Text.Json;

namespace Tallgrass.FinalBill;

public enum RouteMode { Legacy, Shadow, Modern }

/// <summary>One row of modern/routes.json.</summary>
public sealed record Route(RouteMode Mode, int ModernPercent);

/// <summary>
/// The strangler fig, in one class. Every caller of the final bill goes through here
/// instead of calling the procedure directly. That single change is the whole trick:
/// once there is one door, you get to decide which room it opens into.
///
///   Legacy : call the procedure. (Day one. Nothing has changed except that there is a door.)
///   Shadow : call both, return legacy, record every difference. (Weeks.)
///   Modern : a sticky percentage of accounts get the new code; the rest keep shadowing.
/// </summary>
public sealed class FinalBillRouter(
    IFinalBillEngine legacy,
    IFinalBillEngine modern,
    Func<Route> route,
    Action<FinalBillRequest, FinalBillResult, FinalBillResult> onDifference)
{
    public FinalBillResult Calculate(FinalBillRequest request)
    {
        var current = route();

        if (current.Mode == RouteMode.Modern && Bucket(request.AccountId) < current.ModernPercent)
            return modern.Calculate(request);

        var legacyResult = legacy.Calculate(request);
        if (current.Mode == RouteMode.Legacy) return legacyResult;

        try
        {
            var modernResult = modern.Calculate(request);
            if (modernResult.Total != legacyResult.Total) onDifference(request, legacyResult, modernResult);
        }
        catch (Exception)
        {
            // Shadow must never break production. The difference log is the signal.
            onDifference(request, legacyResult, new FinalBillResult(0m, 0m, decimal.MinValue));
        }
        return legacyResult;
    }

    /// <summary>Stable 0-99 bucket per account, so a customer never flips between engines.</summary>
    public static int Bucket(int accountId)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var b in BitConverter.GetBytes(accountId)) h = (h ^ b) * 16777619;
            return (int)(h % 100);
        }
    }
}

public static class RouteTable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static Dictionary<string, Route> Load(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, Route>>(File.ReadAllText(path), Json) ?? new();

    public static void Save(string path, Dictionary<string, Route> routes) =>
        File.WriteAllText(path, JsonSerializer.Serialize(routes, Json) + Environment.NewLine);
}
