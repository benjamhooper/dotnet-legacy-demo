namespace Tallgrass.Billing.Characterization;

/// <summary>
/// Attempt 1. The test you write on day one, the obvious way.
/// It compiles. It will never run.
/// </summary>
public static class LateFeeCharacterization
{
    public const string Approach = "new BillingManager()   (the only constructor there is)";

    public static decimal Calculate(GoldenCase c)
    {
        var manager = new BillingManager();             // opens a SQL Server connection
        return manager.CalculateLateFee(c.AccountId);   // ...and asks the wall clock what day it is
    }
}
