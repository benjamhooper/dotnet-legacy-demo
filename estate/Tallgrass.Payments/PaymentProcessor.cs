using System;
using System.Net.Http;
using Tallgrass.Billing;
using Tallgrass.Business;
using Tallgrass.Common;
using Tallgrass.Data;

namespace Tallgrass.Payments
{
    public class PaymentProcessor
    {
        private static readonly HttpClient Gateway = new HttpClient { BaseAddress = new Uri(AppSettings.Get("Gateway:Url") ?? "https://gateway.invalid/") };

        public bool TakeCardPayment(int accountId, decimal amount, string token)
        {
            if (amount <= 0m) return false;
            var response = Gateway.PostAsync("charge?token=" + token + "&amount=" + amount, null).Result;   // .Result: 2014
            if (!response.IsSuccessStatusCode)
            {
                AuditLog.Write("Payments", "Declined " + accountId + " " + DateTime.Now);
                return false;
            }
            new BillingManager().ApplyPayment(accountId, amount, "CARD");
            return true;
        }

        public void PostAch(int accountId, decimal amount)
        {
            using (var db = Db.Open())
            {
                db.Execute("usp_ApplyPayment", "@AccountId", accountId, "@Amount", amount, "@Channel", "ACH", "@On", DateTime.Now);
            }
            CustomerService.Instance.Notify(CustomerService.Instance.Find(accountId), "PAYMENT_RECEIVED");
        }

        public decimal ConvenienceFee(decimal amount)
        {
            var pct = AppSettings.Get("Payments:ConvenienceFeePct");
            return string.IsNullOrEmpty(pct) ? 0m : Math.Round(amount * decimal.Parse(pct) / 100m, 2);
        }
    }
}
