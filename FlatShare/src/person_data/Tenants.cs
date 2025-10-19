using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class Tenants : XmlDirectoryParser<Tenant>
    {
        private static readonly string ROOT = "tenants";
        private DateUtils.IDateProvider _DateProvider;

        public Tenants(IStorage storage, DateUtils.IDateProvider dateProvider = null) : base(storage, ROOT)
        {
            _DateProvider = dateProvider;
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("tenants", this.ExecuteCommand, "[active]",
                "Display all tenants registered in the database. " +
                "If the \"active\" option is provided, only tenants currently renting are shown.");
        }

        protected override void OnUpdated()
        {
            base.OnUpdated();
            Values.ElementwiseInvoke(t => t.CheckIntegrity(_DateProvider));
        }

        public Tenant GetMostLikelyTenantFromString(string s)
        {
            int distance;
            return GetMostLikelyTenantFromString(s, out distance);
        }

        public Tenant GetMostLikelyTenantFromString(string s, out bool exact)
        {
            int distance;
            var result = GetMostLikelyTenantFromString(s, out distance);
            exact = (distance == 0);
            return result;
        }

        public Tenant GetMostLikelyTenantFromString(string s, out int distance)
        {
            return StringMatching.GetMostLikelyMatch(StringMatching.DamerauLevenshteinToSubstring, t => t.Name.ToLower(), s.ToLower(), Values, out distance);
        }

        private string ExecuteCommand(IEnumerable<string> args)
        {
            string invalid_usage = "Invalid argument list. Usage: tenants [active].";

            var argsList = args.ToList();
            if (argsList.Count > 1) return invalid_usage;
            if (argsList.Count == 1 && argsList[0] != "active") return invalid_usage;

            IEnumerable<Tenant> tenants = Values.OrderBy(t => t.AllocationInterval.From);
            if (argsList.Count == 1) tenants = tenants.Where(t => t.AllocationInterval.Contains(_DateProvider.Now));

            foreach (var tenant in tenants)
            {
                Console.WriteLine(String.Format("{0} ({1}) at {2}", tenant.Name, tenant.Gender, tenant.AddressAsSingleLine));
                if (tenant.Deposit != null)
                    Console.WriteLine("    " + tenant.Deposit.ToString().CapitalizeFirstLetter());
                tenant.RoomAllocations.ElementwiseInvoke(a => Console.WriteLine("    " + a.ToString().CapitalizeFirstLetter()));
                tenant.BankAccounts.ElementwiseInvoke(a => Console.WriteLine("    " + a.ToString().CapitalizeFirstLetter()));
                tenant.Rents.ElementwiseInvoke(r => Console.WriteLine("    " + r.ToString().CapitalizeFirstLetter()));
            }
            return "";
        }
    }
}
