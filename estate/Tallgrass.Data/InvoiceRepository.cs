using System;
using System.Collections.Generic;
using Tallgrass.Common;

namespace Tallgrass.Data
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int AccountId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountPaid { get; set; }
        public string Status { get; set; }
    }

    public static class InvoiceRepository
    {
        public static List<Invoice> GetOpenInvoices(SqlDb db, int accountId)
        {
            var result = new List<Invoice>();
            foreach (var row in db.Query("usp_GetOpenInvoices", "@AccountId", accountId))
            {
                result.Add(new Invoice
                {
                    InvoiceId = Convert.ToInt32(row["InvoiceId"]),
                    AccountId = accountId,
                    InvoiceDate = Convert.ToDateTime(row["InvoiceDate"]),
                    DueDate = Convert.ToDateTime(row["DueDate"]),
                    Amount = Convert.ToDecimal(row["Amount"]),
                    AmountPaid = Convert.ToDecimal(row["AmountPaid"]),
                    Status = Convert.ToString(row["Status"])
                });
            }
            return result;
        }

        public static void AddCharge(SqlDb db, int accountId, string chargeType, decimal amount)
        {
            db.Execute("usp_AddCharge", "@AccountId", accountId, "@ChargeType", chargeType, "@Amount", amount,
                       "@CreatedOn", DateTime.Now);
        }
    }
}
