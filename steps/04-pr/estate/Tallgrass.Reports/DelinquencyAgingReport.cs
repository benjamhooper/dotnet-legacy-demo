using System;
using System.Text;
using Tallgrass.Billing;
using Tallgrass.Common;

namespace Tallgrass.Reports
{
    /// <summary>New report for Collections. PR #4127, "small change, just a report".</summary>
    public class DelinquencyAgingReport
    {
        public string Build()
        {
            var asOf = DateTime.Now.Date;
            var threshold = AppSettings.GetInt("Reports:AgingThresholdDays", 30);
            var billing = new BillingManager();
            var sb = new StringBuilder();
            sb.AppendLine("Delinquency aging as of " + asOf.ToString(Constants.DATE_FORMAT));
            for (var bucket = threshold; bucket <= 120; bucket += 30)
            {
                sb.AppendLine(bucket + "+ days: see rpt_DelinquencyByTerritory");
            }
            return sb.ToString();
        }
    }
}
