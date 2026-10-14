namespace Tallgrass.FinalBill;

/// <summary>
/// The modern final-bill calculation. About forty lines, pure, no database, no clock.
/// Compare with estate/Database/Procedures/dbo.usp_ProrateFinalBill.sql.
/// </summary>
public sealed class FinalBillCalculator(ProrationPolicy policy) : IFinalBillEngine
{
    public string Name => "FinalBillCalculator";
    public ProrationPolicy Policy => policy;

    public FinalBillResult Calculate(FinalBillRequest r)
    {
        if (r.MoveOut < r.CycleStart)
            throw new ArgumentException($"move-out {r.MoveOut:yyyy-MM-dd} is before the cycle start {r.CycleStart:yyyy-MM-dd}");

        var days = r.MoveOut.DayNumber - r.CycleStart.DayNumber + 1;
        decimal daysInMonth = policy.MonthBasis == MonthBasis.Thirty
            ? 30m
            : DateTime.DaysInMonth(r.CycleStart.Year, r.CycleStart.Month);

        // SQL Server rounds half away from zero when it assigns to DECIMAL(10,2). So do we.
        var baseCharge = policy.Rounding == Rounding.PerDay
            ? Money(r.MonthlyCustomerCharge / daysInMonth) * days
            : Money(r.MonthlyCustomerCharge * days / daysInMonth);

        var usageCharge = Money(r.UsageKwh * r.RatePerKwh);
        var total = baseCharge + usageCharge - r.DepositHeld - r.Credits;

        if (policy.SmallBalanceWriteOffUnder is { } limit && total != 0m && Math.Abs(total) < limit)
            total = 0m;

        return new FinalBillResult(baseCharge, usageCharge, total);
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
