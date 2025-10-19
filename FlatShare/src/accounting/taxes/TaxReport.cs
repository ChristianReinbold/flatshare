using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class TaxReport
    {
        public struct TenantTransactionEntry
        {
            public Tenant Tenant { get; private set; }
            public DateTime Date { get; private set; }
            public decimal Amount { get; private set; }
            public IEnumerable<TaxReportEntry> Assignments { get; private set; }
            public decimal Unassigned { get; private set; }

            public TenantTransactionEntry(Tenant tenant, Credit credit)
            {
                Tenant = tenant;
                Date = credit.Transaction.Date;
                Amount = credit.Transaction.Amount;
                Unassigned = Amount - credit.TaxReportEntries.Select(e => e.AssignedAmount).Sum();
                Assignments = credit.TaxReportEntries;
            }

            public bool IsRelevant(int year)
            {
                return Date.Year == year || Assignments.Any(e => e.TaxYear == year);
            }
        }

        public struct DepositTaxReportEntry
        {
            public Tenant Tenant { get; set; }
            public DateTime Due { get; set; }
            public decimal ObtainedAmount { get; set; }
            public decimal RefundedAmount { get; set; }
            public decimal RetainedAmount { get; set; }
            public decimal AncillaryAmount { get; set; }
        }

        public struct ProviderTransactionEntry
        {
            public string Provider { get; private set; }
            public DateTime Date { get; private set; }
            public decimal Amount { get; private set; }

            public ProviderTransactionEntry(ServiceProvider provider, Transaction transaction)
            {
                Provider = provider.Name;
                Date = transaction.Date;
                Amount = -transaction.Amount;
            }
        }

        public struct ProviderEntry
        {
            public string Provider { get; private set; }
            public IEnumerable<string> Services { get; private set; }
            public decimal Amount { get; private set; }

            public ProviderEntry(ServiceProvider provider, decimal amount)
            {
                Provider = provider.Name;
                var services = provider.Services.Select(s => s.AssociatedPosition.Name).ToList();
                services.Sort();
                Services = services.AsEnumerable();
                Amount = amount;
            }
        }

        public int Year { get; private set; }
        public IEnumerable<TenantTransactionEntry> TenantTransactions { get; private set; }
        public IEnumerable<DepositTaxReportEntry> DepositEntries { get; private set; }
        public IEnumerable<ProviderTransactionEntry> ProviderTransactions { get; private set; }
        public IEnumerable<ProviderEntry> ProviderEntries { get; private set; }

        public string FileName { get { return String.Format("{0}_tax_report", Year); } }

        public TaxReport(Tenants tenants,
                         ServiceProviders serviceProviders,
                         SettlementManager settlementManager,
                         TransactionData transactionData,
                         int year)
        {
            Year = year;
            FillTenantTransactions(tenants.Values, settlementManager);
            FillDepositEntries(tenants.Values, settlementManager);
            FillProviderTransactions(serviceProviders.Values, transactionData);
        }

        private void FillTenantTransactions(IEnumerable<Tenant> tenants, SettlementManager settlementManager)
        {
            List<TenantTransactionEntry> tenantTransactions = new List<TenantTransactionEntry>();
            foreach (var tenant in tenants)
            {
                if (!tenant.IsCharged) continue;
                var settlement = settlementManager.GetSettlementForTenant(tenant);
                var newEntries = settlement.Credits.Select(c => new TenantTransactionEntry(tenant, c));
                var filteredEntries = newEntries.Where(e => e.IsRelevant(Year));
                tenantTransactions.AddRange(filteredEntries);
            }
            tenantTransactions.Sort((x, y) => x.Date.CompareTo(y.Date));
            TenantTransactions = tenantTransactions.AsEnumerable();
        }

        private void FillDepositEntries(IEnumerable<Tenant> tenants, SettlementManager settlementManager)
        {
            List<DepositTaxReportEntry> depositEntries = new List<DepositTaxReportEntry>();
            foreach (var tenant in tenants)
            {
                var settlement = settlementManager.GetSettlementForTenant(tenant);
                var refundClaim = settlement.Claims.OfType<DepositRefundClaim>().SingleOrDefault();
                if (refundClaim == null) continue;
                if (refundClaim.DueDate.Year != Year) continue;
                decimal obtained = 0M;
                decimal refunded = 0M;
                foreach (var entry in settlement.Credits.SelectMany(c => c.TaxReportEntries))
                {
                    if (entry.FlowForDeposit >= 0) obtained += entry.FlowForDeposit;
                    else refunded -= entry.FlowForDeposit;
                }
                var depositEntry = new DepositTaxReportEntry();
                depositEntry.Tenant = tenant;
                depositEntry.Due = refundClaim.DueDate;
                depositEntry.ObtainedAmount = obtained;
                depositEntry.RefundedAmount = refunded;
                depositEntry.AncillaryAmount = refundClaim.CoveredPrepayment + refundClaim.WithdrawnPrepayment;
                depositEntry.RetainedAmount = obtained - refunded - depositEntry.AncillaryAmount;
                depositEntries.Add(depositEntry);
            }
            depositEntries.Sort((x, y) => x.Due.CompareTo(y.Due));
            DepositEntries = depositEntries.AsEnumerable();
        }

        private void FillProviderTransactions(IEnumerable<ServiceProvider> providers, TransactionData transactionData)
        {
            List<ProviderTransactionEntry> providerTransactions = new List<ProviderTransactionEntry>();
            List<ProviderEntry> providerEntries = new List<ProviderEntry>();
            foreach (var provider in providers)
            {
                var transactions = transactionData.GetTransactionsForOwner(provider).Where(t => t.Date.Year == Year);
                providerTransactions.AddRange(transactions.Select(t => new ProviderTransactionEntry(provider, t)));

                var amount = -transactions.Select(t => t.Amount).Sum();
                if (amount != 0M) providerEntries.Add(new ProviderEntry(provider, amount));
            }
            providerTransactions.Sort((x, y) => x.Date.CompareTo(y.Date));
            ProviderTransactions = providerTransactions.AsEnumerable();
            providerEntries.Sort((x, y) => x.Provider.CompareTo(y.Provider));
            ProviderEntries = providerEntries.AsEnumerable();
        }
    }
}
