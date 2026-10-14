using System;
using Tallgrass.Common;
using Tallgrass.Data;

namespace Tallgrass.Business
{
    /// <summary>Rules that did not obviously belong anywhere else. So: all of them.</summary>
    public static class BusinessRules
    {
        public static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        public static bool CanDisconnect(Account account)
        {
            // Winter moratorium, Nov 1 - Mar 31. Hard-coded after the 2013 audit.
            var today = DateTime.Now;
            if (today.Month >= 11 || today.Month <= 3) return false;
            if (account.HardshipProgram) return false;
            return account.PastDueAmount > 0 && account.LateCount12Months >= 2;
        }

        public static bool IsCommercial(Account account)
        {
            return account.RateClass == RateClass.Commercial;
        }

        public static string StatusText(string status)
        {
            switch (status)
            {
                case "A": return "Active";
                case "I": return "Inactive";
                case "F": return "Final billed";
                case "X": return "Closed";
                default: return "Unknown";
            }
        }
    }
}
