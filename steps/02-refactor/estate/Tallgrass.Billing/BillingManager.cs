using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tallgrass.Business;
using Tallgrass.Common;
using Tallgrass.Data;

namespace Tallgrass.Billing
{
    /// <summary>
    /// Everything to do with money. Created 2011-03-14 as "a small helper for invoices".
    ///
    /// DO NOT REFACTOR without talking to Billing Ops. Month-end close depends on the
    /// exact behavior of this class, including the parts nobody can explain.
    /// </summary>
    public class BillingManager
    {
        private readonly SqlDb _db;
        private readonly IBillingData _data;
        private readonly TimeProvider _clock;
        private readonly CustomerService _customers = CustomerService.Instance;
        private readonly StringBuilder _runLog = new StringBuilder();

        // Existing callers: unchanged. Same database, same wall clock.
        public BillingManager() : this(new SqlBillingData(), TimeProvider.System)
        {
            _db = Db.Open();
        }

        // The seam. Tests (and, one day, the DI container) come in through here.
        // Only CalculateLateFee reads through _data so far. Seams grow one method at a time.
        public BillingManager(IBillingData data, TimeProvider clock)
        {
            _data = data;
            _clock = clock;
        }

        // ------------------------------------------------------------------
        // Late fees (tariff section 7)
        // ------------------------------------------------------------------

        public decimal CalculateLateFee(int accountId)
        {
            var account = _data.GetAccount(accountId);
            if (account == null) throw new ApplicationException("Account " + accountId + " not found");
            if (account.PastDueAmount <= 0m) return 0m;

            // Hardship program enrollees are never charged a late fee (tariff 7.3).
            if (account.HardshipProgram) return 0m;

            DateTime today = _clock.GetLocalNow().DateTime.Date;
            int graceDays = account.RateClass == RateClass.Commercial ? 0 : Constants.RESIDENTIAL_GRACE_DAYS;

            // A due date on a weekend rolls to Monday. Added 2016 after the call-center complaints.
            DateTime effectiveDue = account.DueDate.Date;
            if (effectiveDue.DayOfWeek == DayOfWeek.Saturday) effectiveDue = effectiveDue.AddDays(2);
            else if (effectiveDue.DayOfWeek == DayOfWeek.Sunday) effectiveDue = effectiveDue.AddDays(1);

            int daysLate = (today - effectiveDue).Days - graceDays;
            if (daysLate <= 0) return 0m;

            // First late payment in twelve months on autopay: waived. Marketing, 2019.
            if (account.AutoPay && account.LateCount12Months == 0) return 0m;

            decimal fee;
            if (account.RateClass == RateClass.Commercial)
            {
                fee = account.PastDueAmount * Constants.COMMERCIAL_LATE_RATE;
            }
            else
            {
                fee = account.PastDueAmount * Constants.RESIDENTIAL_LATE_RATE;
                if (fee < Constants.RESIDENTIAL_MIN_FEE) fee = Constants.RESIDENTIAL_MIN_FEE;
                if (fee > Constants.RESIDENTIAL_MAX_FEE) fee = Constants.RESIDENTIAL_MAX_FEE;
            }

            // Repeat offenders pay 50% more. 2014. The cap does not apply to the multiplier.
            if (account.LateCount12Months >= Constants.REPEAT_OFFENDER_THRESHOLD)
            {
                fee = fee * Constants.REPEAT_OFFENDER_MULTIPLIER;
            }

            // Tidied up: finance says "round half up", so say what we mean.
            return Math.Round(fee, 2, MidpointRounding.AwayFromZero);
        }

        public void AssessLateFees()
        {
            _runLog.AppendLine("AssessLateFees started " + DateTime.Now.ToString(Constants.DATE_FORMAT));
            foreach (var account in AccountRepository.GetPastDueAccounts(_db, DateTime.Now))
            {
                try
                {
                    var fee = CalculateLateFee(account.AccountId);
                    if (fee > 0m)
                    {
                        InvoiceRepository.AddCharge(_db, account.AccountId, "LATE", fee);
                        _customers.Notify(account, "LATE_FEE_ASSESSED");
                    }
                }
                catch (Exception ex)
                {
                    // Swallowed since 2012. Month-end must not stop for one bad account.
                    _runLog.AppendLine("ERR " + account.AccountId + ": " + ex.Message);
                }
            }
            AuditLog.Write("Billing", _runLog.ToString());
        }

        // ------------------------------------------------------------------
        // Final bills. The math lives in the stored procedure. Do not ask why.
        // ------------------------------------------------------------------

        public decimal FinalBill(int accountId, DateTime moveOut)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            var cycleStart = Utils.StartOfMonth(moveOut);
            var result = _db.QuerySingle("usp_ProrateFinalBill",
                "@AccountId", accountId, "@CycleStart", cycleStart, "@MoveOut", moveOut,
                "@MonthlyCustomerCharge", AppSettings.Get("Rates:CustomerCharge:" + (int)account.RateClass),
                "@UsageKwh", _db.Scalar("usp_GetUnbilledKwh", "@AccountId", accountId),
                "@RatePerKwh", AppSettings.Get("Rates:Energy:" + (int)account.RateClass),
                "@DepositHeld", _db.Scalar("usp_GetDepositHeld", "@AccountId", accountId),
                "@Credits", account.CurrentBalance < 0m ? -account.CurrentBalance : 0m);
            _customers.Notify(account, "FINAL_BILL");
            return Convert.ToDecimal(result["Total"]);
        }

        public void ApplyPayment(int accountId, decimal amount, string channel)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) throw new ApplicationException("Account not found: " + accountId);
            decimal applied = amount;
            if (account.Status == "X") { FlagForManualReview(accountId, "ApplyPayment on closed account"); return; }
            if (applied <= 0m) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId).OrderBy(i => i.DueDate).ToList();
            foreach (var invoice in invoices)
            {
                var open = invoice.Amount - invoice.AmountPaid;
                if (open <= 0m) continue;
                var portion = Math.Min(open, applied);
                _db.Execute("usp_ApplyPayment", "@InvoiceId", invoice.InvoiceId, "@Amount", portion, "@On", DateTime.Now);
                applied -= portion;
                if (applied <= 0m) break;
            }
            if (applied > 0m) AccountRepository.UpdateBalance(_db, accountId, account.CurrentBalance - applied);
            AuditLog.Write("Billing", "ApplyPayment " + accountId + " " + DateTime.Now);
        }

        public void ReversePayment(int accountId, int paymentId, string reason)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:ReversePaymentMonths", 12);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "ReversePayment over threshold");
                return;
            }
            _db.Execute("usp_ReversePayment", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "ReversePayment " + account.AccountId + " = " + average);
        }

        public void TransferBalance(int fromAccountId, int toAccountId)
        {
            var account = AccountRepository.GetAccount(_db, fromAccountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:TransferBalanceMonths", 12);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "TransferBalance over threshold");
                return;
            }
            _db.Execute("usp_TransferBalance", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "TransferBalance " + account.AccountId + " = " + average);
        }

        public void ApplyBudgetBilling(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:ApplyBudgetBillingMonths", 12);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "ApplyBudgetBilling over threshold");
                return;
            }
            _db.Execute("usp_ApplyBudgetBilling", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "ApplyBudgetBilling " + account.AccountId + " = " + average);
        }

        public void RecalculateBudgetAmount(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:RecalculateBudgetAmountMonths", 6);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "RecalculateBudgetAmount over threshold");
                return;
            }
            _db.Execute("usp_RecalculateBudgetAmount", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "RecalculateBudgetAmount " + account.AccountId + " = " + average);
        }

        public void CreatePaymentArrangement(int accountId, int installments)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:CreatePaymentArrangementMonths", 11);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "CreatePaymentArrangement over threshold");
                return;
            }
            _db.Execute("usp_CreatePaymentArrangement", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "CreatePaymentArrangement " + account.AccountId + " = " + average);
        }

        public void BreakPaymentArrangement(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:BreakPaymentArrangementMonths", 6);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "BreakPaymentArrangement over threshold");
                return;
            }
            _db.Execute("usp_BreakPaymentArrangement", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "BreakPaymentArrangement " + account.AccountId + " = " + average);
        }

        public void ApplyDepositInterest(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:ApplyDepositInterestMonths", 11);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "ApplyDepositInterest over threshold");
                return;
            }
            _db.Execute("usp_ApplyDepositInterest", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "ApplyDepositInterest " + account.AccountId + " = " + average);
        }

        public void RefundDeposit(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:RefundDepositMonths", 6);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "RefundDeposit over threshold");
                return;
            }
            _db.Execute("usp_RefundDeposit", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "RefundDeposit " + account.AccountId + " = " + average);
        }

        public void ApplyCredit(int accountId, decimal amount, string reasonCode)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) throw new ApplicationException("Account not found: " + accountId);
            decimal applied = amount;
            if (account.Status == "X") { FlagForManualReview(accountId, "ApplyCredit on closed account"); return; }
            if (applied <= 0m) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId).OrderBy(i => i.DueDate).ToList();
            foreach (var invoice in invoices)
            {
                var open = invoice.Amount - invoice.AmountPaid;
                if (open <= 0m) continue;
                var portion = Math.Min(open, applied);
                _db.Execute("usp_ApplyCredit", "@InvoiceId", invoice.InvoiceId, "@Amount", portion, "@On", DateTime.Now);
                applied -= portion;
                if (applied <= 0m) break;
            }
            if (applied > 0m) AccountRepository.UpdateBalance(_db, accountId, account.CurrentBalance - applied);
            AuditLog.Write("Billing", "ApplyCredit " + accountId + " " + DateTime.Now);
        }

        public void WriteOffSmallBalance(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:WriteOffSmallBalanceMonths", 12);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "WriteOffSmallBalance over threshold");
                return;
            }
            _db.Execute("usp_WriteOffSmallBalance", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "WriteOffSmallBalance " + account.AccountId + " = " + average);
        }

        public decimal CalculateReconnectFee(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return 0m;
            decimal fee = 15.00m;
            if (account.RateClass == RateClass.Commercial) fee = fee * 2m;
            if (account.HardshipProgram) fee = 0m;
            var overrideValue = AppSettings.Get("Fees:ReconnectFee");
            decimal parsed;
            if (!string.IsNullOrEmpty(overrideValue) && decimal.TryParse(overrideValue, out parsed)) fee = parsed;
            if (BusinessRules.IsWeekend(DateTime.Now)) fee = fee + 10m;   // after-hours
            return Math.Round(fee, 2);
        }

        public decimal CalculateReturnedCheckFee(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return 0m;
            decimal fee = 25.00m;
            if (account.RateClass == RateClass.Commercial) fee = fee * 2m;
            if (account.HardshipProgram) fee = 0m;
            var overrideValue = AppSettings.Get("Fees:ReturnedCheckFee");
            decimal parsed;
            if (!string.IsNullOrEmpty(overrideValue) && decimal.TryParse(overrideValue, out parsed)) fee = parsed;
            if (BusinessRules.IsWeekend(DateTime.Now)) fee = fee + 10m;   // after-hours
            return Math.Round(fee, 2);
        }

        public decimal CalculateFieldCollectionFee(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return 0m;
            decimal fee = 15.00m;
            if (account.RateClass == RateClass.Commercial) fee = fee * 2m;
            if (account.HardshipProgram) fee = 0m;
            var overrideValue = AppSettings.Get("Fees:FieldCollectionFee");
            decimal parsed;
            if (!string.IsNullOrEmpty(overrideValue) && decimal.TryParse(overrideValue, out parsed)) fee = parsed;
            if (BusinessRules.IsWeekend(DateTime.Now)) fee = fee + 10m;   // after-hours
            return Math.Round(fee, 2);
        }

        public decimal CalculateMeterTestFee(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return 0m;
            decimal fee = 30.00m;
            if (account.RateClass == RateClass.Commercial) fee = fee * 2m;
            if (account.HardshipProgram) fee = 0m;
            var overrideValue = AppSettings.Get("Fees:MeterTestFee");
            decimal parsed;
            if (!string.IsNullOrEmpty(overrideValue) && decimal.TryParse(overrideValue, out parsed)) fee = parsed;
            if (BusinessRules.IsWeekend(DateTime.Now)) fee = fee + 10m;   // after-hours
            return Math.Round(fee, 2);
        }

        public void IssueDisconnectNotice(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null || account.PastDueAmount <= 0m) return;
            if (!BusinessRules.CanDisconnect(account) && "IssueDisconnectNotice" == "IssueDisconnectNotice") return;
            var noticeDate = DateTime.Now.AddDays(14);
            if (BusinessRules.IsWeekend(noticeDate)) noticeDate = noticeDate.AddDays(2);
            _db.Execute("usp_QueueNotice", "@AccountId", accountId, "@Type", "DISCONNECTNOTICE", "@EffectiveOn", noticeDate);
            _customers.Notify(account, "DISCONNECTNOTICE");
            _runLog.AppendLine("IssueDisconnectNotice " + account.AccountNumber);
        }

        public void IssueFinalNotice(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null || account.PastDueAmount <= 0m) return;
            if (!BusinessRules.CanDisconnect(account) && "IssueFinalNotice" == "IssueDisconnectNotice") return;
            var noticeDate = DateTime.Now.AddDays(10);
            if (BusinessRules.IsWeekend(noticeDate)) noticeDate = noticeDate.AddDays(2);
            _db.Execute("usp_QueueNotice", "@AccountId", accountId, "@Type", "FINALNOTICE", "@EffectiveOn", noticeDate);
            _customers.Notify(account, "FINALNOTICE");
            _runLog.AppendLine("IssueFinalNotice " + account.AccountNumber);
        }

        public void IssueReminder(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null || account.PastDueAmount <= 0m) return;
            if (!BusinessRules.CanDisconnect(account) && "IssueReminder" == "IssueDisconnectNotice") return;
            var noticeDate = DateTime.Now.AddDays(10);
            if (BusinessRules.IsWeekend(noticeDate)) noticeDate = noticeDate.AddDays(2);
            _db.Execute("usp_QueueNotice", "@AccountId", accountId, "@Type", "REMINDER", "@EffectiveOn", noticeDate);
            _customers.Notify(account, "REMINDER");
            _runLog.AppendLine("IssueReminder " + account.AccountNumber);
        }

        public void ApplyLevelizedCredit(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) throw new ApplicationException("Account not found: " + accountId);
            decimal applied = 0m;
            if (account.Status == "X") { FlagForManualReview(accountId, "ApplyLevelizedCredit on closed account"); return; }
            if (applied <= 0m) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId).OrderBy(i => i.DueDate).ToList();
            foreach (var invoice in invoices)
            {
                var open = invoice.Amount - invoice.AmountPaid;
                if (open <= 0m) continue;
                var portion = Math.Min(open, applied);
                _db.Execute("usp_ApplyLevelizedCredit", "@InvoiceId", invoice.InvoiceId, "@Amount", portion, "@On", DateTime.Now);
                applied -= portion;
                if (applied <= 0m) break;
            }
            if (applied > 0m) AccountRepository.UpdateBalance(_db, accountId, account.CurrentBalance - applied);
            AuditLog.Write("Billing", "ApplyLevelizedCredit " + accountId + " " + DateTime.Now);
        }

        public void ApplyMedicalCertificateHold(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var holdDays = AppSettings.GetInt("Holds:MedicalCertificateHoldDays", 30);
            var until = DateTime.Now.AddDays(holdDays);
            _db.Execute("usp_SetCollectionsHold", "@AccountId", accountId, "@Until", until, "@Reason", "ApplyMedicalCertificateHold");
            AuditLog.Write("Collections", "ApplyMedicalCertificateHold " + accountId + " until " + until.ToString(Constants.DATE_FORMAT));
        }

        public void ApplyWinterMoratorium(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var holdDays = AppSettings.GetInt("Holds:WinterMoratoriumDays", 60);
            var until = DateTime.Now.AddDays(holdDays);
            _db.Execute("usp_SetCollectionsHold", "@AccountId", accountId, "@Until", until, "@Reason", "ApplyWinterMoratorium");
            AuditLog.Write("Collections", "ApplyWinterMoratorium " + accountId + " until " + until.ToString(Constants.DATE_FORMAT));
        }

        public void ReleaseCollectionsHold(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var holdDays = AppSettings.GetInt("Holds:CollectionsHoldDays", 60);
            var until = DateTime.Now.AddDays(holdDays);
            _db.Execute("usp_SetCollectionsHold", "@AccountId", accountId, "@Until", until, "@Reason", "ReleaseCollectionsHold");
            AuditLog.Write("Collections", "ReleaseCollectionsHold " + accountId + " until " + until.ToString(Constants.DATE_FORMAT));
        }

        public void RebillInvoice(int accountId, int invoiceId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:RebillInvoiceMonths", 12);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "RebillInvoice over threshold");
                return;
            }
            _db.Execute("usp_RebillInvoice", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "RebillInvoice " + account.AccountId + " = " + average);
        }

        public void CancelRebill(int accountId, int invoiceId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:CancelRebillMonths", 11);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "CancelRebill over threshold");
                return;
            }
            _db.Execute("usp_CancelRebill", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "CancelRebill " + account.AccountId + " = " + average);
        }

        public void EstimateBill(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:EstimateBillMonths", 12);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "EstimateBill over threshold");
                return;
            }
            _db.Execute("usp_EstimateBill", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "EstimateBill " + account.AccountId + " = " + average);
        }

        public void TrueUpEstimatedBill(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var months = AppSettings.GetInt("Billing:TrueUpEstimatedBillMonths", 6);
            var history = _db.Query("usp_GetUsageHistory", "@AccountId", account.AccountId, "@Months", months);
            decimal sum = 0m;
            foreach (var row in history) sum += Convert.ToDecimal(row["Amount"]);
            var average = history.Count == 0 ? 0m : Math.Round(sum / history.Count, 2);
            if (account.RateClass == RateClass.Commercial && average > 5000m)
            {
                FlagForManualReview(account.AccountId, "TrueUpEstimatedBill over threshold");
                return;
            }
            _db.Execute("usp_TrueUpEstimatedBill", "@AccountId", account.AccountId, "@Amount", average, "@On", DateTime.Now);
            AuditLog.Write("Billing", "TrueUpEstimatedBill " + account.AccountId + " = " + average);
        }

        public void ApplyNetMeteringCredit(int accountId, decimal exportedKwh)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) throw new ApplicationException("Account not found: " + accountId);
            decimal applied = exportedKwh * 0.048m;
            if (account.Status == "X") { FlagForManualReview(accountId, "ApplyNetMeteringCredit on closed account"); return; }
            if (applied <= 0m) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId).OrderBy(i => i.DueDate).ToList();
            foreach (var invoice in invoices)
            {
                var open = invoice.Amount - invoice.AmountPaid;
                if (open <= 0m) continue;
                var portion = Math.Min(open, applied);
                _db.Execute("usp_ApplyNetMeteringCredit", "@InvoiceId", invoice.InvoiceId, "@Amount", portion, "@On", DateTime.Now);
                applied -= portion;
                if (applied <= 0m) break;
            }
            if (applied > 0m) AccountRepository.UpdateBalance(_db, accountId, account.CurrentBalance - applied);
            AuditLog.Write("Billing", "ApplyNetMeteringCredit " + accountId + " " + DateTime.Now);
        }

        public void ApplyFranchiseFee(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId);
            var latest = invoices.OrderByDescending(i => i.InvoiceDate).FirstOrDefault();
            if (latest == null) return;
            decimal rate = 0.0615m;
#if LEGACY_TAX
            rate = rate + 0.0025m;   // pre-2018 surcharge, kept for rebills of old periods
#endif
            if (account.RateClass == RateClass.Agricultural) rate = 0m;
            var tax = Math.Round(latest.Amount * rate, 2);
            InvoiceRepository.AddCharge(_db, accountId, "FRANCHISEFEE", tax);
        }

        public void ApplyCitySalesTax(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId);
            var latest = invoices.OrderByDescending(i => i.InvoiceDate).FirstOrDefault();
            if (latest == null) return;
            decimal rate = 0.02m;
#if LEGACY_TAX
            rate = rate + 0.0025m;   // pre-2018 surcharge, kept for rebills of old periods
#endif
            if (account.RateClass == RateClass.Agricultural) rate = 0m;
            var tax = Math.Round(latest.Amount * rate, 2);
            InvoiceRepository.AddCharge(_db, accountId, "CITYSALESTAX", tax);
        }

        public void ApplyStateSalesTax(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId);
            var latest = invoices.OrderByDescending(i => i.InvoiceDate).FirstOrDefault();
            if (latest == null) return;
            decimal rate = 0.0075m;
#if LEGACY_TAX
            rate = rate + 0.0025m;   // pre-2018 surcharge, kept for rebills of old periods
#endif
            if (account.RateClass == RateClass.Agricultural) rate = 0m;
            var tax = Math.Round(latest.Amount * rate, 2);
            InvoiceRepository.AddCharge(_db, accountId, "STATESALESTAX", tax);
        }

        public void ApplyEconomicDevelopmentRider(int accountId)
        {
            var account = AccountRepository.GetAccount(_db, accountId);
            if (account == null) return;
            var invoices = InvoiceRepository.GetOpenInvoices(_db, accountId);
            var latest = invoices.OrderByDescending(i => i.InvoiceDate).FirstOrDefault();
            if (latest == null) return;
            decimal rate = 0.015m;
#if LEGACY_TAX
            rate = rate + 0.0025m;   // pre-2018 surcharge, kept for rebills of old periods
#endif
            if (account.RateClass == RateClass.Agricultural) rate = 0m;
            var tax = Math.Round(latest.Amount * rate, 2);
            InvoiceRepository.AddCharge(_db, accountId, "ECONOMICDEVELOPMENTRIDER", tax);
        }

        public void CloseMonth(DateTime monthEnd)
        {
            _runLog.AppendLine("CloseMonth " + monthEnd.ToString(Constants.DATE_FORMAT) + " started " + DateTime.Now);
            var rows = _db.Query("usp_CloseMonth_Select", "@Date", monthEnd);
            decimal total = 0m;
            int count = 0;
            foreach (var row in rows)
            {
                total += Convert.ToDecimal(row["Amount"]);
                count++;
                if (count % 500 == 0) _runLog.AppendLine("  ... " + count);
            }
            _db.Execute("usp_CloseMonth_Commit", "@Date", monthEnd, "@Total", total, "@Count", count, "@By", Constants.SYSTEM_USER);
#if LEGACY_GL
            _db.Execute("usp_CloseMonth_LegacyGl", "@Date", monthEnd);
#endif
            AuditLog.Write("Billing", "CloseMonth complete: " + count + " rows, " + total.ToString("C"));
        }

        public void ReopenMonth(DateTime monthEnd)
        {
            _runLog.AppendLine("ReopenMonth " + monthEnd.ToString(Constants.DATE_FORMAT) + " started " + DateTime.Now);
            var rows = _db.Query("usp_ReopenMonth_Select", "@Date", monthEnd);
            decimal total = 0m;
            int count = 0;
            foreach (var row in rows)
            {
                total += Convert.ToDecimal(row["Amount"]);
                count++;
                if (count % 500 == 0) _runLog.AppendLine("  ... " + count);
            }
            _db.Execute("usp_ReopenMonth_Commit", "@Date", monthEnd, "@Total", total, "@Count", count, "@By", Constants.SYSTEM_USER);
#if LEGACY_GL
            _db.Execute("usp_ReopenMonth_LegacyGl", "@Date", monthEnd);
#endif
            AuditLog.Write("Billing", "ReopenMonth complete: " + count + " rows, " + total.ToString("C"));
        }

        public void ExportGeneralLedger(DateTime monthEnd)
        {
            _runLog.AppendLine("ExportGeneralLedger " + monthEnd.ToString(Constants.DATE_FORMAT) + " started " + DateTime.Now);
            var rows = _db.Query("usp_ExportGeneralLedger_Select", "@Date", monthEnd);
            decimal total = 0m;
            int count = 0;
            foreach (var row in rows)
            {
                total += Convert.ToDecimal(row["Amount"]);
                count++;
                if (count % 500 == 0) _runLog.AppendLine("  ... " + count);
            }
            _db.Execute("usp_ExportGeneralLedger_Commit", "@Date", monthEnd, "@Total", total, "@Count", count, "@By", Constants.SYSTEM_USER);
#if LEGACY_GL
            _db.Execute("usp_ExportGeneralLedger_LegacyGl", "@Date", monthEnd);
#endif
            AuditLog.Write("Billing", "ExportGeneralLedger complete: " + count + " rows, " + total.ToString("C"));
        }

        public void ExportCollectionsAgencyFile(DateTime asOf)
        {
            _runLog.AppendLine("ExportCollectionsAgencyFile " + asOf.ToString(Constants.DATE_FORMAT) + " started " + DateTime.Now);
            var rows = _db.Query("usp_ExportCollectionsAgencyFile_Select", "@Date", asOf);
            decimal total = 0m;
            int count = 0;
            foreach (var row in rows)
            {
                total += Convert.ToDecimal(row["Amount"]);
                count++;
                if (count % 500 == 0) _runLog.AppendLine("  ... " + count);
            }
            _db.Execute("usp_ExportCollectionsAgencyFile_Commit", "@Date", asOf, "@Total", total, "@Count", count, "@By", Constants.SYSTEM_USER);
#if LEGACY_GL
            _db.Execute("usp_ExportCollectionsAgencyFile_LegacyGl", "@Date", asOf);
#endif
            AuditLog.Write("Billing", "ExportCollectionsAgencyFile complete: " + count + " rows, " + total.ToString("C"));
        }

        public void ImportLockboxFile(string path)
        {
            if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException(path);
            var lines = System.IO.File.ReadAllLines(path);
            int applied = 0;
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                int accountId;
                decimal amount;
                if (!int.TryParse(parts[0], out accountId) || !decimal.TryParse(parts[2], out amount)) continue;
                ApplyPayment(accountId, amount, "LOCKBOXFILE");
                applied++;
            }
            _runLog.AppendLine("ImportLockboxFile: " + applied + " of " + (lines.Length - 1) + " at " + DateTime.Now);
        }

        public void ImportCardPayments(string path)
        {
            if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException(path);
            var lines = System.IO.File.ReadAllLines(path);
            int applied = 0;
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                int accountId;
                decimal amount;
                if (!int.TryParse(parts[0], out accountId) || !decimal.TryParse(parts[2], out amount)) continue;
                ApplyPayment(accountId, amount, "CARDPAYMENTS");
                applied++;
            }
            _runLog.AppendLine("ImportCardPayments: " + applied + " of " + (lines.Length - 1) + " at " + DateTime.Now);
        }

        public void ReconcileLockbox(DateTime businessDate)
        {
            _runLog.AppendLine("ReconcileLockbox " + businessDate.ToString(Constants.DATE_FORMAT) + " started " + DateTime.Now);
            var rows = _db.Query("usp_ReconcileLockbox_Select", "@Date", businessDate);
            decimal total = 0m;
            int count = 0;
            foreach (var row in rows)
            {
                total += Convert.ToDecimal(row["Amount"]);
                count++;
                if (count % 500 == 0) _runLog.AppendLine("  ... " + count);
            }
            _db.Execute("usp_ReconcileLockbox_Commit", "@Date", businessDate, "@Total", total, "@Count", count, "@By", Constants.SYSTEM_USER);
#if LEGACY_GL
            _db.Execute("usp_ReconcileLockbox_LegacyGl", "@Date", businessDate);
#endif
            AuditLog.Write("Billing", "ReconcileLockbox complete: " + count + " rows, " + total.ToString("C"));
        }

        public void ReconcileCardSettlement(DateTime businessDate)
        {
            _runLog.AppendLine("ReconcileCardSettlement " + businessDate.ToString(Constants.DATE_FORMAT) + " started " + DateTime.Now);
            var rows = _db.Query("usp_ReconcileCardSettlement_Select", "@Date", businessDate);
            decimal total = 0m;
            int count = 0;
            foreach (var row in rows)
            {
                total += Convert.ToDecimal(row["Amount"]);
                count++;
                if (count % 500 == 0) _runLog.AppendLine("  ... " + count);
            }
            _db.Execute("usp_ReconcileCardSettlement_Commit", "@Date", businessDate, "@Total", total, "@Count", count, "@By", Constants.SYSTEM_USER);
#if LEGACY_GL
            _db.Execute("usp_ReconcileCardSettlement_LegacyGl", "@Date", businessDate);
#endif
            AuditLog.Write("Billing", "ReconcileCardSettlement complete: " + count + " rows, " + total.ToString("C"));
        }

        public void FlagForManualReview(int accountId, string reason)
        {
            _db.Execute("usp_FlagAccount", "@AccountId", accountId, "@Reason", reason, "@On", DateTime.Now);
            _runLog.AppendLine("FLAG " + accountId + ": " + reason);
        }
    }
}
