namespace Tallgrass.FinalBill;

/// <summary>Exactly the inputs dbo.usp_ProrateFinalBill takes. Same names, same meaning.</summary>
public sealed record FinalBillRequest(
    int AccountId,
    DateOnly CycleStart,
    DateOnly MoveOut,
    decimal MonthlyCustomerCharge,
    decimal UsageKwh,
    decimal RatePerKwh,
    decimal DepositHeld,
    decimal Credits);

/// <summary>Positive Total = the customer owes us. Negative = we owe the customer a refund.</summary>
public sealed record FinalBillResult(decimal BaseCharge, decimal UsageCharge, decimal Total);

public interface IFinalBillEngine
{
    string Name { get; }
    FinalBillResult Calculate(FinalBillRequest request);
}
