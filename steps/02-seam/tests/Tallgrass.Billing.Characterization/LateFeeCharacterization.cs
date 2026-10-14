using Tallgrass.Data;

namespace Tallgrass.Billing.Characterization;

/// <summary>
/// Attempt 2. Same test, through the seam: recorded data in, a clock pinned to the day
/// the case was recorded. No database, no mocking library, no container.
/// </summary>
public static class LateFeeCharacterization
{
    public const string Approach = "new BillingManager(recordedAccount, new FixedClock(case.AsOf))";

    public static decimal Calculate(GoldenCase c)
    {
        var manager = new BillingManager(new RecordedAccount(c.ToAccount()), new FixedClock(c.AsOf));
        return manager.CalculateLateFee(c.AccountId);
    }

    private sealed class RecordedAccount(Account account) : IBillingData
    {
        public Account GetAccount(int accountId) => account;
    }
}
