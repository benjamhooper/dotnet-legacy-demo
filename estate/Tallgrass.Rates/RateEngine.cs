using System;
using Tallgrass.Common;
using Tallgrass.Data;

namespace Tallgrass.Rates
{
    public class RateEngine
    {
        public decimal EnergyCharge(RatePlan plan, decimal kwh, DateTime periodEnd)
        {
            decimal rate = plan.EnergyRate;
#if LEGACY_TAX
            if (periodEnd < new DateTime(2018, 1, 1)) rate = rate + 0.0025m;
#endif
            // Summer tier, June-September, above 1,000 kWh.
            if (periodEnd.Month >= 6 && periodEnd.Month <= 9 && kwh > 1000m)
            {
                return Math.Round(1000m * rate + (kwh - 1000m) * (rate * 1.12m), 2);
            }
            return Math.Round(kwh * rate, 2);
        }

        public RatePlan CurrentPlan(int ratePlanId)
        {
            using (var db = Db.Open())
            {
                var plan = RatePlanRepository.Get(db, ratePlanId);
                if (plan.EffectiveFrom > DateTime.Now) throw new InvalidOperationException("Rate plan not yet effective");
                return plan;
            }
        }
    }
}
