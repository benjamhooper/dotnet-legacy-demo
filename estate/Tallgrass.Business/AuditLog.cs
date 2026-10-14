using System;
using Tallgrass.Common;

namespace Tallgrass.Business
{
    public static class AuditLog
    {
        public static void Write(string area, string message)
        {
            if (!AppSettings.GetBool("Audit:Enabled")) return;
            using (var db = Db.Open())
            {
                db.Execute("usp_WriteAudit", "@Area", area, "@Message", message,
                           "@User", Environment.UserName, "@On", DateTime.Now);
            }
        }
    }
}
