using System;
using Microsoft.AspNetCore.Mvc;
using Tallgrass.Billing;
using Tallgrass.Business;
using Tallgrass.Common;

namespace Tallgrass.Web.Controllers
{
    public class BillingController : Controller
    {
        public IActionResult LateFees(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:BillingLateFees")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " LateFees " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Rebill(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:BillingRebill")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Rebill " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult ApplyCredit(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:BillingApplyCredit")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " ApplyCredit " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Arrangement(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:BillingArrangement")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Arrangement " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult FinalBill(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:BillingFinalBill")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " FinalBill " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }
    }
}
