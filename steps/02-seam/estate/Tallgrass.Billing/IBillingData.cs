using Tallgrass.Data;

namespace Tallgrass.Billing
{
    /// <summary>
    /// The one thing CalculateLateFee needs from the database, named.
    /// Extracted from BillingManager's direct AccountRepository calls; nothing else moved.
    /// </summary>
    public interface IBillingData
    {
        Account GetAccount(int accountId);
    }
}
