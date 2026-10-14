using System;
using System.Collections.Generic;
using Tallgrass.Common;

namespace Tallgrass.Data
{
    public class Meter
    {
        public int MeterId { get; set; }
        public string SerialNumber { get; set; }
        public int PremiseId { get; set; }
        public DateTime InstalledOn { get; set; }
        public string Type { get; set; }
    }

    public static class MeterRepository
    {
        public static Meter Get(SqlDb db, int id)
        {
            var row = db.QuerySingle("usp_GetMeter", "@Id", id);
            if (row == null) return null;
            return new Meter
            {
                MeterId = Convert.ToInt32(row["MeterId"]),
                SerialNumber = Convert.ToString(row["SerialNumber"]),
                PremiseId = Convert.ToInt32(row["PremiseId"]),
                InstalledOn = Convert.ToDateTime(row["InstalledOn"]),
                Type = Convert.ToString(row["Type"])
            };
        }

        public static void Touch(SqlDb db, int id)
        {
            db.Execute("usp_TouchMeter", "@Id", id, "@On", DateTime.Now, "@By", Constants.SYSTEM_USER);
        }
    }
}
