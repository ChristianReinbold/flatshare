using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public enum Role { LANDLORD, TENANT }

    public static class RoleExtension
    {
        public static Role Flip(this Role role)
        {
            switch (role)
            {
                case Role.LANDLORD:
                    return Role.TENANT;
                case Role.TENANT:
                    return Role.LANDLORD;
                default:
                    throw new ArgumentException("Unknown role type");
            }
        }
    }

    public abstract class Claim
    {
        protected virtual decimal SignedAmount { get { return _SignedFixedAmount; } }
        public DateTime AnnounceDate { get; private set; }
        public DateTime DueDate { get; private set; }
        public DateTime? CoveredAt { get; private set; }

        private decimal _SignedFixedAmount;
        private decimal _SignedCoveredAmount;

        public Role? CurrentDebtor
        {
            get
            {
                if (_SignedCoveredAmount == SignedAmount) return null;
                if (_SignedCoveredAmount > SignedAmount) return Role.LANDLORD;
                return Role.TENANT;
            }
        }

        public Role LastDebtor { get; private set; }

        public Role InitialDebtor { get { return SignedAmount < 0 ? Role.LANDLORD : Role.TENANT; } }

        public decimal Amount { get { return Math.Abs(SignedAmount); } }
        public decimal OpenAmount { get { return Math.Abs(SignedAmount - _SignedCoveredAmount); } }

        public bool IsCovered { get { return _SignedCoveredAmount == SignedAmount; } }

        private List<CoverEntry> _History = new List<CoverEntry>();

        public IEnumerable<CoverEntry> History { get { return _History.AsEnumerable(); } }

        protected Claim(DateTime announceDate, DateTime dueDate, decimal fixedAmount)
        {
            AnnounceDate = announceDate;
            DueDate = dueDate;
            _SignedFixedAmount = fixedAmount;
            _SignedCoveredAmount = 0M;
        }

        protected void Assign(decimal amount, object target, DateTime date)
        {
            this._SignedCoveredAmount += amount;
            this._History.Add(new CoverEntry(amount, target));
            this.NotifyChange(date);
        }

        protected decimal GetAssignablePart(decimal amount)
        {
            if (IsCovered) return 0;
            if (CurrentDebtor.Value == Role.TENANT) return Math.Max(0, Math.Min(OpenAmount, amount));
            else return Math.Max(-OpenAmount, Math.Min(0, amount));
        }

        public void BalanceWith(Claim other, DateTime balanceDate)
        {
            if (this.IsCovered || other.IsCovered || this.CurrentDebtor == other.CurrentDebtor) return;
            var transferAmount = Math.Min(this.OpenAmount, other.OpenAmount);
            if (this.CurrentDebtor == Role.LANDLORD) transferAmount *= -1;
            this.Assign(transferAmount, other, balanceDate);
            other.Assign(-transferAmount, this, balanceDate);
        }

        public void BalanceWith(Credit other, DateTime balanceDate)
        {
            if (this.IsCovered || other.IsDepleted || this.CurrentDebtor != other.Owner) return;
            var amount = other.Withdraw(this.OpenAmount, this);
            if (amount == 0) return;
            if (this.CurrentDebtor == Role.LANDLORD) amount *= -1;
            this.Assign(amount, other, balanceDate);
            var entry = CreateTaxReportEntry(amount);
            entry.Reason = this;
            other.AddTaxReportEntry(entry);
        }

        protected abstract TaxReportEntry CreateTaxReportEntry(decimal assignedAmount);

        public override string ToString()
        {
            return ToString(false);
        }

        protected void NotifyChange(DateTime date)
        {
            if (CoveredAt.HasValue) return;
            if (IsCovered) CoveredAt = date;
            else LastDebtor = CurrentDebtor.GetValueOrDefault(Role.TENANT);

        }

        public string ToString(bool withAnnotations)
        {
            var name = this.GetType().Name.SplitCamelCase();
            if (name.ToLower().EndsWith("claim"))
            {
                name = name.Substring(0, name.Length - 5).TrimEnd();
            }
            var annotations = new List<string>();
            if (!IsCovered) annotations.Add(String.Format("{0} still owes {1}{2}", CurrentDebtor, OpenAmount, Currency.CURRENT.Symbol));
            string result = String.Format("{0} for {1} over {2}{3} due at {4}", name, InitialDebtor.Flip(), Amount, Currency.CURRENT.Symbol, DueDate.ToShortDateString());
            if (withAnnotations && !annotations.IsEmpty()) result += " (" + String.Join(", ", annotations) + ")";
            return result;
        }

    }
}
