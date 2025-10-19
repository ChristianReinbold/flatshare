using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class Credit
    {
        private decimal _Expended = 0M;
        public Transaction Transaction { get; private set; }

        public Role Owner { get { return Transaction.Amount >= 0 ? Role.TENANT : Role.LANDLORD; } }
        public decimal Remaining { get { return Math.Abs(Transaction.Amount) - _Expended; } }

        public bool IsDepleted { get { return Remaining == 0; } }

        private List<CoverEntry> _History = new List<CoverEntry>();
        public IEnumerable<CoverEntry> History { get { return _History.AsEnumerable(); } }

        private List<TaxReportEntry> _TaxReportEntries = new List<TaxReportEntry>();
        public IEnumerable<TaxReportEntry> TaxReportEntries { get { return _TaxReportEntries.AsEnumerable(); } }

        public Credit(Transaction transaction)
        {
            Transaction = transaction;
        }

        public decimal Withdraw(decimal max, Claim invokingClaim)
        {
            var withdrawnCredit = Math.Min(max, Remaining);
            Debug.Assert(withdrawnCredit >= 0);
            if (withdrawnCredit > 0)
            {
                _Expended += withdrawnCredit;
                _History.Add(new CoverEntry(withdrawnCredit, invokingClaim));
            }
            return withdrawnCredit;
        }

        public void BalanceWith(Credit other, DateTime balanceDate)
        {
            if (this.Owner == other.Owner || this.IsDepleted || other.IsDepleted) return;

            var balanceAmount = Math.Min(this.Remaining, other.Remaining);
            this._Expended += balanceAmount;
            other._Expended += balanceAmount;
            this._History.Add(new CoverEntry(balanceAmount, other));
            other._History.Add(new CoverEntry(balanceAmount, this));
            this.AddTaxReportEntryForBalancingCredits(balanceAmount, other);
            other.AddTaxReportEntryForBalancingCredits(balanceAmount, this);
        }

        public void AddTaxReportEntry(TaxReportEntry entry)
        {
            if (entry.Due.HasValue)
            {
                // Consider "10-Tagesregelung"
                var possibleYears = new HashSet<int>();
                possibleYears.Add(Transaction.Date.Year);
                possibleYears.Add(Transaction.Date.AddDays(10).Year);
                possibleYears.Add(Transaction.Date.AddDays(-10).Year);
                if (possibleYears.Contains(entry.Due.Value.Year)) entry.TaxYear = entry.Due.Value.Year;
                else entry.TaxYear = Transaction.Date.Year;
            }
            else
            {
                entry.TaxYear = Transaction.Date.Year;
            }
            _TaxReportEntries.Add(entry);
        }

        private void AddTaxReportEntryForBalancingCredits(decimal assignedAmount, Credit other)
        {
            if (this.Owner == Role.LANDLORD) assignedAmount = -1 * assignedAmount;
            var entry = new TaxReportEntry();
            entry.AssignedAmount = assignedAmount;
            entry.Reason = other;
            AddTaxReportEntry(entry);
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool withAnnotations)
        {
            var annotations = new List<string>();
            if (!IsDepleted) annotations.Add(String.Format("{0}{1} remaining", Remaining, Currency.CURRENT.Symbol));
            string result = String.Format("Credit from {0} owned by the {1}", Transaction.Date.ToShortDateString(), Owner);
            if (withAnnotations && !annotations.IsEmpty()) result += " (" + String.Join(", ", annotations) + ")";
            return result;
        }
    }
}
