using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class RentClaim : ClaimWithPrepayment
    {
        public static IEnumerable<RentClaim> FromRent(MonthlyRent rent, DateTime dueDateLimit)
        {
            if (rent.Net == 0M && rent.Ancillary == 0M) yield break;
            foreach (var dueDate in rent.DueDates.TakeWhile(d => d <= dueDateLimit))
            {
                yield return new RentClaim(rent, dueDate);
            }
        }

        private RentClaim(MonthlyRent rent, DateTime dueDate) : base(rent.Announced, dueDate, rent.Net, rent.Ancillary, dueDate)
        {
            NotifyChange(rent.Announced);
        }

        protected override TaxReportEntry CreateTaxReportEntry(decimal assignedAmount)
        {
            Debug.Assert(LastDebtor == Role.TENANT);
            var entry = new TaxReportEntry();
            entry.AssignedAmount = assignedAmount;
            entry.Due = this.DueDate;
            entry.AncillaryTax = Math.Min(CoveredPrepayment + WithdrawnPrepayment, assignedAmount);
            entry.NetTax = assignedAmount - entry.AncillaryTax;
            return entry;
        }
    }
}
