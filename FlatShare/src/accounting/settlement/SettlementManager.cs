using System;
using System.Collections.Generic;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{
    public class SettlementManager : IStorageListener
    {
        private static readonly int CONSIDERED_FUTURE_MONTHS = 3;

        private DateUtils.IDateProvider _DateProvider;
        private Tenants _Tenants;
        private TransactionData _TransactionData;
        private BillRecordData _BillRecordData;
        private Dictionary<Tenant, Settlement> _Settlements = new Dictionary<Tenant, Settlement>();


        public SettlementManager(IStorage storage, Tenants tenants, TransactionData transactionData, BillRecordData billRecordData, DateUtils.IDateProvider dateProvider = null)
        {
            storage.Register(this, tenants, transactionData, billRecordData);
            if (dateProvider == null) dateProvider = new DateUtils.SystemDateProvider();
            _DateProvider = dateProvider;
            _Tenants = tenants;
            _TransactionData = transactionData;
            _BillRecordData = billRecordData;
            Update();
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("settle", _SettleCommand, "[history <tenant>|statistics|check]", "Prints information about settlements. Type \"settle help\" for more information.");
        }

        public Settlement GetSettlementForTenant(Tenant tenant)
        {
            return _Settlements[tenant];
        }

        private string _SettleCommand(IEnumerable<string> args)
        {
            switch (args.FirstOrDefault())
            {
                case "history":
                    var tenantNameArg = String.Join(" ", args.Skip(1));
                    if (String.IsNullOrWhiteSpace(tenantNameArg))
                    {
                        return "Provide a valid tenant name. Usage: settle history <tenant>";
                    }
                    if (_Settlements.IsEmpty())
                    {
                        return "No tenants in the database.";
                    }
                    bool exactMatch;
                    var tenant = _Tenants.GetMostLikelyTenantFromString(tenantNameArg, out exactMatch);
                    if (!exactMatch)
                    {
                        var template = "No matching tenant found. Assuming \"{0}\"";
                        Console.WriteLine(String.Format(template, tenant.Name));
                    }
                    _PrintHistoryFor(tenant);
                    return "";
                case "statistics":
                    if (args.CountLowerEqual(2))
                    {
                        _PrintOverdueStatistics();
                        return "";
                    }
                    break;
                case "check":
                    if (args.CountLowerEqual(2))
                    {
                        _PrintUncoveredClaimsAndOpenCredits();
                        return "";
                    }
                    break;
            }
            return "Usage:\n" +
                   "settle history <tenant>: Print balance history for the given tenant.\n" +
                   "settle statistics: Prints the payment statistics for the landlord and tenants.\n" +
                   "settle check: Prints all settlements issues which require attention.";
        }

        private bool _CheckAndWarn(KeyValuePair<Tenant, Settlement> pair, Role role, string prefix = "")
        {
            var tenant = pair.Key;
            var settlement = pair.Value;
            string protagonist;
            string antagonist;
            switch (role)
            {
                case Role.LANDLORD:
                    protagonist = "The landlord";
                    antagonist = tenant.Name;
                    break;
                case Role.TENANT:
                    protagonist = tenant.Name;
                    antagonist = "the landlord";
                    break;
                default:
                    throw new ArgumentException("Unknown role.");
            }
            var amount = settlement.GetOverdueAmountFor(role);
            var credit = settlement.GetUnassignableCreditFor(role);
            if (amount != 0M)
            {
                Console.WriteLine(prefix + String.Format("{0} has uncovered liabilities of {1}{2} towards {3}.",
                                                         protagonist, amount, Currency.CURRENT.Symbol, antagonist));
            }
            if (credit != 0M)
            {
                Console.WriteLine(prefix + String.Format("{0} has a credit of {1}{2} that is not assignable to any claim of {3}.",
                                                         protagonist, credit, Currency.CURRENT.Symbol, antagonist));
            }
            bool allOkay = amount == 0M && credit == 0M;
            if (role == Role.TENANT)
            {
                allOkay &= _CheckAndWarnPrepayments(tenant, settlement, prefix);
            }
            return allOkay;
        }

        private void _WarnPrepayments(IList<ClaimWithPrepayment> pendingClaims, Tenant tenant, string prefix)
        {
            const string templateUnassigned = "Unassigned prepayments of {0}{1} for tenant {2} in {3}.";
            var sum = pendingClaims.Select(c => c.CoveredPrepayment).Sum();
            var first = pendingClaims.First();
            var last = pendingClaims.Last();
            string dateString = first == last ? DateUtils.DateAsString(first.DueDate) : new DateUtils.Interval(first.DueDate, last.DueDate).ToString();
            Console.WriteLine(prefix + String.Format(templateUnassigned, sum, Currency.CURRENT.Symbol, tenant.Name, dateString));
        }

        private bool _CheckAndWarnPrepayments(Tenant tenant, Settlement settlement, string prefix)
        {
            bool allOkay = true;
            var pendingClaims = new List<ClaimWithPrepayment>();
            foreach (var claim in settlement.ElapsedClaims.OfType<ClaimWithPrepayment>())
            {
                if (claim.PrepaymentIsResolved && pendingClaims.Count > 0)
                {
                    _WarnPrepayments(pendingClaims, tenant, prefix);
                    allOkay = false;
                    pendingClaims.Clear();
                }
                if (claim.CoveredPrepayment != 0M) pendingClaims.Add(claim);
            }

            if (pendingClaims.Count > 0)
            {
                var lastAllocationInterval = tenant.RoomAllocations.Last().Interval;
                var movedOut = lastAllocationInterval.IsFinite && lastAllocationInterval.To < _DateProvider.Now;
                if (movedOut || pendingClaims[0].DueDate.AddYears(1) < _DateProvider.Now)
                {
                    _WarnPrepayments(pendingClaims, tenant, prefix);
                    allOkay = false;
                }
            }
            return allOkay;
        }

        public void OnStorageUpdated(IFileSystem storage)
        {
            Update();
        }

        protected void Update()
        {
            DateTime dueDateLimit = _DateProvider.Now.AddMonths(CONSIDERED_FUTURE_MONTHS);

            _Settlements.Clear();
            foreach (var tenant in _Tenants.Entries)
            {
                var bills = _BillRecordData.GetBillRecordsForTenant(tenant);
                List<Claim> constructedClaims = new List<Claim>();
                constructedClaims.AddRange(DepositRateClaim.FromTenant(tenant));
                constructedClaims.AddRange(tenant.Rents.SelectMany(r => RentClaim.FromRent(r, dueDateLimit)));
                constructedClaims.AddRange(bills.Select(b => new AncillaryBillClaim(b)));
                constructedClaims.AddRange(DepositRefundClaim.FromTenant(tenant));
                constructedClaims.Sort((c1, c2) => c1.AnnounceDate.CompareTo(c2.AnnounceDate));

                var claims = constructedClaims;
                var credits = _TransactionData.GetTransactionsForOwner(tenant).Select(t => new Credit(t));

                var settlement = new Settlement(claims, credits);
                _Settlements[tenant] = settlement;
            }
            _Settlements.Values.ElementwiseInvoke(s => s.SettleUpTo(_DateProvider.Now.AddDays(1)));
            var settlements_to_check = _Settlements.Where(e => e.Key.IsCharged);
            settlements_to_check.ElementwiseInvoke(e => _CheckAndWarn(e, Role.LANDLORD, "Warning: "));
            settlements_to_check.ElementwiseInvoke(e => _CheckAndWarn(e, Role.TENANT, "Warning: "));
        }

        private string _PrintUncoveredClaimsAndOpenCredits()
        {
            var settlements_to_check = _Settlements.Where(e => e.Key.IsCharged);
            // .ToList() is required in order to enforce invoking _CheckAndWarn() for all settlements.
            var allOkayLandlord = settlements_to_check.Select(e => _CheckAndWarn(e, Role.LANDLORD)).ToList().All(b => b);
            var allOkayTenant = settlements_to_check.Select(e => _CheckAndWarn(e, Role.TENANT)).ToList().All(b => b);
            if (allOkayLandlord && allOkayTenant) return "No uncovered claims, open credits or unexpected prepayments found.";
            else return "";
        }

        private void _PrintOverdueStatistics(string name, IEnumerable<Claim> claims)
        {
            int dayThreshold = 30;
            int countInTime = 0;
            int countLate = 0;
            int countMuchTooLate = 0;
            int notCovered = 0;
            foreach (var claim in claims)
            {
                if (!claim.IsCovered)
                {
                    notCovered++;
                }
                else
                {
                    var diff = (claim.CoveredAt.Value - claim.DueDate).Days;
                    if (diff > dayThreshold) countMuchTooLate++;
                    else if (diff > 0) countLate++;
                    else countInTime++;
                }
            }
            var template = "{0, 30}: {1,3} in time - {2,3} late - {3,3} very late (>{4} days) - {5,3} not covered.";
            Console.WriteLine(String.Format(template, name, countInTime, countLate, countMuchTooLate, dayThreshold, notCovered));
        }

        private void _PrintOverdueStatistics()
        {
            var claims = _Settlements.Values.SelectMany(s => s.ElapsedClaims.Where(c => c.LastDebtor == Role.LANDLORD));
            _PrintOverdueStatistics("The Landlord", claims);
            foreach (var kv in _Settlements)
            {
                claims = kv.Value.ElapsedClaims.Where(c => c.LastDebtor == Role.TENANT);
                _PrintOverdueStatistics(kv.Key.Name, claims);
            }
        }

        private void _PrintHistoryFor(Tenant tenant)
        {
            var credits = _Settlements[tenant].Credits;
            Console.WriteLine(new String('-', 78));
            if (credits.IsEmpty()) Console.WriteLine("No credits available.");
            else Console.WriteLine("Credits:");
            foreach (var credit in credits)
            {
                Console.WriteLine(credit.ToString(true));
                foreach (var entry in credit.History) Console.WriteLine("| " + entry);
            }

            var elapsedClaims = _Settlements[tenant].ElapsedClaims;
            Console.WriteLine(new String('-', 78));
            if (elapsedClaims.IsEmpty()) Console.WriteLine("No elapsed claims available.");
            else Console.WriteLine("Past/Overdue claims:");
            foreach (var claim in elapsedClaims)
            {
                Console.WriteLine(claim.ToString(true));
                foreach (var entry in claim.History) Console.WriteLine("| " + entry.ToString(claim.InitialDebtor == Role.LANDLORD));
            }
            Console.WriteLine(new String('-', 78));
            var futureClaims = _Settlements[tenant].FutureClaims;
            if (futureClaims.IsEmpty()) Console.WriteLine("No claims due in the near future available.");
            else Console.WriteLine("Future claims:");
            foreach (var claim in futureClaims)
            {
                Console.WriteLine(claim.ToString(true));
                foreach (var entry in claim.History) Console.WriteLine("| " + entry.ToString(claim.InitialDebtor == Role.LANDLORD));
            }
        }
    }
}
