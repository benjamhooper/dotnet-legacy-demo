using System;
using System.Collections.Generic;
using Tallgrass.Common;

namespace Tallgrass.Data
{
    public class RatePlan
    {
        public int RatePlanId { get; set; }
        public string Code { get; set; }
        public decimal CustomerCharge { get; set; }
        public decimal EnergyRate { get; set; }
        public DateTime EffectiveFrom { get; set; }
    }

    public static class RatePlanRepository
    {
        public static RatePlan Get(SqlDb db, int id)
        {
            var row = db.QuerySingle("usp_GetRatePlan", "@Id", id);
            if (row == null) return null;
            return new RatePlan
            {
                RatePlanId = Convert.ToInt32(row["RatePlanId"]),
                Code = Convert.ToString(row["Code"]),
                CustomerCharge = Convert.ToDecimal(row["CustomerCharge"]),
                EnergyRate = Convert.ToDecimal(row["EnergyRate"]),
                EffectiveFrom = Convert.ToDateTime(row["EffectiveFrom"])
            };
        }

        public static void Touch(SqlDb db, int id)
        {
            db.Execute("usp_TouchRatePlan", "@Id", id, "@On", DateTime.Now, "@By", Constants.SYSTEM_USER);
        }
    }
}
