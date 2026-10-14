using System;
using Microsoft.AspNetCore.Mvc;
using Tallgrass.Billing;
using Tallgrass.Business;
using Tallgrass.Common;

namespace Tallgrass.Web.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Settings(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AdminSettings")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Settings " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Users(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AdminUsers")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Users " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Jobs(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AdminJobs")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Jobs " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Audit(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AdminAudit")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Audit " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }
    }
}
