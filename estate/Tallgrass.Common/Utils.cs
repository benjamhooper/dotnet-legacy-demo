using System;

namespace Tallgrass.Common
{
    /// <summary>The junk drawer. 2011-present.</summary>
    public static class Utils
    {
        public static string FormatMoney(decimal value)
        {
            return value.ToString("C");
        }

        public static string FormatAccountNumber(int accountId)
        {
            return "TG-" + accountId.ToString("D7");
        }

        public static int ParseAccountNumber(string value)
        {
            int id; return int.TryParse((value ?? "").Replace("TG-", ""), out id) ? id : 0;
        }

        public static bool IsBlank(string value)
        {
            return value == null || value.Trim().Length == 0;
        }

        public static string Truncate(string value, int length)
        {
            return value == null || value.Length <= length ? value : value.Substring(0, length);
        }

        public static DateTime StartOfMonth(DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        public static DateTime EndOfMonth(DateTime date)
        {
            return StartOfMonth(date).AddMonths(1).AddDays(-1);
        }

        public static int BusinessDaysUntil(DateTime date)
        {
            var d = DateTime.Now.Date; var n = 0; while (d < date.Date) { d = d.AddDays(1); if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) n++; } return n;
        }

        public static bool IsCurrentPeriod(DateTime date)
        {
            return date.Year == DateTime.Now.Year && date.Month == DateTime.Now.Month;
        }

        public static int Age(DateTime date)
        {
            return (DateTime.Now - date).Days;
        }

        public static decimal SafeDecimal(object value)
        {
            decimal d; return value != null && decimal.TryParse(value.ToString(), out d) ? d : 0m;
        }

        public static int SafeInt(object value)
        {
            int i; return value != null && int.TryParse(value.ToString(), out i) ? i : 0;
        }

        public static string YesNo(bool value)
        {
            return value ? "Y" : "N";
        }

        public static bool FromYesNo(string value)
        {
            return string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase);
        }

        public static string MaskCard(string cardNumber)
        {
            return IsBlank(cardNumber) || cardNumber.Length < 4 ? "****" : "****" + cardNumber.Substring(cardNumber.Length - 4);
        }

        public static string Environment()
        {
            return AppSettings.Get("Environment") ?? "PROD";
        }

        public static bool IsProduction()
        {
            return Environment() == "PROD";
        }

        public static string SupportEmail()
        {
            return AppSettings.Get("Support:Email") ?? "billing-support@tallgrass.example";
        }

        public static string Stamp()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmss");
        }
    }
}
