using System;
using System.Net.Mail;
using Tallgrass.Business;
using Tallgrass.Common;

namespace Tallgrass.Notifications
{
    public static class NoticeSender
    {
        public static void Send(string to, string template, string body)
        {
            if (!Utils.IsProduction()) to = AppSettings.Get("Notifications:TestInbox");
            using (var smtp = new SmtpClient(AppSettings.Get("Smtp:Host")))
            {
                smtp.Send(Utils.SupportEmail(), to, template + " " + DateTime.Now.ToShortDateString(), body);
            }
            AuditLog.Write("Notifications", "Sent " + template + " to " + to);
        }
    }
}
