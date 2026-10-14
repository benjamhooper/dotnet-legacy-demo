using System;
using System.Web;
using System.Web.UI;
using Tallgrass.Billing;
using Tallgrass.Common;
using Tallgrass.Payments;

namespace Tallgrass.CustomerPortal.Classic
{
    /// <summary>The business logic is in the button click handler. It always was.</summary>
    public partial class PayBill : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (AppSettings.GetBool("Portal:Maintenance")) Response.Redirect("~/Maintenance.aspx");
            var accountId = (int)HttpContext.Current.Session["AccountId"];
            AccountLabel.Text = Utils.FormatAccountNumber(accountId);
        }

        protected void PayButton_Click(object sender, EventArgs e)
        {
            var accountId = (int)HttpContext.Current.Session["AccountId"];
            decimal amount;
            if (!decimal.TryParse(AmountTextBox.Text, out amount) || amount <= 0m)
            {
                ResultLabel.Text = "Enter an amount";
                return;
            }
            var fee = new PaymentProcessor().ConvenienceFee(amount);
            var ok = new PaymentProcessor().TakeCardPayment(accountId, amount + fee, Request.Form["cardToken"]);
            ResultLabel.Text = ok ? "Thank you. Payment received " + DateTime.Now.ToString("g") : "Payment declined";
        }
    }
}
