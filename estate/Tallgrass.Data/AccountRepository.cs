using System;
using System.Collections.Generic;
using Tallgrass.Common;

namespace Tallgrass.Data
{
    public static class AccountRepository
    {
        public static Account GetAccount(SqlDb db, int accountId)
        {
            var row = db.QuerySingle("usp_GetAccount", "@AccountId", accountId);
            return row == null ? null : Map(row);
        }

        public static List<Account> GetPastDueAccounts(SqlDb db, DateTime asOf)
        {
            var result = new List<Account>();
            foreach (var row in db.Query("usp_GetPastDueAccounts", "@AsOf", asOf)) result.Add(Map(row));
            return result;
        }

        public static void UpdateBalance(SqlDb db, int accountId, decimal balance)
        {
            db.Execute("usp_UpdateAccountBalance", "@AccountId", accountId, "@Balance", balance,
                       "@ModifiedBy", Constants.SYSTEM_USER, "@ModifiedOn", DateTime.Now);
        }

        public static Account Map(IDictionary<string, object> row)
        {
            return new Account
            {
                AccountId = Convert.ToInt32(row["AccountId"]),
                AccountNumber = Convert.ToString(row["AccountNumber"]),
                CustomerName = Convert.ToString(row["CustomerName"]),
                RateClass = (RateClass)Convert.ToInt32(row["RateClassId"]),
                DueDate = Convert.ToDateTime(row["DueDate"]),
                PastDueAmount = Convert.ToDecimal(row["PastDueAmount"]),
                CurrentBalance = Convert.ToDecimal(row["CurrentBalance"]),
                AutoPay = Convert.ToBoolean(row["AutoPay"]),
                HardshipProgram = Convert.ToBoolean(row["HardshipProgram"]),
                LateCount12Months = Convert.ToInt32(row["LateCount12Months"]),
                LastPaymentDate = row["LastPaymentDate"] is DBNull ? (DateTime?)null : Convert.ToDateTime(row["LastPaymentDate"]),
                Status = Convert.ToString(row["Status"])
            };
        }
    }
}
