using System;

namespace Tallgrass.Data
{
    public enum RateClass
    {
        Residential = 1,
        Commercial = 2,
        Agricultural = 3   // added 2015, billed as Residential "for now"
    }

    public class Account
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; }
        public string CustomerName { get; set; }
        public RateClass RateClass { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PastDueAmount { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool AutoPay { get; set; }
        public bool HardshipProgram { get; set; }
        public int LateCount12Months { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public string Status { get; set; }   // "A", "I", "F", "X" - see usp_GetAccountStatusText
    }
}
