using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;


namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class SettlementTest
    {
        private static DateTime AsDate(string s)
        {
            return DateUtils.DateFromString(s);
        }

        [TestMethod]
        public void SettlesScenario1Correctly()
        {
            var claim1 = new ClaimWithPrepaymentImpl("01-01-2018", "04-01-2018", 50M, 50M);
            var credit1 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("02-01-2018"), 80M));
            var claim2 = new ClaimWithPrepaymentImpl("03-01-2018", "05-01-2018", 100M, 50M);
            var credit2 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("06-01-2018"), 90M));

            var record = new BillRecord();
            record.CreationDateAsString = "07-01-2018";
            record.DueDateAsString = "07-01-2018";
            record.FromAsString = "04-01-2018";
            record.ToAsString = "05-01-2018";
            record.TotalCosts = 10M;
            Claim claim3 = new AncillaryBillClaim(record);

            var credit3 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("08-01-2018"), -60M));
            var credit4 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("09-01-2018"), 50M));

            var s = new Settlement(new[] { claim1, claim2, claim3 }, new[] { credit1, credit2, credit3, credit4 });

            // Check first day
            s.SettleUpTo(AsDate("02-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            var claims = s.Claims.ToList();
            var futClaims = s.FutureClaims.ToList();
            var credits = s.Credits.ToList();
            Assert.AreEqual(1, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(0, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim1, futClaims[0]);
            Assert.AreEqual(100M, claims[0].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[0].CurrentDebtor);

            // Check second day
            s.SettleUpTo(AsDate("03-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(1, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(1, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim1, futClaims[0]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreEqual(100M, claims[0].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[0].CurrentDebtor);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsFalse(credits[0].IsDepleted);

            // Check third day
            s.SettleUpTo(AsDate("04-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(2, claims.Count);
            Assert.AreEqual(2, futClaims.Count);
            Assert.AreEqual(1, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim1, futClaims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim2, futClaims[1]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreEqual(100M, claims[0].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[0].CurrentDebtor);
            Assert.AreEqual(150M, claims[1].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[1].CurrentDebtor);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsFalse(credits[0].IsDepleted);

            // Check 4. day
            s.SettleUpTo(AsDate("05-01-2018"));
            Assert.AreEqual(20M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(2, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(1, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim2, futClaims[0]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreEqual(20M, claims[0].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[0].CurrentDebtor);
            Assert.AreEqual(150M, claims[1].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[1].CurrentDebtor);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);

            // Check 5. day
            s.SettleUpTo(AsDate("06-01-2018"));
            Assert.AreEqual(170M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(2, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(1, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreEqual(20M, claims[0].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[0].CurrentDebtor);
            Assert.AreEqual(150M, claims[1].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[1].CurrentDebtor);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);

            // Check 6. day
            s.SettleUpTo(AsDate("07-01-2018"));
            Assert.AreEqual(80M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(2, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(2, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.AreEqual(20M, claims[0].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[0].CurrentDebtor);
            Assert.AreEqual(60M, claims[1].OpenAmount);
            Assert.AreEqual(Role.TENANT, claims[1].CurrentDebtor);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.TENANT, credits[1].Owner);
            Assert.IsTrue(credits[1].IsDepleted);

            // Check 7. day
            s.SettleUpTo(AsDate("08-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(10M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(2, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.IsTrue(claims[0].IsCovered);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.AreEqual(10M, claims[2].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[2].CurrentDebtor);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.TENANT, credits[1].Owner);
            Assert.IsTrue(credits[1].IsDepleted);

            // Check 8. day
            s.SettleUpTo(AsDate("09-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(3, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.AreSame(credit3, credits[2]);
            Assert.IsTrue(claims[0].IsCovered);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.IsTrue(claims[2].IsCovered);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.TENANT, credits[1].Owner);
            Assert.IsTrue(credits[1].IsDepleted);
            Assert.AreEqual(Role.LANDLORD, credits[2].Owner);
            Assert.AreEqual(50M, credits[2].Remaining);

            // Check 9. day
            s.SettleUpTo(AsDate("10-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(4, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.AreSame(credit3, credits[2]);
            Assert.AreSame(credit4, credits[3]);
            Assert.IsTrue(claims[0].IsCovered);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.IsTrue(claims[2].IsCovered);
            Assert.AreEqual(Role.TENANT, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.TENANT, credits[1].Owner);
            Assert.IsTrue(credits[1].IsDepleted);
            Assert.AreEqual(Role.LANDLORD, credits[2].Owner);
            Assert.IsTrue(credits[2].IsDepleted);
            Assert.AreEqual(Role.TENANT, credits[3].Owner);
            Assert.IsTrue(credits[3].IsDepleted);
        }

        [TestMethod]
        public void SettlesScenario2Correctly()
        {
            var claim1 = new ClaimImpl("01-01-2018", "01-01-2018", -20M);
            var claim2 = new ClaimImpl("02-01-2018", "02-01-2018", -30M);
            var claim3 = new ClaimImpl("03-01-2018", "08-01-2018", -50M);
            var credit1 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("04-01-2018"), -40M));
            var credit2 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("05-01-2018"), -70M));
            var credit3 = new Credit(new Transaction(IBANTest.GetValidIBAN(), AsDate("06-01-2018"), -40M));
            var claim4 = new ClaimImpl("07-01-2018", "08-01-2018", -50M);
            var s = new Settlement(new[] { claim1, claim2, claim3, claim4 }, new[] { credit1, credit2, credit3 });

            // Check first day
            s.SettleUpTo(AsDate("02-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(20M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            var claims = s.Claims.ToList();
            var futClaims = s.FutureClaims.ToList();
            var credits = s.Credits.ToList();
            Assert.AreEqual(1, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(0, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreEqual(20M, claims[0].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[0].CurrentDebtor);

            // Check 2. day
            s.SettleUpTo(AsDate("03-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(2, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(0, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreEqual(20M, claims[0].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[0].CurrentDebtor);
            Assert.AreEqual(30M, claims[1].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[1].CurrentDebtor);

            // Check 3. day
            s.SettleUpTo(AsDate("04-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(0, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(claim3, futClaims[0]);
            Assert.AreEqual(20M, claims[0].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[0].CurrentDebtor);
            Assert.AreEqual(30M, claims[1].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[1].CurrentDebtor);
            Assert.AreEqual(50M, claims[2].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[2].CurrentDebtor);

            // Check 4. day
            s.SettleUpTo(AsDate("05-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(10M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(1, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(claim3, futClaims[0]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreEqual(10M, claims[0].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[0].CurrentDebtor);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.AreEqual(50M, claims[2].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, claims[2].CurrentDebtor);
            Assert.AreEqual(Role.LANDLORD, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);

            // Check 5. day
            s.SettleUpTo(AsDate("06-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(10M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(2, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(claim3, futClaims[0]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.IsTrue(claims[0].IsCovered);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.AreEqual(50M, claims[2].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.LANDLORD, credits[1].Owner);
            Assert.AreEqual(60M, credits[1].Remaining);

            // Check 6. day
            s.SettleUpTo(AsDate("07-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(3, claims.Count);
            Assert.AreEqual(1, futClaims.Count);
            Assert.AreEqual(3, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(claim3, futClaims[0]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.AreSame(credit3, credits[2]);
            Assert.IsTrue(claims[0].IsCovered);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.AreEqual(50M, claims[2].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.LANDLORD, credits[1].Owner);
            Assert.AreEqual(60M, credits[1].Remaining);
            Assert.AreEqual(Role.LANDLORD, credits[2].Owner);
            Assert.AreEqual(40M, credits[2].Remaining);

            // Check 7. day
            s.SettleUpTo(AsDate("08-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(4, claims.Count);
            Assert.AreEqual(2, futClaims.Count);
            Assert.AreEqual(3, credits.Count);
            Assert.AreSame(claim1, claims[0]);
            Assert.AreSame(claim2, claims[1]);
            Assert.AreSame(claim3, claims[2]);
            Assert.AreSame(claim4, claims[3]);
            Assert.AreSame(claim3, futClaims[0]);
            Assert.AreSame(claim4, futClaims[1]);
            Assert.AreSame(credit1, credits[0]);
            Assert.AreSame(credit2, credits[1]);
            Assert.AreSame(credit3, credits[2]);
            Assert.IsTrue(claims[0].IsCovered);
            Assert.IsTrue(claims[1].IsCovered);
            Assert.AreEqual(50M, claims[2].OpenAmount);
            Assert.AreEqual(50M, claims[3].OpenAmount);
            Assert.AreEqual(Role.LANDLORD, credits[0].Owner);
            Assert.IsTrue(credits[0].IsDepleted);
            Assert.AreEqual(Role.LANDLORD, credits[1].Owner);
            Assert.AreEqual(60M, credits[1].Remaining);
            Assert.AreEqual(Role.LANDLORD, credits[2].Owner);
            Assert.AreEqual(40M, credits[2].Remaining);

            // Check 8. day
            s.SettleUpTo(AsDate("09-01-2018"));
            Assert.AreEqual(0M, s.GetOverdueAmountFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetOverdueAmountFor(Role.LANDLORD));
            Assert.AreEqual(0M, s.GetUnassignableCreditFor(Role.TENANT));
            Assert.AreEqual(50M, s.GetUnassignableCreditFor(Role.LANDLORD));
            claims = s.Claims.ToList();
            futClaims = s.FutureClaims.ToList();
            credits = s.Credits.ToList();
            Assert.AreEqual(4, claims.Count);
            Assert.AreEqual(0, futClaims.Count);
            Assert.AreEqual(3, credits.Count);
        }

        private struct IntermediateResult
        {
            public DateTime Date;
            public decimal Overdue;
            public decimal Unassignable;

            public IntermediateResult Next(decimal amount)
            {
                IntermediateResult newResult = new IntermediateResult();
                newResult.Date = Date.AddDays(1);
                if (amount >= 0)
                {
                    newResult.Unassignable = Unassignable;
                    newResult.Overdue = Overdue + amount;
                }
                else
                {
                    newResult.Unassignable = Unassignable + Math.Max(-amount - Overdue, 0);
                    newResult.Overdue = Math.Max(Overdue + amount, 0);
                }
                return newResult;
            }
        }

        private void CorrectRandomOverdueAmountAndRemainingCredit(Role role)
        {
            decimal roleFactor = role == Role.TENANT ? 1 : -1;

            DateTime date = DateUtils.DateFromString("01-01-2018");
            IntermediateResult result = new IntermediateResult();
            result.Date = date;
            result.Overdue = 0M;
            result.Unassignable = 0M;
            List<Claim> claims = new List<Claim>();
            List<Credit> credits = new List<Credit>();
            List<IntermediateResult> results = new List<IntermediateResult>();
            results.Add(result);

            int seed = new Random().Next(100000);
            Random r = new Random(seed);
            for (int i = 0; i < 1024; i++)
            {
                var preComma = r.Next(-100, 100);
                var postComma = r.Next(0, 100);
                var val = preComma + 0.01M * postComma;
                if (val >= 0)
                {
                    var prepayment = r.Next(0, preComma) + 0.01M * r.Next(0, postComma + 1);
                    if (role == Role.LANDLORD) prepayment = 0;
                    var dateAsString = DateUtils.DateAsString(result.Date);
                    var claim = new ClaimWithPrepaymentImpl(dateAsString, dateAsString, roleFactor * val - prepayment, prepayment);
                    claims.Add(claim);
                }
                else
                {
                    var transaction = new Transaction(IBANTest.GetValidIBAN(), result.Date, roleFactor * (-val));
                    credits.Add(new Credit(transaction));
                }
                result = result.Next(val);
                results.Add(result);
            }

            var settlement = new Settlement(claims, credits);
            foreach (var intermediate in results)
            {
                settlement.SettleUpTo(intermediate.Date);
                var msg = String.Format("seed = {0}, date = {1}", seed, intermediate.Date);
                Assert.AreEqual(intermediate.Overdue, settlement.GetOverdueAmountFor(role), msg);
                Assert.AreEqual(intermediate.Unassignable, settlement.GetUnassignableCreditFor(role), msg);
            }
        }

        [TestMethod]
        public void CorrectRandomOverdueAmountAndRemainingCreditForTenant()
        {
            CorrectRandomOverdueAmountAndRemainingCredit(Role.TENANT);
        }

        [TestMethod]
        public void CorrectRandomOverdueAmountAndRemainingCreditForLandlord()
        {
            CorrectRandomOverdueAmountAndRemainingCredit(Role.LANDLORD);
        }

        [TestMethod]
        public void CorrectlyWithdrawsFromCreditForClaimOnTheSameDay()
        {
            DateTime date = DateUtils.DateFromString("01-01-2018");
            Claim claim = new ClaimImpl("01-01-2018", "01-01-2018", 60M);
            Credit credit = new Credit(new Transaction(IBANTest.GetValidIBAN(), date, 100M));
            var settlement = new Settlement(claim.ToEnumerable(), credit.ToEnumerable());
            settlement.SettleUpTo(date.AddDays(1));
            Assert.AreEqual(40M, settlement.Credits.First().Remaining);
            Assert.IsTrue(settlement.Claims.First().IsCovered);
        }
    }
}
