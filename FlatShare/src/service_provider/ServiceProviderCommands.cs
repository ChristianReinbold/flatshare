using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class ServiceProviderCommands
    {
        private ServiceProviders _Providers;
        private TransactionData _TransactionData;
        private DateUtils.IDateProvider _DateProvider;

        public ServiceProviderCommands(ServiceProviders providers, TransactionData transactionData, DateUtils.IDateProvider dateProvider = null)
        {
            if (dateProvider == null) dateProvider = new DateUtils.SystemDateProvider();
            _Providers = providers;
            _TransactionData = transactionData;
            _DateProvider = dateProvider;
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("providers", ExecuteCommand, "[balance [<provider>] [<from>] [<to>]]",
                "Displays all registered service providers when no options are given. If the balance option is provided, " +
                "costs for provided services and associated debits are shown for a given time period. " +
                "If no provider is given, aggregated information for all providers is shown. " +
                "<from> defaults to today minus one year, <to> defaults to today.");
        }

        private string ExecuteCommand(IEnumerable<string> args)
        {
            if (_Providers.IsEmpty()) return "No providers registered.";

            string invalid_usage = "Invalid argument list. Usage: providers [balance [<provider>] [<from>] [<to>]]. Allowed date formats: YYYY, MM-YYYY, DD-MM-YYYY";

            var argsList = args.ToList();
            if (argsList.IsEmpty())
            {
                DisplayProviders();
                return "";
            }

            if (argsList[0] != "balance")
            {
                return invalid_usage;
            }

            DateTime to = _DateProvider.Now.Date;
            DateTime from = to.AddYears(-1);
            ServiceProvider provider = null;

            bool providerInArgs = false;
            if (argsList.Count == 4)
            {
                providerInArgs = true;
            }
            else if (argsList.Count > 1)
            {
                try
                {
                    DateUtils.DateFromString(argsList[1], DateUtils.DateFillMode.First);
                }
                catch (FormatException)
                {
                    providerInArgs = true;
                }
            }

            if (providerInArgs) provider = _Providers.GetMostLikelyServiceProviderFromString(argsList[1]);

            int fromIndex = providerInArgs ? 2 : 1;
            int toIndex = providerInArgs ? 3 : 2;

            try
            {
                if (fromIndex < argsList.Count) from = DateUtils.DateFromString(argsList[fromIndex], DateUtils.DateFillMode.First);
                if (toIndex < argsList.Count) to = DateUtils.DateFromString(argsList[toIndex], DateUtils.DateFillMode.Last);
            }
            catch (FormatException)
            {
                return invalid_usage;
            }

            var interval = new DateUtils.Interval(from, to);

            if (interval.IsEmpty)
            {
                return "\"from\" date located after \"to\" date.";
            }

            if (providerInArgs) DisplayBalanceForProvider(_Providers.GetMostLikelyServiceProviderFromString(argsList[1]), interval);
            else DisplayBalances(interval);
            return "";
        }

        private void DisplayProviders()
        {
            foreach (var provider in _Providers.Values)
            {
                Console.WriteLine(provider.Name);
                provider.Services.ElementwiseInvoke(a => Console.WriteLine("    " + a.ToString().CapitalizeFirstLetter()));
                provider.BankAccounts.ElementwiseInvoke(a => Console.WriteLine("    " + a.ToString().CapitalizeFirstLetter()));
            }
        }

        private void DisplayBalances(DateUtils.Interval interval)
        {
            var providerNames = new List<string>();
            var debitStrings = new List<string>();
            var costsStrings = new List<string>();
            var differenceStrings = new List<string>();
            providerNames.Add("Provider Name");
            costsStrings.Add("Claimed");
            debitStrings.Add("Debited");
            differenceStrings.Add("Difference");
            foreach (var provider in _Providers.Values.OrderBy(p => p.Name))
            {
                var transactions = _TransactionData.GetTransactionsForOwner(provider).Where(t => interval.Contains(t.Date));
                var debitedAmount = -transactions.Select(t => t.Amount).Sum();
                var costs = provider.Services.Select(s => s.GetCosts(interval)).Sum();
                if (debitedAmount == 0M && costs == 0M) continue;
                var difference = debitedAmount - costs;
                providerNames.Add(provider.Name);
                debitStrings.Add(debitedAmount.ToString("N2"));
                costsStrings.Add(costs.ToString("N2"));
                differenceStrings.Add(difference.ToString("N2"));
            }
            var nameLength = providerNames.Max(s => s.Length);
            var debitLength = debitStrings.Max(s => s.Length);
            var costsLength = costsStrings.Max(s => s.Length);
            var differenceLength = differenceStrings.Max(s => s.Length);
            var format = String.Format("{{0, -{0}}}  {{1, {1}}}  {{2, {2}}}  {{3, {3}}}", nameLength, debitLength, costsLength, differenceLength);

            Console.WriteLine(String.Format("\nBalances of service providers in {0}:\n", interval));
            Console.WriteLine(String.Format(format, providerNames[0], debitStrings[0], costsStrings[0], differenceStrings[0]));
            Console.WriteLine(new String('-', nameLength + debitLength + costsLength + differenceLength + 6));
            for (int i = 1; i < providerNames.Count; i++)
            {
                Console.WriteLine(String.Format(format, providerNames[i], debitStrings[i], costsStrings[i], differenceStrings[i]));
            }
            Console.WriteLine("\n");
        }

        private void DisplayBalanceForProvider(ServiceProvider provider, DateUtils.Interval interval)
        {
            var transactions = _TransactionData.GetTransactionsForOwner(provider).Where(t => interval.Contains(t.Date));

            var debitedAmount = -transactions.Select(t => t.Amount).Sum();
            var costSum = provider.Services.Select(s => s.GetCosts(interval)).Sum();

            Console.WriteLine(String.Format("Balance for {0} in {1}", provider.Name, interval));
            Console.WriteLine(String.Format("Claims (Total: {0}{1})",
                                            costSum.ToString("N2"),
                                            Currency.CURRENT.Symbol));
            var positionNames = new List<string>();
            var costsStrings = new List<string>();
            foreach (var service in provider.Services.OrderBy(c => c.AssociatedPosition.Name))
            {
                var costs = service.GetCosts(interval);
                positionNames.Add(service.AssociatedPosition.Name);
                costsStrings.Add(costs.ToString("N2") + Currency.CURRENT.Symbol);
            }
            var nameLength = positionNames.Max(s => s.Length);
            var costsLength = costsStrings.Max(s => s.Length);
            var format = String.Format("    {{0, -{0}}}  {{1, {1}}}", nameLength, costsLength);
            for (int i = 0; i < positionNames.Count; i++)
            {
                Console.WriteLine(String.Format(format, positionNames[i], costsStrings[i]));
            }

            Console.WriteLine(String.Format("Transactions (Total debited amount: {0}{1})",
                                            debitedAmount.ToString("N2"),
                                            Currency.CURRENT.Symbol));
            transactions.OrderBy(t => t.Date).ElementwiseInvoke(t => Console.WriteLine("    " + t.ToString().CapitalizeFirstLetter()));
        }
    }
}
