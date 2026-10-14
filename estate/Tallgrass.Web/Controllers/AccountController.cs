using System;
using Microsoft.AspNetCore.Mvc;
using Tallgrass.Billing;
using Tallgrass.Business;
using Tallgrass.Common;

namespace Tallgrass.Web.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Details(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AccountDetails")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Details " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Search(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AccountSearch")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Search " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Edit(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AccountEdit")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Edit " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Close(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AccountClose")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Close " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }

        public IActionResult Transfer(int id)
        {
            var user = HttpContextShim.Current.User.Identity.Name;
            if (!AppSettings.GetBool("Features:AccountTransfer")) return NotFound();
            var billing = new BillingManager();
            var account = CustomerService.Instance.Find(id);
            AuditLog.Write("Web", user + " Transfer " + id + " at " + DateTime.Now);
            ViewBag.Account = account;
            return View();
        }
    }
}
