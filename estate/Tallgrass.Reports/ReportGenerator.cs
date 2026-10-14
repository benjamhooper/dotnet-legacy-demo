using System;
using System.IO;
using System.Text;
using Tallgrass.Business;
using Tallgrass.Common;

namespace Tallgrass.Reports
{
    /// <summary>
    /// Every report the business has ever asked for. Thirty of them, one class.
    /// Last meaningful change: 2023. Last incident: never.
    /// </summary>
    public class ReportGenerator
    {
        public string DailyCash(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DailyCash as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Channel,CustomerName,AccountNumber,Amount,RateClass,Reason");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_DailyCash", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("DailyCash", asOf, sb.ToString());
            return sb.ToString();
        }

        public string AgedReceivables(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AgedReceivables as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("CustomerName,Channel,Balance,Reason,Status,AccountNumber");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_AgedReceivables", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("AgedReceivables", asOf, sb.ToString());
            return sb.ToString();
        }

        public string DelinquencyByTerritory(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DelinquencyByTerritory as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("RateClass,Reason,Balance,AccountNumber,Channel,DueDate");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_DelinquencyByTerritory", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("DelinquencyByTerritory", asOf, sb.ToString());
            return sb.ToString();
        }

        public string WriteOffs(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WriteOffs as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Balance,Status,RateClass,DaysPastDue,Amount,Channel");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_WriteOffs", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("WriteOffs", asOf, sb.ToString());
            return sb.ToString();
        }

        public string DepositLiability(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DepositLiability as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Territory,Status,DueDate,Amount,Reason,CustomerName");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_DepositLiability", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("DepositLiability", asOf, sb.ToString());
            return sb.ToString();
        }

        public string BudgetBillingVariance(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BudgetBillingVariance as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("DueDate,CustomerName,Channel,Status,PostedOn,Territory");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_BudgetBillingVariance", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("BudgetBillingVariance", asOf, sb.ToString());
            return sb.ToString();
        }

        public string LateFeeRevenue(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LateFeeRevenue as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("PostedOn,Amount,AccountNumber,DaysPastDue,CustomerName,RateClass");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_LateFeeRevenue", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("LateFeeRevenue", asOf, sb.ToString());
            return sb.ToString();
        }

        public string HardshipEnrollment(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("HardshipEnrollment as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Amount,Channel,PostedOn,DueDate,RateClass,Balance");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_HardshipEnrollment", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("HardshipEnrollment", asOf, sb.ToString());
            return sb.ToString();
        }

        public string DisconnectsPending(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DisconnectsPending as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Channel,RateClass,Amount,CustomerName,Reason,Status");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_DisconnectsPending", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("DisconnectsPending", asOf, sb.ToString());
            return sb.ToString();
        }

        public string ReconnectsCompleted(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ReconnectsCompleted as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Amount,DaysPastDue,DueDate,Territory,PostedOn,Balance");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_ReconnectsCompleted", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("ReconnectsCompleted", asOf, sb.ToString());
            return sb.ToString();
        }

        public string RefundRegister(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RefundRegister as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Amount,Channel,CustomerName,Territory,RateClass,PostedOn");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_RefundRegister", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("RefundRegister", asOf, sb.ToString());
            return sb.ToString();
        }

        public string UnappliedCash(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("UnappliedCash as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Amount,Channel,Balance,RateClass,DueDate,Status");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_UnappliedCash", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("UnappliedCash", asOf, sb.ToString());
            return sb.ToString();
        }

        public string RateClassRevenue(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RateClassRevenue as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("AccountNumber,RateClass,Reason,DueDate,Status,Territory");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_RateClassRevenue", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("RateClassRevenue", asOf, sb.ToString());
            return sb.ToString();
        }

        public string FranchiseFeeRemittance(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("FranchiseFeeRemittance as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("PostedOn,DueDate,RateClass,DaysPastDue,Status,Channel");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_FranchiseFeeRemittance", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("FranchiseFeeRemittance", asOf, sb.ToString());
            return sb.ToString();
        }

        public string SalesTaxRemittance(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SalesTaxRemittance as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Amount,Territory,RateClass,Balance,Reason,DueDate");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_SalesTaxRemittance", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("SalesTaxRemittance", asOf, sb.ToString());
            return sb.ToString();
        }

        public string NetMeteringCredits(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NetMeteringCredits as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("PostedOn,Status,DueDate,RateClass,Territory,Amount");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_NetMeteringCredits", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("NetMeteringCredits", asOf, sb.ToString());
            return sb.ToString();
        }

        public string EstimatedBills(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EstimatedBills as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("AccountNumber,CustomerName,Territory,PostedOn,Status,Amount");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_EstimatedBills", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("EstimatedBills", asOf, sb.ToString());
            return sb.ToString();
        }

        public string RebillRegister(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RebillRegister as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Status,PostedOn,DaysPastDue,Balance,Amount,Channel");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_RebillRegister", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("RebillRegister", asOf, sb.ToString());
            return sb.ToString();
        }

        public string CollectionsAgencyPlacement(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CollectionsAgencyPlacement as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("AccountNumber,Channel,CustomerName,Balance,Amount,Status");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_CollectionsAgencyPlacement", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("CollectionsAgencyPlacement", asOf, sb.ToString());
            return sb.ToString();
        }

        public string ReturnedPayments(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ReturnedPayments as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("CustomerName,Amount,Status,Territory,DaysPastDue,AccountNumber");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_ReturnedPayments", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("ReturnedPayments", asOf, sb.ToString());
            return sb.ToString();
        }

        public string CardSettlement(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CardSettlement as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Reason,Amount,Balance,Territory,CustomerName,Status");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_CardSettlement", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("CardSettlement", asOf, sb.ToString());
            return sb.ToString();
        }

        public string LockboxExceptions(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LockboxExceptions as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Channel,Balance,PostedOn,RateClass,Territory,DaysPastDue");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_LockboxExceptions", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("LockboxExceptions", asOf, sb.ToString());
            return sb.ToString();
        }

        public string MonthEndSummary(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("MonthEndSummary as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Balance,Reason,AccountNumber,DueDate,DaysPastDue,PostedOn");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_MonthEndSummary", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("MonthEndSummary", asOf, sb.ToString());
            return sb.ToString();
        }

        public string AnnualRegulatoryFiling(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AnnualRegulatoryFiling as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("DueDate,Amount,RateClass,AccountNumber,PostedOn,Channel");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_AnnualRegulatoryFiling", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("AnnualRegulatoryFiling", asOf, sb.ToString());
            return sb.ToString();
        }

        public string CommissionQuarterly(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CommissionQuarterly as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("CustomerName,DaysPastDue,Reason,Balance,Territory,PostedOn");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_CommissionQuarterly", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("CommissionQuarterly", asOf, sb.ToString());
            return sb.ToString();
        }

        public string WinterMoratoriumStatus(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WinterMoratoriumStatus as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Balance,Territory,Amount,Reason,Status,CustomerName");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_WinterMoratoriumStatus", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("WinterMoratoriumStatus", asOf, sb.ToString());
            return sb.ToString();
        }

        public string MedicalHoldStatus(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("MedicalHoldStatus as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Reason,RateClass,Amount,Status,DueDate,Channel");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_MedicalHoldStatus", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Amount"]))).Append(",");
                    total += Utils.SafeDecimal(row["Amount"]);
                    sb.Append(BusinessRules.StatusText(Convert.ToString(row["Status"]))).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("MedicalHoldStatus", asOf, sb.ToString());
            return sb.ToString();
        }

        public string LargeCommercialAccounts(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LargeCommercialAccounts as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("DaysPastDue,CustomerName,RateClass,PostedOn,Channel,Territory");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_LargeCommercialAccounts", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToString(row["Territory"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
#if LEGACY_TAX
            sb.AppendLine("* includes pre-2018 surcharge");
#endif
            Archive("LargeCommercialAccounts", asOf, sb.ToString());
            return sb.ToString();
        }

        public string AgriculturalAccounts(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AgriculturalAccounts as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("Balance,RateClass,PostedOn,Channel,AccountNumber,DaysPastDue");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_AgriculturalAccounts", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["PostedOn"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Convert.ToString(row["Channel"])).Append(",");
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["DaysPastDue"])).Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("AgriculturalAccounts", asOf, sb.ToString());
            return sb.ToString();
        }

        public string ZeroUsageAccounts(DateTime asOf)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ZeroUsageAccounts as of " + asOf.ToString(Constants.DATE_FORMAT) + "  generated " + DateTime.Now);
            sb.AppendLine("AccountNumber,RateClass,CustomerName,Reason,DueDate,Balance");
            using (var db = Db.Open(AppSettings.Get("Reports:Connection") ?? "TallgrassReporting"))
            {
                var rows = db.Query("rpt_ZeroUsageAccounts", "@AsOf", asOf, "@Territory", AppSettings.GetInt("Reports:Territory", 0));
                decimal total = 0m;
                foreach (var row in rows)
                {
                    sb.Append(Convert.ToString(row["AccountNumber"])).Append(",");
                    sb.Append(Convert.ToString(row["RateClass"])).Append(",");
                    sb.Append(Convert.ToString(row["CustomerName"])).Append(",");
                    sb.Append(Convert.ToString(row["Reason"])).Append(",");
                    sb.Append(Convert.ToDateTime(row["DueDate"]).ToString(Constants.DATE_FORMAT)).Append(",");
                    sb.Append(Utils.FormatMoney(Utils.SafeDecimal(row["Balance"]))).Append(",");
                    total += Utils.SafeDecimal(row["Balance"]);
                    sb.AppendLine();
                }
                sb.AppendLine("TOTAL," + Utils.FormatMoney(total));
            }
            Archive("ZeroUsageAccounts", asOf, sb.ToString());
            return sb.ToString();
        }

        private void Archive(string name, DateTime asOf, string content)
        {
            var folder = AppSettings.Get("Reports:ArchivePath") ?? @"\\tg-fs01\reports";
            File.WriteAllText(Path.Combine(folder, name + "_" + asOf.ToString("yyyyMMdd") + "_" + Utils.Stamp() + ".csv"), content);
        }
    }
}
