using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class SettlementManagerTest
    {
        private const string IBAN_DE = "DE44033224803003270479";
        private const string IBAN_NL1 = "NL94BLTT9732507274";
        private const string IBAN_NL2 = "NL44DOIU8538471081";

        private static Tenants BuildTenants(IStorage storage)
        {
            // stays short amount of time, pays, service charge deducted from deposit, bill with refund from landlord
            var t1 = new Tenant("Ludwig", "van Beethoven", Tenant.Genders.MALE);
            t1.BankAccounts.Add(new BankAccount("Ludwig van Beethoven", IBAN_DE));
            t1.Deposit = new Deposit(680M, 130M, 0);
            t1.RoomAllocations.Add(new RoomAllocation("TenantRoom", 1, "01-2018", "02-2018"));
            t1.Rents.Add(new MonthlyRent(300M, 60M, 3, "01-2018", "02-2018", "20-12-2017"));

            // current stay, some payments missing, deposit in two charges, bill with additional payment from tenant
            var t2 = new Tenant("Johann", "de Mej", Tenant.Genders.MALE);
            t2.BankAccounts.Add(new BankAccount("Johann de Mej", IBAN_NL1));
            t2.BankAccounts.Add(new BankAccount("Johann de Mej", IBAN_NL2));
            t2.Deposit = new Deposit(680M);
            t2.RoomAllocations.Add(new RoomAllocation("TenantRoom", 1, "04-2018"));
            t2.Rents.Add(new MonthlyRent(310M, 80M, 3, "04-2018", "", "13-03-2018"));

            var tenants = new Tenants(storage);
            tenants.Add("t1.xml", t1);
            tenants.Add("t2.xml", t2);
            return tenants;
        }

        private static TransactionData BuildTransactions(IStorage storage, Tenants tenants)
        {
            var transactionData = new TransactionData(storage, tenants);
            var l = transactionData.Transactions;
            l.Add(new Transaction(IBAN_DE, "27-12-2017", 360M + 680M));
            l.Add(new Transaction(IBAN_DE, "01-02-2018", 360M));
            l.Add(new Transaction(IBAN_DE, "02-03-2018", -550M));
            // other tenant
            l.Add(new Transaction(IBAN_NL1, "29-03-2018", 390M + 340M));
            l.Add(new Transaction(IBAN_NL1, "02-05-2018", 350M + 340M)); // 40€ not payed, considered as missing deposit in 3th monteh
            // other tenant
            l.Add(new Transaction(IBAN_DE, "06-06-2018", 194.39M - 60M * 2 - 130M));
            // other tenant
            l.Add(new Transaction(IBAN_NL2, "29-06-2018", 350M)); // Payed late, again 40€ are missing
            l.Add(new Transaction(IBAN_NL2, "29-06-2018", 212.44M - 2 * 80M));

            transactionData.Update();
            return transactionData;
        }

        private static BillRecordData BuildBillRecordData(IStorage storage, Tenants tenants)
        {
            // bill ranging to end of may
            var billRecordData = new BillRecordData(storage, tenants);
            var record = new BillRecord();
            record.CreationDateAsString = "16-05-2018";
            record.DueDateAsString = "16-06-2018";
            record.FromAsString = "2018";
            record.ToAsString = "15-05-2018";
            record.TotalCosts = 194.39M;
            record.TenantFile = "t1.xml";
            billRecordData.BillRecords.Add(record);
            record = new BillRecord();
            record.CreationDateAsString = "16-05-2018";
            record.DueDateAsString = "16-06-2018";
            record.FromAsString = "2018";
            record.ToAsString = "15-05-2018";
            record.TotalCosts = 212.44M;
            record.TenantFile = "t2.xml";
            billRecordData.BillRecords.Add(record);
            billRecordData.Update();
            return billRecordData;
        }

        private static SettlementManager BuildManager()
        {
            var storage = new FileStorage(new MemoryFileSystem());
            var tenants = BuildTenants(storage);
            var transactionData = BuildTransactions(storage, tenants);
            var billRecordData = BuildBillRecordData(storage, tenants);
            var dateProvider = new DateProviderMock("27-07-2018");
            return new SettlementManager(storage, tenants, transactionData, billRecordData, dateProvider);
        }

        private static Settlement GetSettlementsForTenantFile(SettlementManager manager, string file)
        {
            var access = new PrivateObject(manager);
            var dict = (Dictionary<Tenant, Settlement>)access.GetField("_Settlements");
            var tenants = (Tenants)access.GetField("_Tenants");
            var tenant = tenants.Where(p => p.Key == file).First().Value;
            return dict[tenant];
        }

        [TestMethod]
        public void SettlesScenarioForReliableTenantCorrectly()
        {
            var manager = BuildManager();
            var dict = GetSettlementsForTenantFile(manager, "t1.xml");
            var claims = dict.ElapsedClaims.ToList();
            var credits = dict.Credits.ToList();
            Assert.AreEqual(6, claims.Count);
            Assert.AreEqual(4, credits.Count);

            var deposit1 = claims[0] as DepositRateClaim;
            var rent1 = claims[1] as RentClaim;
            var deposit2 = claims[2] as DepositRateClaim;
            var rent2 = claims[3] as RentClaim;
            var depositRefund = claims[4] as DepositRefundClaim;
            var ancillaryBill = claims[5] as AncillaryBillClaim;

            var cDepositRent = credits[0];
            var cRent2 = credits[1];
            var cDepositRefund = credits[2];
            var cAncillaryRefund = credits[3];

            Assert.IsTrue(cDepositRent.IsDepleted);
            Assert.IsTrue(cRent2.IsDepleted);
            Assert.IsTrue(cDepositRefund.IsDepleted);
            Assert.IsTrue(cAncillaryRefund.IsDepleted);
            Assert.AreEqual(Role.TENANT, cDepositRent.Owner);
            Assert.AreEqual(Role.TENANT, cRent2.Owner);
            Assert.AreEqual(Role.LANDLORD, cDepositRefund.Owner);
            Assert.AreEqual(Role.LANDLORD, cAncillaryRefund.Owner);
            Utils.AssertDate("27-12-2017", cDepositRent.Transaction.Date);
            Utils.AssertDate("01-02-2018", cRent2.Transaction.Date);
            Utils.AssertDate("02-03-2018", cDepositRefund.Transaction.Date);
            Utils.AssertDate("06-06-2018", cAncillaryRefund.Transaction.Date);
            Assert.AreEqual(1040M, cDepositRent.Transaction.Amount);
            Assert.AreEqual(360M, cRent2.Transaction.Amount);
            Assert.AreEqual(-550M, cDepositRefund.Transaction.Amount);
            Assert.AreEqual(-55.61M, cAncillaryRefund.Transaction.Amount);

            Utils.AssertDate("20-12-2017", deposit1.AnnounceDate);
            Utils.AssertDate("20-12-2017", deposit2.AnnounceDate);
            Utils.AssertDate("20-12-2017", rent1.AnnounceDate);
            Utils.AssertDate("20-12-2017", rent2.AnnounceDate);
            Utils.AssertDate("28-02-2018", depositRefund.AnnounceDate);
            Utils.AssertDate("16-05-2018", ancillaryBill.AnnounceDate);
            Assert.IsTrue(deposit1.IsCovered);
            Assert.IsTrue(deposit2.IsCovered);
            Assert.IsTrue(rent1.IsCovered);
            Assert.IsTrue(rent2.IsCovered);
            Assert.IsTrue(depositRefund.IsCovered);
            Assert.IsTrue(ancillaryBill.IsCovered);
            Utils.AssertDate("01-01-2018", deposit1.CoveredAt.Value);
            Utils.AssertDate("03-02-2018", deposit2.CoveredAt.Value);
            Utils.AssertDate("03-01-2018", rent1.CoveredAt.Value);
            Utils.AssertDate("03-02-2018", rent2.CoveredAt.Value);
            Utils.AssertDate("28-03-2018", depositRefund.CoveredAt.Value);
            Utils.AssertDate("16-06-2018", ancillaryBill.CoveredAt.Value);
            Utils.AssertDate("01-01-2018", deposit1.DueDate);
            Utils.AssertDate("03-02-2018", deposit2.DueDate);
            Utils.AssertDate("03-01-2018", rent1.DueDate);
            Utils.AssertDate("03-02-2018", rent2.DueDate);
            Utils.AssertDate("28-03-2018", depositRefund.DueDate);
            Utils.AssertDate("16-06-2018", ancillaryBill.DueDate);
            Assert.AreEqual(0M, rent1.Prepayment);
            Assert.AreEqual(0M, rent2.Prepayment);
            Assert.AreEqual(0M, depositRefund.Prepayment);

            var history = cDepositRent.History.ToList();
            Assert.AreEqual(3, history.Count);
            Assert.AreEqual(deposit1, history[0].Source);
            Assert.AreEqual(rent1, history[1].Source);
            Assert.AreEqual(deposit2, history[2].Source);
            Assert.AreEqual(340M, history[0].Amount);
            Assert.AreEqual(360M, history[1].Amount);
            Assert.AreEqual(340M, history[2].Amount);
            var entries = cDepositRent.TaxReportEntries.ToList();
            Assert.AreEqual(3, entries.Count);
            Utils.AssertDate("01-01-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(340M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(null, entries[0].AncillaryTax);
            Assert.AreEqual(340M, entries[0].FlowForDeposit);
            Utils.AssertDate("03-01-2018", entries[1].Due.Value);
            Assert.AreEqual(2018, entries[1].TaxYear);
            Assert.AreEqual(360M, entries[1].AssignedAmount);
            Assert.AreEqual(300M, entries[1].NetTax);
            Assert.AreEqual(60M, entries[1].AncillaryTax);
            Assert.AreEqual(0M, entries[1].FlowForDeposit);
            Utils.AssertDate("03-02-2018", entries[2].Due.Value);
            Assert.AreEqual(2018, entries[2].TaxYear);
            Assert.AreEqual(340M, entries[2].AssignedAmount);
            Assert.AreEqual(null, entries[2].NetTax);
            Assert.AreEqual(null, entries[2].AncillaryTax);
            Assert.AreEqual(340M, entries[2].FlowForDeposit);

            history = cRent2.History.ToList();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(rent2, history[0].Source);
            Assert.AreEqual(360M, history[0].Amount);
            entries = cRent2.TaxReportEntries.ToList();
            Assert.AreEqual(1, entries.Count);
            Utils.AssertDate("03-02-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(360M, entries[0].AssignedAmount);
            Assert.AreEqual(300M, entries[0].NetTax);
            Assert.AreEqual(60M, entries[0].AncillaryTax);
            Assert.AreEqual(0M, entries[0].FlowForDeposit);

            history = cDepositRefund.History.ToList();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(depositRefund, history[0].Source);
            Assert.AreEqual(550M, history[0].Amount);
            entries = cDepositRefund.TaxReportEntries.ToList();
            Assert.AreEqual(1, entries.Count);
            Utils.AssertDate("28-03-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(-550M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(null, entries[0].AncillaryTax);
            Assert.AreEqual(-550M, entries[0].FlowForDeposit);

            history = cAncillaryRefund.History.ToList();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(ancillaryBill, history[0].Source);
            Assert.AreEqual(55.61M, history[0].Amount);
            entries = cAncillaryRefund.TaxReportEntries.ToList();
            Assert.AreEqual(1, entries.Count);
            Utils.AssertDate("16-06-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(194.39M - 2 * 60M - 130M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(194.39M - 2 * 60M - 130M, entries[0].AncillaryTax);
            Assert.AreEqual(0M, entries[0].FlowForDeposit);

            history = deposit1.History.ToList();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(cDepositRent, history[0].Source);
            Assert.AreEqual(340M, history[0].Amount);

            history = deposit2.History.ToList();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(cDepositRent, history[0].Source);
            Assert.AreEqual(340M, history[0].Amount);

            history = rent1.History.ToList();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(cDepositRent, history[0].Source);
            Assert.AreEqual(ancillaryBill, history[1].Source);
            Assert.AreEqual(360M, history[0].Amount);
            Assert.AreEqual(-60M, history[1].Amount);

            history = rent2.History.ToList();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(cRent2, history[0].Source);
            Assert.AreEqual(ancillaryBill, history[1].Source);
            Assert.AreEqual(360M, history[0].Amount);
            Assert.AreEqual(-60M, history[1].Amount);

            history = depositRefund.History.ToList();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(cDepositRefund, history[0].Source);
            Assert.AreEqual(ancillaryBill, history[1].Source);
            Assert.AreEqual(-550M, history[0].Amount);
            Assert.AreEqual(-130M, history[1].Amount);

            history = ancillaryBill.History.ToList();
            Assert.AreEqual(4, history.Count);
            Assert.AreEqual(rent1, history[0].Source);
            Assert.AreEqual(rent2, history[1].Source);
            Assert.AreEqual(depositRefund, history[2].Source);
            Assert.AreEqual(cAncillaryRefund, history[3].Source);
            Assert.AreEqual(60M, history[0].Amount);
            Assert.AreEqual(60M, history[1].Amount);
            Assert.AreEqual(130M, history[2].Amount);
            Assert.AreEqual(-55.61M, history[3].Amount);
        }

        [TestMethod]
        public void SettlesScenarioForUnreliableTenantCorrectly()
        {
            var manager = BuildManager();
            var dict = GetSettlementsForTenantFile(manager, "t2.xml");
            var claims = dict.ElapsedClaims.ToList();
            var credits = dict.Credits.ToList();
            Assert.AreEqual(8, claims.Count);
            Assert.AreEqual(4, credits.Count);

            var deposit1 = claims[0] as DepositRateClaim;
            var rent1 = claims[1] as RentClaim;
            var deposit2 = claims[2] as DepositRateClaim;
            var rent2 = claims[3] as RentClaim;
            var deposit3 = claims[4] as DepositRateClaim;
            var rent3 = claims[5] as RentClaim;
            var ancillaryBill = claims[6] as AncillaryBillClaim;
            var rent4 = claims[7] as RentClaim;

            var cDepositRent1 = credits[0];
            var cDepositRent2 = credits[1];
            var cPartialRent3 = credits[2];
            var cAncillaryBalance = credits[3];

            Assert.IsTrue(cDepositRent1.IsDepleted);
            Assert.IsTrue(cDepositRent2.IsDepleted);
            Assert.IsTrue(cPartialRent3.IsDepleted);
            Assert.IsTrue(cAncillaryBalance.IsDepleted);
            Assert.AreEqual(Role.TENANT, cDepositRent1.Owner);
            Assert.AreEqual(Role.TENANT, cDepositRent2.Owner);
            Assert.AreEqual(Role.TENANT, cPartialRent3.Owner);
            Assert.AreEqual(Role.TENANT, cAncillaryBalance.Owner);
            Utils.AssertDate("29-03-2018", cDepositRent1.Transaction.Date);
            Utils.AssertDate("02-05-2018", cDepositRent2.Transaction.Date);
            Utils.AssertDate("29-06-2018", cPartialRent3.Transaction.Date);
            Utils.AssertDate("29-06-2018", cAncillaryBalance.Transaction.Date);
            Assert.AreEqual(730M, cDepositRent1.Transaction.Amount);
            Assert.AreEqual(690M, cDepositRent2.Transaction.Amount);
            Assert.AreEqual(350M, cPartialRent3.Transaction.Amount);
            Assert.AreEqual(52.44M, cAncillaryBalance.Transaction.Amount);

            Utils.AssertDate("13-03-2018", deposit1.AnnounceDate);
            Utils.AssertDate("13-03-2018", deposit2.AnnounceDate);
            Utils.AssertDate("13-03-2018", deposit3.AnnounceDate);
            Utils.AssertDate("13-03-2018", rent1.AnnounceDate);
            Utils.AssertDate("13-03-2018", rent2.AnnounceDate);
            Utils.AssertDate("13-03-2018", rent3.AnnounceDate);
            Utils.AssertDate("13-03-2018", rent4.AnnounceDate);
            Utils.AssertDate("16-05-2018", ancillaryBill.AnnounceDate);

            Assert.IsTrue(deposit1.IsCovered);
            Assert.IsTrue(deposit2.IsCovered);
            Assert.IsFalse(deposit3.IsCovered);
            Assert.IsTrue(rent1.IsCovered);
            Assert.IsTrue(rent2.IsCovered);
            Assert.IsFalse(rent3.IsCovered);
            Assert.IsFalse(rent4.IsCovered);
            Assert.IsTrue(ancillaryBill.IsCovered);
            Utils.AssertDate("01-04-2018", deposit1.CoveredAt.Value);
            Utils.AssertDate("03-05-2018", deposit2.CoveredAt.Value);
            Utils.AssertDate("03-04-2018", rent1.CoveredAt.Value);
            Utils.AssertDate("03-05-2018", rent2.CoveredAt.Value);
            Utils.AssertDate("29-06-2018", ancillaryBill.CoveredAt.Value);
            Assert.AreEqual(40M, deposit3.OpenAmount);
            Assert.AreEqual(40M, rent3.OpenAmount);
            Assert.AreEqual(390M, rent4.OpenAmount);
            Utils.AssertDate("01-04-2018", deposit1.DueDate);
            Utils.AssertDate("03-05-2018", deposit2.DueDate);
            Utils.AssertDate("03-06-2018", deposit3.DueDate);
            Utils.AssertDate("03-04-2018", rent1.DueDate);
            Utils.AssertDate("03-05-2018", rent2.DueDate);
            Utils.AssertDate("03-06-2018", rent3.DueDate);
            Utils.AssertDate("03-07-2018", rent4.DueDate);
            Utils.AssertDate("16-06-2018", ancillaryBill.DueDate);
            Assert.AreEqual(0M, rent1.Prepayment);
            Assert.AreEqual(0M, rent2.Prepayment);
            Assert.AreEqual(80M, rent3.Prepayment);
            Assert.AreEqual(80M, rent4.Prepayment);

            var history = cDepositRent1.History.ToList();
            Assert.AreEqual(3, history.Count());
            Assert.AreEqual(deposit1, history[0].Source);
            Assert.AreEqual(rent1, history[1].Source);
            Assert.AreEqual(deposit2, history[2].Source);
            Assert.AreEqual(226M, history[0].Amount);
            Assert.AreEqual(390M, history[1].Amount);
            Assert.AreEqual(114M, history[2].Amount);
            var entries = cDepositRent1.TaxReportEntries.ToList();
            Assert.AreEqual(3, entries.Count());
            Utils.AssertDate("01-04-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(226M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(null, entries[0].AncillaryTax);
            Assert.AreEqual(226M, entries[0].FlowForDeposit);
            Utils.AssertDate("03-04-2018", entries[1].Due.Value);
            Assert.AreEqual(2018, entries[1].TaxYear);
            Assert.AreEqual(390M, entries[1].AssignedAmount);
            Assert.AreEqual(310M, entries[1].NetTax);
            Assert.AreEqual(80M, entries[1].AncillaryTax);
            Assert.AreEqual(0M, entries[1].FlowForDeposit);
            Utils.AssertDate("03-05-2018", entries[2].Due.Value);
            Assert.AreEqual(2018, entries[2].TaxYear);
            Assert.AreEqual(114M, entries[2].AssignedAmount);
            Assert.AreEqual(null, entries[2].NetTax);
            Assert.AreEqual(null, entries[2].AncillaryTax);
            Assert.AreEqual(114M, entries[2].FlowForDeposit);

            history = cDepositRent2.History.ToList();
            Assert.AreEqual(3, history.Count());
            Assert.AreEqual(deposit2, history[0].Source);
            Assert.AreEqual(rent2, history[1].Source);
            Assert.AreEqual(deposit3, history[2].Source);
            Assert.AreEqual(112M, history[0].Amount);
            Assert.AreEqual(390M, history[1].Amount);
            Assert.AreEqual(188M, history[2].Amount);
            entries = cDepositRent2.TaxReportEntries.ToList();
            Assert.AreEqual(3, entries.Count());
            Utils.AssertDate("03-05-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(112M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(null, entries[0].AncillaryTax);
            Assert.AreEqual(112M, entries[0].FlowForDeposit);
            Utils.AssertDate("03-05-2018", entries[1].Due.Value);
            Assert.AreEqual(2018, entries[1].TaxYear);
            Assert.AreEqual(390M, entries[1].AssignedAmount);
            Assert.AreEqual(310M, entries[1].NetTax);
            Assert.AreEqual(80M, entries[1].AncillaryTax);
            Assert.AreEqual(0M, entries[1].FlowForDeposit);
            Utils.AssertDate("03-06-2018", entries[2].Due.Value);
            Assert.AreEqual(2018, entries[2].TaxYear);
            Assert.AreEqual(188M, entries[2].AssignedAmount);
            Assert.AreEqual(null, entries[2].NetTax);
            Assert.AreEqual(null, entries[2].AncillaryTax);
            Assert.AreEqual(188M, entries[2].FlowForDeposit);

            history = cPartialRent3.History.ToList();
            Assert.AreEqual(2, history.Count());
            Assert.AreEqual(ancillaryBill, history[0].Source);
            Assert.AreEqual(rent3, history[1].Source);
            Assert.AreEqual(52.44M, history[0].Amount);
            Assert.AreEqual(297.56M, history[1].Amount);
            entries = cPartialRent3.TaxReportEntries.ToList();
            Assert.AreEqual(2, entries.Count());
            Utils.AssertDate("16-06-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(52.44M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(52.44M, entries[0].AncillaryTax);
            Assert.AreEqual(0M, entries[0].FlowForDeposit);
            Utils.AssertDate("03-06-2018", entries[1].Due.Value);
            Assert.AreEqual(2018, entries[1].TaxYear);
            Assert.AreEqual(297.56M, entries[1].AssignedAmount);
            Assert.AreEqual(297.56M, entries[1].NetTax);
            Assert.AreEqual(0M, entries[1].AncillaryTax);
            Assert.AreEqual(0M, entries[1].FlowForDeposit);

            history = cAncillaryBalance.History.ToList();
            Assert.AreEqual(1, history.Count());
            Assert.AreEqual(rent3, history[0].Source);
            Assert.AreEqual(52.44M, history[0].Amount);
            entries = cAncillaryBalance.TaxReportEntries.ToList();
            Assert.AreEqual(1, entries.Count());
            Utils.AssertDate("03-06-2018", entries[0].Due.Value);
            Assert.AreEqual(2018, entries[0].TaxYear);
            Assert.AreEqual(52.44M, entries[0].AssignedAmount);
            Assert.AreEqual(12.44M, entries[0].NetTax);
            Assert.AreEqual(40M, entries[0].AncillaryTax);
            Assert.AreEqual(0M, entries[0].FlowForDeposit);

            history = deposit1.History.ToList();
            Assert.AreEqual(1, history.Count());
            Assert.AreEqual(cDepositRent1, history[0].Source);
            Assert.AreEqual(226M, history[0].Amount);

            history = deposit2.History.ToList();
            Assert.AreEqual(2, history.Count());
            Assert.AreEqual(cDepositRent1, history[0].Source);
            Assert.AreEqual(cDepositRent2, history[1].Source);
            Assert.AreEqual(114M, history[0].Amount);
            Assert.AreEqual(112M, history[1].Amount);

            history = deposit3.History.ToList();
            Assert.AreEqual(1, history.Count());
            Assert.AreEqual(cDepositRent2, history[0].Source);
            Assert.AreEqual(188M, history[0].Amount);

            history = rent1.History.ToList();
            Assert.AreEqual(2, history.Count());
            Assert.AreEqual(cDepositRent1, history[0].Source);
            Assert.AreEqual(ancillaryBill, history[1].Source);
            Assert.AreEqual(390M, history[0].Amount);
            Assert.AreEqual(-80M, history[1].Amount);

            history = rent2.History.ToList();
            Assert.AreEqual(2, history.Count());
            Assert.AreEqual(cDepositRent2, history[0].Source);
            Assert.AreEqual(ancillaryBill, history[1].Source);
            Assert.AreEqual(390M, history[0].Amount);
            Assert.AreEqual(-80M, history[1].Amount);

            history = rent3.History.ToList();
            Assert.AreEqual(2, history.Count());
            Assert.AreEqual(cPartialRent3, history[0].Source);
            Assert.AreEqual(cAncillaryBalance, history[1].Source);
            Assert.AreEqual(297.56M, history[0].Amount);
            Assert.AreEqual(52.44M, history[1].Amount);

            history = rent4.History.ToList();
            Assert.AreEqual(0, history.Count());

            history = ancillaryBill.History.ToList();
            Assert.AreEqual(3, history.Count());
            Assert.AreEqual(rent1, history[0].Source);
            Assert.AreEqual(rent2, history[1].Source);
            Assert.AreEqual(cPartialRent3, history[2].Source);
            Assert.AreEqual(80M, history[0].Amount);
            Assert.AreEqual(80M, history[1].Amount);
            Assert.AreEqual(52.44M, history[2].Amount);
        }
    }
}
