using System;
using System.Web;
using System.Web.UI;
using Tallgrass.Common;

namespace Tallgrass.CustomerPortal.Classic
{
    /// <summary>Self-service move-out. Calls the final-bill procedure directly, because in 2013 that was faster.</summary>
    public partial class MoveOut : Page
    {
        protected void SubmitButton_Click(object sender, EventArgs e)
        {
            var accountId = (int)HttpContext.Current.Session["AccountId"];
            var moveOut = MoveOutCalendar.SelectedDate;
            if (moveOut < DateTime.Now.Date) { EstimateLabel.Text = "Move-out date cannot be in the past"; return; }
            using (var db = Db.Open())
            {
                var row = db.QuerySingle("usp_ProrateFinalBill", "@AccountId", accountId,
                    "@CycleStart", Utils.StartOfMonth(moveOut), "@MoveOut", moveOut,
                    "@MonthlyCustomerCharge", AppSettings.Get("Rates:CustomerCharge:1"),
                    "@UsageKwh", 0m, "@RatePerKwh", 0m, "@DepositHeld", 0m, "@Credits", 0m);
                EstimateLabel.Text = "Your estimated final bill is " + Utils.FormatMoney(Convert.ToDecimal(row["Total"]));
            }
        }
    }
}
