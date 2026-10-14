using Tallgrass.Common;
using Tallgrass.Data;

namespace Tallgrass.Billing
{
    /// <summary>Production implementation: exactly what BillingManager did before, moved.</summary>
    public class SqlBillingData : IBillingData
    {
        public Account GetAccount(int accountId)
        {
            using (var db = Db.Open())
            {
                return AccountRepository.GetAccount(db, accountId);
            }
        }
    }
}
