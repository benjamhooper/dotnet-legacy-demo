using System;
using System.Collections.Generic;
using Tallgrass.Common;
using Tallgrass.Data;

namespace Tallgrass.Business
{
    public class CustomerService
    {
        private static CustomerService _instance;
        public static CustomerService Instance
        {
            get { return _instance ?? (_instance = new CustomerService()); }
        }

        public Account Find(int accountId)
        {
            using (var db = Db.Open())
            {
                return AccountRepository.GetAccount(db, accountId);
            }
        }

        public void Notify(Account account, string template)
        {
            if (AppSettings.GetBool("Notifications:Disabled")) return;
            AuditLog.Write("Customer", "Notify " + account.AccountNumber + " " + template + " at " + DateTime.Now);
        }
    }
}
