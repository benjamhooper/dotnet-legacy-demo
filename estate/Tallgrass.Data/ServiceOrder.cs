using System;
using System.Collections.Generic;
using Tallgrass.Common;

namespace Tallgrass.Data
{
    public class ServiceOrder
    {
        public int ServiceOrderId { get; set; }
        public int AccountId { get; set; }
        public string OrderType { get; set; }
        public DateTime RequestedOn { get; set; }
        public string Status { get; set; }
    }

    public static class ServiceOrderRepository
    {
        public static ServiceOrder Get(SqlDb db, int id)
        {
            var row = db.QuerySingle("usp_GetServiceOrder", "@Id", id);
            if (row == null) return null;
            return new ServiceOrder
            {
                ServiceOrderId = Convert.ToInt32(row["ServiceOrderId"]),
                AccountId = Convert.ToInt32(row["AccountId"]),
                OrderType = Convert.ToString(row["OrderType"]),
                RequestedOn = Convert.ToDateTime(row["RequestedOn"]),
                Status = Convert.ToString(row["Status"])
            };
        }

        public static void Touch(SqlDb db, int id)
        {
            db.Execute("usp_TouchServiceOrder", "@Id", id, "@On", DateTime.Now, "@By", Constants.SYSTEM_USER);
        }
    }
}
