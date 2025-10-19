using System;
using System.Collections.Generic;

namespace de.creinbold.FlatShare
{
    public class DepositRefundClaim : ClaimWithPrepayment
    {
        private static readonly int PAYBACK_DUE_IN_MONTS = 1;

        public static IEnumerable<DepositRefundClaim> FromTenant(Tenant tenant)
        {
            var deposit = tenant.Deposit;
            if (deposit == null) yield break;
            if (!deposit.DoPayout) yield break;

            if (tenant.RoomAllocations.IsEmpty())
            {
                var template = "Deposit of tenant \"{0}\" cannot be processed. At least one room allocation is required.";
                throw new IntegrityException(String.Format(template, tenant.Name));
            }

            var allocationInterval = tenant.AllocationInterval;
            var moveOutDate = allocationInterval.To;
            var due = moveOutDate.AddMonths(PAYBACK_DUE_IN_MONTS);
            yield return new DepositRefundClaim(moveOutDate, due, deposit);
        }

        private Deposit _Deposit;

        private DepositRefundClaim(DateTime moveOutDate, DateTime dueDate, Deposit deposit)
            : base(moveOutDate, dueDate, deposit.Retained.Value - deposit.Amount, deposit.Ancillary.Value, moveOutDate)
        {
            _Deposit = deposit;
            NotifyChange(moveOutDate);
        }

        protected override TaxReportEntry CreateTaxReportEntry(decimal assignedAmount)
        {
            var entry = new TaxReportEntry();
            entry.AssignedAmount = assignedAmount;
            entry.Due = this.DueDate;
            entry.FlowForDeposit = assignedAmount;
            return entry;
        }
    }
}
