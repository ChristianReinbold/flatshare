using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class ExpenseTests
    {
        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenIntervalIsNonPositive()
        {
            var expense = new RepeatedExpense(10, Frequency.ONCE, "22-01-2018", "05-01-2018");
            Assert.ThrowsExactly<IntegrityException>(() => expense.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenOnceExpenseIsInfinite()
        {
            var expense = new RepeatedExpense(100, Frequency.ONCE, "22-01-2018");
            Assert.ThrowsExactly<IntegrityException>(() => expense.CheckIntegrity());
        }

        [TestMethod]
        public void ComputesAnnualRepetitions()
        {
            var expense = new RepeatedExpense(100, Frequency.ANNUAL, "2018", "30-06-2019");
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(expense.Interval), 181 / (decimal)365 * 1 + 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("01-01-2016", "31-12-2018")), 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("01-01-2019", "31-12-2020")), 181 / (decimal)365);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("05-12-2018", "31-12-2020")), 208 / (decimal)365);
            expense.ToAsString = "";
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("2018", "2020")), 3);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("2019", "2020")), 2);
        }

        [TestMethod]
        public void ComputesMonthlyRepetitions()
        {
            var expense = new RepeatedExpense(100, Frequency.MONTHLY, "2018", "10-02-2018");
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(expense.Interval), 10 / (decimal)28 + 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("01-01-2016", "31-01-2018")), 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("20-01-2018", "15-02-2018")), 12 / 31m + 10 / 28m);
            expense.ToAsString = "";
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("10-2017", "10-2019")), 22);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("06-2018", "2020")), 31);
        }

        [TestMethod]
        public void ComputesDailyRepetitions()
        {
            var expense = new RepeatedExpense(100, Frequency.DAILY, "15-12-2018", "2018");
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(expense.Interval), 17);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("01-01-2018", "31-01-2020")), 17);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("10-12-2018", "20-12-2018")), 6);
            expense.ToAsString = "";
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("05-02-2020", "05-02-2020")), 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("10-12-2018", "20-12-2018")), 6);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("05-02-2000", "05-02-2000")), 0);
        }

        [TestMethod]
        public void ComputesFractionalRepetition()
        {
            var expense = new RepeatedExpense(100, Frequency.ONCE, "15-12-2018", "2018");
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(expense.Interval), 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("01-01-2018", "31-01-2020")), 1);
            Utils.AssertNumericAlmostEqual(expense.CountRepetitions(new DateUtils.Interval("10-12-2018", "20-12-2018")), 6 / 17m);
        }

        [TestMethod]
        public void ThrowsWhenRequestingExpenseForInfiniteInterval()
        {
            var expense = new RepeatedExpense(100, Frequency.ONCE, "15-12-2018", "2018");
            Assert.ThrowsExactly<ArgumentException>(() => expense.CountRepetitions(new DateUtils.Interval("01-01-2018", "")));
        }
    }
}
