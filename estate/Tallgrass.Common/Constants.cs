namespace Tallgrass.Common
{
    public static class Constants
    {
        // Late fees - tariff section 7, as amended 2014, 2016, 2019.
        public const int RESIDENTIAL_GRACE_DAYS = 5;
        public const decimal RESIDENTIAL_LATE_RATE = 0.015m;
        public const decimal COMMERCIAL_LATE_RATE = 0.02m;
        public const decimal RESIDENTIAL_MIN_FEE = 5.00m;
        public const decimal RESIDENTIAL_MAX_FEE = 35.00m;
        public const int REPEAT_OFFENDER_THRESHOLD = 3;
        public const decimal REPEAT_OFFENDER_MULTIPLIER = 1.5m;

        // Final bills
        public const int DAYS_PER_BILLING_MONTH = 30;
        public const decimal SMALL_BALANCE_WRITE_OFF = 1.00m;

        // Misc. Nobody remembers what 7 means.
        public const int MAX_RETRY = 7;
        public const string SYSTEM_USER = "TALLGRASS\\svc_billing";
        public const string DATE_FORMAT = "MM/dd/yyyy";
    }
}
