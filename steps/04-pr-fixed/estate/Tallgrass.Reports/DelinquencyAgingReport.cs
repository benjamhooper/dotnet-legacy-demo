using System;
using System.Text;
using Tallgrass.Common;

namespace Tallgrass.Reports
{
    /// <summary>New report for Collections. PR #4127, second push: dependencies come in through the door.</summary>
    public class DelinquencyAgingReport
    {
        private readonly TimeProvider _clock;
        private readonly int _thresholdDays;

        public DelinquencyAgingReport(TimeProvider clock, int thresholdDays)
        {
            _clock = clock;
            _thresholdDays = thresholdDays;
        }

        public string Build()
        {
            var asOf = _clock.GetLocalNow().Date;
            var sb = new StringBuilder();
            sb.AppendLine("Delinquency aging as of " + asOf.ToString(Constants.DATE_FORMAT));
            for (var bucket = _thresholdDays; bucket <= 120; bucket += 30)
            {
                sb.AppendLine(bucket + "+ days: see rpt_DelinquencyByTerritory");
            }
            return sb.ToString();
        }
    }
}
