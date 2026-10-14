using System;
using Tallgrass.Billing;
using Tallgrass.Common;
using Tallgrass.Payments;
using Tallgrass.Reports;

namespace Tallgrass.Batch
{
    public static class NightlyJobs
    {
        public static int Main(string[] args)
        {
            var billing = new BillingManager();
            try
            {
                billing.ImportLockboxFile(AppSettings.Get("Batch:LockboxPath"));
                billing.ReconcileLockbox(DateTime.Now.Date.AddDays(-1));
                billing.AssessLateFees();
                if (DateTime.Now.Day == 1) billing.CloseMonth(DateTime.Now.Date.AddDays(-1));
                new ReportGenerator().DailyCash(DateTime.Now.Date.AddDays(-1));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("NIGHTLY FAILED " + DateTime.Now + " " + ex);
                return Constants.MAX_RETRY;   // the scheduler retries on 7. Do not ask.
            }
        }
    }
}
