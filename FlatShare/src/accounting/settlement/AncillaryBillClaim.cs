using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace de.creinbold.FlatShare
{
    public class AncillaryBillClaim : Claim
    {
        private DateUtils.Interval _BillingInterval;

        public AncillaryBillClaim(BillRecord bill) : base(bill.CreationDate, bill.DueDate, bill.TotalCosts)
        {
            _BillingInterval = bill.Interval;
            Debug.Assert(_BillingInterval.IsFinite);
            NotifyChange(bill.CreationDate);
        }

        public void CollectPrepayments(IEnumerable<ClaimWithPrepayment> claims, DateTime collectionDate)
        {
            Debug.Assert(_BillingInterval.To <= collectionDate);
            foreach (var claim in claims)
            {
                if (_BillingInterval.CompareTo(claim.PrepaymentDate) != 0) continue;
                var prepayment = claim.WithdrawPrepayment(this, collectionDate);
                Assign(prepayment, claim, collectionDate);
            }
        }

        protected override TaxReportEntry CreateTaxReportEntry(decimal assignedAmount)
        {
            var entry = new TaxReportEntry();
            entry.AssignedAmount = assignedAmount;
            entry.Due = this.DueDate;
            entry.AncillaryTax = assignedAmount;
            return entry;
        }
    }
}
