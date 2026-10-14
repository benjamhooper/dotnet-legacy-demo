using System;
using System.Collections.Generic;
using Tallgrass.Common;

namespace Tallgrass.Data
{
    public class Premise
    {
        public int PremiseId { get; set; }
        public string Address1 { get; set; }
        public string City { get; set; }
        public string Zip { get; set; }
        public int ServiceTerritory { get; set; }
    }

    public static class PremiseRepository
    {
        public static Premise Get(SqlDb db, int id)
        {
            var row = db.QuerySingle("usp_GetPremise", "@Id", id);
            if (row == null) return null;
            return new Premise
            {
                PremiseId = Convert.ToInt32(row["PremiseId"]),
                Address1 = Convert.ToString(row["Address1"]),
                City = Convert.ToString(row["City"]),
                Zip = Convert.ToString(row["Zip"]),
                ServiceTerritory = Convert.ToInt32(row["ServiceTerritory"])
            };
        }

        public static void Touch(SqlDb db, int id)
        {
            db.Execute("usp_TouchPremise", "@Id", id, "@On", DateTime.Now, "@By", Constants.SYSTEM_USER);
        }
    }
}
