using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class PositionTest
    {

        private Position GetInstance(bool infinite)
        {
            var pos = new Position("", AllocationKey.PERS);
            pos.Expenses.Clear();
            pos.Expenses.Add(new RepeatedExpense(10, Frequency.ONCE, "05-01-2018", "21-01-2018"));
            pos.Expenses.Add(new RepeatedExpense(100, Frequency.ONCE, "22-01-2018", "10-02-2018"));
            if (infinite)
            {
                pos.Expenses.Add(new RepeatedExpense(1, Frequency.DAILY, "11-02-2018"));
            }
            return pos;
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenExpensesAreNonContinous()
        {
            var pos = GetInstance(false);
            pos.Expenses.Add(new RepeatedExpense(10, Frequency.ONCE, "13-02-2018", "2018"));
            Assert.ThrowsExactly<IntegrityException>(() => pos.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenExpensesAreNonContinousDueToInfinity()
        {
            var pos = GetInstance(true);
            pos.Expenses.Add(new RepeatedExpense(10, Frequency.ONCE, "11-02-2018", "2018"));
            Assert.ThrowsExactly<IntegrityException>(() => pos.CheckIntegrity());
        }

        [TestMethod]
        public void IntervalMatchesAggregatedExpenses()
        {
            var pos = GetInstance(false);
            var expected = new DateUtils.Interval("05-01-2018", "10-02-2018");
            Assert.AreEqual(pos.Interval, expected);
        }

        [TestMethod]
        public void IntervalMatchesInfiniteAggregatedExpenses()
        {
            var pos = GetInstance(true);
            var expected = new DateUtils.Interval("05-01-2018", "");
            Assert.AreEqual(pos.Interval, expected);
        }

        [TestMethod]
        public void SumsUpCostsForPartialInterval()
        {
            var pos = GetInstance(false);
            var actual = pos.GetCosts(new DateUtils.Interval("2018", "31-01-2018"));
            Utils.AssertNumericAlmostEqual(actual, 60);

            pos = GetInstance(true);
            actual = pos.GetCosts(new DateUtils.Interval("2018", "31-01-2018"));
            Utils.AssertNumericAlmostEqual(actual, 60);
            actual = pos.GetCosts(new DateUtils.Interval("2018", "15-02-2018"));
            Utils.AssertNumericAlmostEqual(actual, 115);
        }
    }
}
