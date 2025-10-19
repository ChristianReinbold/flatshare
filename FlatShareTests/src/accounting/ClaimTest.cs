using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare.Tests
{
    public class ClaimWithPrepaymentImpl : ClaimWithPrepayment
    {
        public ClaimWithPrepaymentImpl(string announced, string due, decimal fixedAmount, decimal prepayment)
            : base(DateUtils.DateFromString(announced), DateUtils.DateFromString(due), fixedAmount, prepayment, DateUtils.DateFromString(due))
        { }

        protected override TaxReportEntry CreateTaxReportEntry(decimal assignedAmount)
        {
            return new TaxReportEntry();
        }
    }

    public class ClaimImpl : Claim
    {
        public ClaimImpl(string announced, string due, decimal fixedAmount)
            : base(DateUtils.DateFromString(announced), DateUtils.DateFromString(due), fixedAmount)
        { }

        public void CollectPrepayment(ClaimWithPrepaymentImpl other, DateTime date)
        {
            var withdrawnAmount = other.WithdrawPrepayment(this, date);
            Assign(withdrawnAmount, other, date);
        }

        protected override TaxReportEntry CreateTaxReportEntry(decimal assignedAmount)
        {
            return new TaxReportEntry();
        }
    }

    [TestClass]
    public class ClaimTest
    {
        [TestMethod]
        public void CorrectClaimsFromRent()
        {
            var rent = new MonthlyRent(410M, 110M, 3, "04-2018", "07-2018", "04-03-2018");
            rent.CheckIntegrity();

            var claims1 = RentClaim.FromRent(rent, DateUtils.DateFromString("03-05-2018")).ToList();
            var claims2 = RentClaim.FromRent(rent, DateUtils.DateFromString("02-07-2018")).ToList();
            var claims3 = RentClaim.FromRent(rent, DateUtils.DateFromString("12-12-2020")).ToList();

            Assert.AreEqual(claims1.Count, 2);
            Assert.AreEqual(claims2.Count, 3);
            Assert.AreEqual(claims3.Count, 4);

            var allClaims = claims1.Concat(claims2).Concat(claims3);
            foreach (var c in allClaims) Utils.AssertDate("04-03-2018", c.AnnounceDate);
            foreach (var c in allClaims) Assert.AreEqual(c.CoveredAt, null);
            foreach (var c in allClaims) Assert.AreEqual(c.OpenAmount, 520M);
            foreach (var c in allClaims) Assert.AreEqual(c.CurrentDebtor, Role.TENANT);
            foreach (var c in allClaims) Assert.AreEqual(c.InitialDebtor, Role.TENANT);
            foreach (var c in allClaims) Assert.AreEqual(c.LastDebtor, Role.TENANT);
            foreach (var c in allClaims) Assert.AreEqual(c.IsCovered, false);
            Utils.AssertDate("03-04-2018", claims1[0].DueDate);
            Utils.AssertDate("03-05-2018", claims1[1].DueDate);
            Utils.AssertDate("03-04-2018", claims2[0].DueDate);
            Utils.AssertDate("03-05-2018", claims2[1].DueDate);
            Utils.AssertDate("03-06-2018", claims2[2].DueDate);
            Utils.AssertDate("03-04-2018", claims3[0].DueDate);
            Utils.AssertDate("03-05-2018", claims3[1].DueDate);
            Utils.AssertDate("03-06-2018", claims3[2].DueDate);
            Utils.AssertDate("03-07-2018", claims3[3].DueDate);
        }

        [TestMethod]
        public void CorrectClaimsFromBill()
        {
            var record = new BillRecord();
            record.CreationDateAsString = "07-07-2018";
            record.DueDateAsString = "07-08-2018";
            record.FromAsString = "2018";
            record.ToAsString = "06-2018";
            record.RemainingCosts = 25.24M;
            record.TotalCosts = 213.55M;
            record.Warnings = 1;
            record.TenantFile = "myTenant.xml";
            record.CheckIntegrity();

            var claim = new AncillaryBillClaim(record);
            Utils.AssertDate("07-07-2018", claim.AnnounceDate);
            Assert.AreEqual(claim.CoveredAt, null);
            Assert.AreEqual(claim.OpenAmount, 213.55M);
            Assert.AreEqual(claim.CurrentDebtor, Role.TENANT);
            Assert.AreEqual(claim.InitialDebtor, Role.TENANT);
            Assert.AreEqual(claim.LastDebtor, Role.TENANT);
            Assert.AreEqual(claim.IsCovered, false);
        }

        private static Tenant GetTenantWithDeposit(bool onlyOneMonth = false, bool repayDeposit = true)
        {
            var tenant = new Tenant("Jana", "Dana", Tenant.Genders.FEMALE);
            tenant.Deposit = new Deposit(661.20M, repayDeposit ? (decimal?)110M : null, repayDeposit ? (decimal?)50M : null);
            tenant.Rents.Add(new MonthlyRent(100M, 0M, 5, "02-2018", onlyOneMonth ? "02-2018" : "06-2018", "03-01-2018"));
            tenant.RoomAllocations.Add(new RoomAllocation("ARoom", 1, "02-2018", onlyOneMonth ? "02-2018" : "06-2018"));

            tenant.CheckIntegrity();

            return tenant;
        }

        [TestMethod]
        public void NoClaimsFromDepositWithoutDeposit()
        {
            var tenant = GetTenantWithDeposit();
            tenant.Deposit = null;
            Assert.AreEqual(DepositRateClaim.FromTenant(tenant).Count(), 0);
            Assert.AreEqual(DepositRefundClaim.FromTenant(tenant).Count(), 0);
        }

        public void AssertClaimsFromDeposit(List<Claim> claims, bool onlyOneMonth = false, bool repayDeposit = true)
        {
            int rateCount = onlyOneMonth ? 1 : 3;

            Assert.AreEqual(claims.Count, rateCount + (repayDeposit ? 1 : 0));

            for (int i = 0; i < rateCount; i++)
            {
                var c = claims[i];
                Utils.AssertDate("03-01-2018", c.AnnounceDate);
                Assert.AreEqual(c.CoveredAt, null);
                Assert.AreEqual(c.CurrentDebtor, Role.TENANT);
                Assert.AreEqual(c.InitialDebtor, Role.TENANT);
                Assert.AreEqual(c.LastDebtor, Role.TENANT);
                Assert.AreEqual(c.IsCovered, false);
            }

            if (onlyOneMonth)
            {
                Assert.AreEqual(claims[0].OpenAmount, 661.20M);
                Utils.AssertDate("01-02-2018", claims[0].DueDate);
            }
            else
            {
                Assert.AreEqual(claims[0].OpenAmount, 220M);
                Utils.AssertDate("01-02-2018", claims[0].DueDate);
                Assert.AreEqual(claims[1].OpenAmount, 220M);
                Utils.AssertDate("05-03-2018", claims[1].DueDate);
                Assert.AreEqual(claims[2].OpenAmount, 221.20M);
                Utils.AssertDate("05-04-2018", claims[2].DueDate);
            }

            if (repayDeposit)
            {
                var repayClaim = claims[rateCount] as DepositRefundClaim;

                Utils.AssertDate(onlyOneMonth ? "28-02-2018" : "30-06-2018", repayClaim.AnnounceDate);
                Utils.AssertDate(onlyOneMonth ? "28-03-2018" : "30-07-2018", repayClaim.DueDate);

                Assert.AreEqual(repayClaim.CoveredAt, null);
                Assert.AreEqual(repayClaim.CurrentDebtor, Role.LANDLORD);
                Assert.AreEqual(repayClaim.InitialDebtor, Role.LANDLORD);
                Assert.AreEqual(repayClaim.LastDebtor, Role.LANDLORD);
                Assert.AreEqual(repayClaim.IsCovered, false);
                Assert.AreEqual(repayClaim.OpenAmount, 661.20M - 160M);
                Assert.AreEqual(repayClaim.CoveredPrepayment, 110M);
                Assert.AreEqual(repayClaim.WithdrawnPrepayment, 0M);
            }
        }

        [TestMethod]
        public void CorrectClaimsFromDepositOverMoreThanThreeMonths()
        {
            var tenant = GetTenantWithDeposit(false, false);
            List<Claim> claims = new List<Claim>();
            claims.AddRange(DepositRateClaim.FromTenant(tenant));
            claims.AddRange(DepositRefundClaim.FromTenant(tenant));
            claims.Sort(Comparer<Claim>.Create((c1, c2) => c1.AnnounceDate.CompareTo(c2.AnnounceDate)));
            AssertClaimsFromDeposit(claims, false, false);
        }

        [TestMethod]
        public void CorrectClaimsFromDepositOverMoreThanThreeMonthsWithRepay()
        {
            var tenant = GetTenantWithDeposit(false, true);
            List<Claim> claims = new List<Claim>();
            claims.AddRange(DepositRateClaim.FromTenant(tenant));
            claims.AddRange(DepositRefundClaim.FromTenant(tenant));
            claims.Sort(Comparer<Claim>.Create((c1, c2) => c1.AnnounceDate.CompareTo(c2.AnnounceDate)));
            AssertClaimsFromDeposit(claims, false, true);
        }

        [TestMethod]
        public void CorrectClaimsFromDepositOverOneMonth()
        {
            var tenant = GetTenantWithDeposit(true, false);
            List<Claim> claims = new List<Claim>();
            claims.AddRange(DepositRateClaim.FromTenant(tenant));
            claims.AddRange(DepositRefundClaim.FromTenant(tenant));
            claims.Sort(Comparer<Claim>.Create((c1, c2) => c1.AnnounceDate.CompareTo(c2.AnnounceDate)));
            AssertClaimsFromDeposit(claims, true, false);
        }

        [TestMethod]
        public void CorrectClaimsFromDepositOverOneMonthWithRepay()
        {
            var tenant = GetTenantWithDeposit(true, true);
            List<Claim> claims = new List<Claim>();
            claims.AddRange(DepositRateClaim.FromTenant(tenant));
            claims.AddRange(DepositRefundClaim.FromTenant(tenant));
            claims.Sort(Comparer<Claim>.Create((c1, c2) => c1.AnnounceDate.CompareTo(c2.AnnounceDate)));
            AssertClaimsFromDeposit(claims, true, true);
        }

        [TestMethod]
        public void CorrectlyBalancesClaimWithSufficientCredit()
        {
            var claim = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 300M, 30M);
            Credit credit = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 370M));

            claim.BalanceWith(credit, DateUtils.DateFromString("05-01-2018"));

            Assert.AreEqual(0M, claim.OpenAmount);
            Assert.IsTrue(claim.IsCovered);
            Assert.AreEqual(40M, credit.Remaining);
            Assert.IsFalse(credit.IsDepleted);
        }

        [TestMethod]
        public void CorrectlyBalancesClaimWithExactCredit()
        {
            var claim = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 300M, 30M);
            Credit credit = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 330M));

            claim.BalanceWith(credit, DateUtils.DateFromString("05-01-2018"));

            Assert.AreEqual(0M, claim.OpenAmount);
            Assert.IsTrue(claim.IsCovered);
            Assert.AreEqual(0M, credit.Remaining);
            Assert.IsTrue(credit.IsDepleted);
        }

        [TestMethod]
        public void CorrectlyBalancesClaimWithInsufficientCredit()
        {
            var claim = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 300M, 30M);
            Credit credit = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 230M));

            claim.BalanceWith(credit, DateUtils.DateFromString("05-01-2018"));

            Assert.AreEqual(100M, claim.OpenAmount);
            Assert.IsFalse(claim.IsCovered);
            Assert.AreEqual(0M, credit.Remaining);
            Assert.IsTrue(credit.IsDepleted);
        }

        [TestMethod]
        public void CorrectlyTransfersPrepaymentsToOtherClaim()
        {
            var c1 = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 300M, 100M);
            var c2 = new ClaimImpl("01-01-2018", "01-02-2018", 50M);
            Credit credit = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 370M));

            Assert.AreEqual(0M, c1.CoveredPrepayment);
            Assert.AreEqual(0M, c1.WithdrawnPrepayment);
            c1.BalanceWith(credit, DateUtils.DateFromString("05-01-2018"));
            Assert.AreEqual(70M, c1.CoveredPrepayment);
            Assert.AreEqual(0M, c1.WithdrawnPrepayment);
            c2.CollectPrepayment(c1, DateUtils.DateFromString("05-01-2018"));
            Assert.AreEqual(0M, c1.CoveredPrepayment);
            Assert.AreEqual(70M, c1.WithdrawnPrepayment);

            Utils.AssertDate("05-01-2018", c1.CoveredAt.Value);
            Assert.IsFalse(c1.CurrentDebtor.HasValue);
            Assert.IsTrue(c1.IsCovered);
            Assert.AreEqual(0M, c1.OpenAmount);

            Assert.IsFalse(c2.CoveredAt.HasValue);
            Assert.AreEqual(c2.CurrentDebtor, Role.LANDLORD);
            Assert.IsFalse(c2.IsCovered);
            Assert.AreEqual(20M, c2.OpenAmount);
        }

        [TestMethod]
        public void CorrectlyBalancesClaimWithSmallerClaim()
        {
            var c1 = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 300M, 100M);
            var c2 = new ClaimImpl("01-01-2018", "01-02-2018", -350M);
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));

            Assert.IsFalse(c1.IsCovered);
            Assert.IsTrue(c2.IsCovered);
            Assert.AreEqual(50M, c1.OpenAmount);
            Assert.AreEqual(Role.TENANT, c1.CurrentDebtor);
        }

        [TestMethod]
        public void CorrectlyBalancesClaimWithLargerClaim()
        {
            var c1 = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 200M, 100M);
            var c2 = new ClaimImpl("01-01-2018", "01-02-2018", -350M);
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));

            Assert.IsTrue(c1.IsCovered);
            Assert.IsFalse(c2.IsCovered);
            Assert.AreEqual(50M, c2.OpenAmount);
            Assert.AreEqual(Role.LANDLORD, c2.CurrentDebtor);
        }

        [TestMethod]
        public void CorrectlyBalancesClaimWithExactClaim()
        {
            var c1 = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 300M, 100M);
            var c2 = new ClaimImpl("01-01-2018", "01-02-2018", -400M);
            c1.BalanceWith(c2, DateUtils.DateFromString("05-01-2018"));

            Assert.IsTrue(c1.IsCovered);
            Assert.IsTrue(c2.IsCovered);
        }

        [TestMethod]
        public void CorrectHistoryForClaim()
        {
            var c1 = new ClaimWithPrepaymentImpl("01-01-2018", "01-02-2018", 400M, 100M);
            var c2 = new ClaimImpl("01-01-2018", "01-02-2018", -100M);
            var c3 = new ClaimImpl("01-01-2018", "01-02-2018", 120M);
            Credit credit = new Credit(new Transaction(IBANTest.GetValidIBAN(), "01-01-2018", 330M));

            c1.BalanceWith(credit, DateUtils.DateFromString("05-01-2018"));
            c1.BalanceWith(c2, DateUtils.DateFromString("07-01-2018"));
            c3.CollectPrepayment(c1, DateUtils.DateFromString("05-01-2018"));

            var entries = c1.History.ToList();
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(credit, entries[0].Source);
            Assert.AreEqual(330M, entries[0].Amount);
            Assert.AreEqual(c2, entries[1].Source);
            Assert.AreEqual(100M, entries[1].Amount);
            Assert.AreEqual(c3, entries[2].Source);
            Assert.AreEqual(-30M, entries[2].Amount);

            entries = c2.History.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(c1, entries[0].Source);
            Assert.AreEqual(-100M, entries[0].Amount);

            entries = credit.History.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(c1, entries[0].Source);
            Assert.AreEqual(330M, entries[0].Amount);

            entries = c3.History.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(c1, entries[0].Source);
            Assert.AreEqual(30M, entries[0].Amount);
        }
    }
}
