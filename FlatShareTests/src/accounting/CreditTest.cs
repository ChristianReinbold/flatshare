using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class CreditTest
    {
        private static Claim CreateAClaim()
        {
            return new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 400M, 100M);
        }

        [TestMethod]
        public void CorrectTaxYearFor10TageRule()
        {
            var transaction1 = new Transaction(IBANTest.GetValidIBAN(), "21-12-2017", 500M);
            var transaction2 = new Transaction(IBANTest.GetValidIBAN(), "22-12-2017", 500M);
            var transaction3 = new Transaction(IBANTest.GetValidIBAN(), "10-01-2018", 500M);
            var transaction4 = new Transaction(IBANTest.GetValidIBAN(), "11-01-2018", 500M);
            var credit1 = new Credit(transaction1);
            var credit2 = new Credit(transaction2);
            var credit3 = new Credit(transaction3);
            var credit4 = new Credit(transaction4);

            var rent2018 = new MonthlyRent(400M, 100M, 3, "2018", "2018", "01-01-2018");
            var rent2017 = new MonthlyRent(400M, 100M, 3, "12-2017", "2017", "01-12-2017");
            var claim1 = RentClaim.FromRent(rent2018, DateUtils.DateFromString("10-01-2018")).Single();
            var claim2 = RentClaim.FromRent(rent2018, DateUtils.DateFromString("10-01-2018")).Single();
            var claim3 = RentClaim.FromRent(rent2017, DateUtils.DateFromString("10-12-2017")).Single();
            var claim4 = RentClaim.FromRent(rent2017, DateUtils.DateFromString("10-12-2017")).Single();

            claim1.BalanceWith(credit1, DateUtils.DateFromString("15-01-2018"));
            claim2.BalanceWith(credit2, DateUtils.DateFromString("15-01-2018"));
            claim3.BalanceWith(credit3, DateUtils.DateFromString("15-01-2018"));
            claim4.BalanceWith(credit4, DateUtils.DateFromString("15-01-2018"));

            Assert.AreEqual(2017, credit1.TaxReportEntries.Single().TaxYear);
            Assert.AreEqual(2018, credit2.TaxReportEntries.Single().TaxYear);
            Assert.AreEqual(2017, credit3.TaxReportEntries.Single().TaxYear);
            Assert.AreEqual(2018, credit4.TaxReportEntries.Single().TaxYear);
        }

        [TestMethod]
        public void CorrectTaxReportForCredit()
        {
            var transaction = new Transaction(IBANTest.GetValidIBAN(), "05-01-2018", 1450M);
            var transaction2 = new Transaction(IBANTest.GetValidIBAN(), "05-01-2018", -500M);
            var credit = new Credit(transaction);
            var credit2 = new Credit(transaction2);

            var rent = new MonthlyRent(400M, 100M, 3, "2018", "2018", "01-01-2018");
            var claims = RentClaim.FromRent(rent, DateUtils.DateFromString("31-12-2018")).ToList();
            var c1 = claims[0];
            var c2 = claims[1];

            c1.BalanceWith(credit, DateUtils.DateFromString("05-01-2018"));
            credit.BalanceWith(credit2, DateUtils.DateFromString("06-01-2018"));
            c2.BalanceWith(credit, DateUtils.DateFromString("07-01-2018"));

            var entries = credit.TaxReportEntries.ToList();
            Assert.AreEqual(3, entries.Count);

            Utils.AssertDate("03-01-2018", entries[0].Due.Value);
            Assert.AreEqual(500M, entries[0].AssignedAmount);
            Assert.AreEqual(400M, entries[0].NetTax);
            Assert.AreEqual(100M, entries[0].AncillaryTax);
            Assert.AreEqual(0M, entries[0].FlowForDeposit);

            Assert.AreEqual(false, entries[1].Due.HasValue);
            Assert.AreEqual(500M, entries[1].AssignedAmount);
            Assert.AreEqual(null, entries[1].NetTax);
            Assert.AreEqual(null, entries[1].AncillaryTax);
            Assert.AreEqual(0M, entries[1].FlowForDeposit);

            Utils.AssertDate("03-02-2018", entries[2].Due.Value);
            Assert.AreEqual(450M, entries[2].AssignedAmount);
            Assert.AreEqual(400M, entries[2].NetTax);
            Assert.AreEqual(50M, entries[2].AncillaryTax);
            Assert.AreEqual(0M, entries[2].FlowForDeposit);

            entries = credit2.TaxReportEntries.ToList();
            Assert.AreEqual(1, entries.Count);

            Assert.AreEqual(false, entries[0].Due.HasValue);
            Assert.AreEqual(-500M, entries[0].AssignedAmount);
            Assert.AreEqual(null, entries[0].NetTax);
            Assert.AreEqual(null, entries[0].AncillaryTax);
            Assert.AreEqual(0M, entries[0].FlowForDeposit);
        }

        [TestMethod]
        public void CorrectHistoryForCredit()
        {
            var transaction = new Transaction(IBANTest.GetValidIBAN(), "05-01-2018", 100M);
            var credit = new Credit(transaction);
            var c1 = CreateAClaim();
            var c2 = CreateAClaim();
            var c3 = CreateAClaim();

            credit.Withdraw(30M, c1);
            credit.Withdraw(30M, c2);
            credit.Withdraw(50M, c1);
            credit.Withdraw(120M, c3);

            var entries = credit.History.ToList();
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(c1, entries[0].Source);
            Assert.AreEqual(30M, entries[0].Amount);
            Assert.AreEqual(c2, entries[1].Source);
            Assert.AreEqual(30M, entries[1].Amount);
            Assert.AreEqual(c1, entries[2].Source);
            Assert.AreEqual(40M, entries[2].Amount);
        }

        [TestMethod]
        public void CorrectlyWithdrawsFromCredit()
        {
            var transaction = new Transaction(IBANTest.GetValidIBAN(), "05-01-2018", 100M);
            var credit = new Credit(transaction);

            Assert.AreEqual(40M, credit.Withdraw(40M, null));
            Assert.AreEqual(60M, credit.Remaining);
            Assert.IsFalse(credit.IsDepleted);
            Assert.AreEqual(60M, credit.Withdraw(800M, null));
            Assert.AreEqual(0M, credit.Remaining);
            Assert.IsTrue(credit.IsDepleted);
        }

        [TestMethod]
        public void CorrectlyInitializesCreditOfTenant()
        {
            var transaction = new Transaction(IBANTest.GetValidIBAN(), "05-01-2018", 100M);
            var credit = new Credit(transaction);
            Assert.IsFalse(credit.IsDepleted);
            Assert.AreEqual(Role.TENANT, credit.Owner);
            Assert.AreEqual(100M, credit.Remaining);
            Assert.AreEqual(transaction, credit.Transaction);
        }

        [TestMethod]
        public void CorrectlyInitializesCreditOfLandlort()
        {
            var transaction = new Transaction(IBANTest.GetValidIBAN(), "05-01-2018", -100M);
            var credit = new Credit(transaction);
            Assert.IsFalse(credit.IsDepleted);
            Assert.AreEqual(Role.LANDLORD, credit.Owner);
            Assert.AreEqual(100M, credit.Remaining);
            Assert.AreEqual(transaction, credit.Transaction);
        }

        [TestMethod]
        public void DoesNotBalanceCreditsOfSameOwner()
        {
            Credit c1 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 100M));
            Credit c2 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 90M));
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));
            Assert.AreEqual(100M, c1.Remaining);
            Assert.AreEqual(90M, c2.Remaining);
        }

        [TestMethod]
        public void BalancesCreditWithSmallerCredit()
        {
            Credit c1 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 100M));
            Credit c2 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", -90M));
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));
            Assert.AreEqual(10M, c1.Remaining);
            Assert.IsTrue(c2.IsDepleted);
        }

        [TestMethod]
        public void BalancesCreditWithLargerCredit()
        {
            Credit c1 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 90M));
            Credit c2 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", -100M));
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));
            Assert.IsTrue(c1.IsDepleted);
            Assert.AreEqual(10M, c2.Remaining);
        }

        [TestMethod]
        public void BalancesCreditWithExactCredit()
        {
            Credit c1 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", -90M));
            Credit c2 = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 90M));
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));
            Assert.IsTrue(c1.IsDepleted);
            Assert.IsTrue(c2.IsDepleted);
        }
    }
}
