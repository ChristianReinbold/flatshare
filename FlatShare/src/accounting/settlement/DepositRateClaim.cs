using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class DepositRateClaim : Claim
    {
        private static readonly int MAX_NUMBER_RATES = 3;

        public static IEnumerable<DepositRateClaim> FromTenant(Tenant tenant)
        {
            var deposit = tenant.Deposit;
            if (deposit == null) yield break;

            if (tenant.RoomAllocations.IsEmpty())
            {
                var template = "Deposit of tenant \"{0}\" cannot be processed. At least one room allocation is required.";
                throw new IntegrityException(String.Format(template, tenant.Name));
            }

            var allocationInterval = tenant.AllocationInterval;
            var moveInDate = allocationInterval.From;

            // Split deposit fee into three payments 
            var firstRents = tenant.Rents.SelectMany(r => RentClaim.FromRent(r, moveInDate.AddYears(1))).Take(MAX_NUMBER_RATES);
            var dueDates = firstRents.Select(r => r.DueDate).ToList();
            if (dueDates.Count == 0) dueDates.Add(moveInDate);
            else dueDates[0] = DateUtils.Min(moveInDate, dueDates[0]);

            decimal amountPerPayment = (int)(deposit.Amount) / dueDates.Count;
            decimal amountOfLastPayment = deposit.Amount - amountPerPayment * (dueDates.Count - 1);

            var announceDate = moveInDate;
            if (!firstRents.IsEmpty()) announceDate = firstRents.First().AnnounceDate;

            foreach (var dueDate in dueDates.TakeAllButLast())
            {
                yield return new DepositRateClaim(announceDate, dueDate, amountPerPayment);
            }
            yield return new DepositRateClaim(announceDate, dueDates.Last(), amountOfLastPayment);
        }

        private DepositRateClaim(DateTime announceDate, DateTime dueDate, decimal fixedAmount) : base(announceDate, dueDate, fixedAmount)
        {
            NotifyChange(announceDate);
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
