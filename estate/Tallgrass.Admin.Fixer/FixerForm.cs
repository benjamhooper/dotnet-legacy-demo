using System;
using System.Windows.Forms;
using Tallgrass.Business;
using Tallgrass.Common;

namespace Tallgrass.Admin.Fixer
{
    /// <summary>Every button on this form is a bug report nobody filed.</summary>
    public partial class FixerForm : Form
    {
        private TextBox accountIdTextBox = new TextBox();

        private void FixNegativeBalance_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixNegativeBalance for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixNegativeBalance", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixNegativeBalance " + accountId);
        }

        private void FixDuplicatePayment_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixDuplicatePayment for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixDuplicatePayment", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixDuplicatePayment " + accountId);
        }

        private void FixOrphanedInvoice_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixOrphanedInvoice for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixOrphanedInvoice", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixOrphanedInvoice " + accountId);
        }

        private void FixStuckFinalBill_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixStuckFinalBill for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("usp_ProrateFinalBill", "@AccountId", accountId, "@CycleStart", DateTime.Now.AddDays(-30), "@MoveOut", DateTime.Now, "@MonthlyCustomerCharge", 0m, "@UsageKwh", 0m, "@RatePerKwh", 0m, "@DepositHeld", 0m, "@Credits", 0m);
                db.Execute("fix_FixStuckFinalBill", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixStuckFinalBill " + accountId);
        }

        private void FixMissedLateFee_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixMissedLateFee for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixMissedLateFee", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixMissedLateFee " + accountId);
        }

        private void FixDoubleLateFee_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixDoubleLateFee for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixDoubleLateFee", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixDoubleLateFee " + accountId);
        }

        private void FixWrongRateClass_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixWrongRateClass for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixWrongRateClass", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixWrongRateClass " + accountId);
        }

        private void FixArrangementNotBroken_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("FixArrangementNotBroken for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_FixArrangementNotBroken", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "FixArrangementNotBroken " + accountId);
        }

        private void ReopenClosedAccount_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("ReopenClosedAccount for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_ReopenClosedAccount", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "ReopenClosedAccount " + accountId);
        }

        private void ForceMonthClose_Click(object sender, EventArgs e)
        {
            int accountId = int.Parse(accountIdTextBox.Text);
            if (MessageBox.Show("ForceMonthClose for " + accountId + "?", "Are you sure?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using (var db = Db.Open())
            {
                db.Execute("fix_ForceMonthClose", "@AccountId", accountId, "@By", Environment.UserName, "@On", DateTime.Now);
            }
            AuditLog.Write("Fixer", "ForceMonthClose " + accountId);
        }
    }
}
