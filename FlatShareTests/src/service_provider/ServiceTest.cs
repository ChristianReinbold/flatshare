using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class ServiceTest
    {
        private static Service GetInstance(bool finite)
        {
            var position = new Position("Heating", AllocationKey.PERS);
            position.Expenses.Add(new RepeatedExpense(27.45M, Frequency.MONTHLY, "2018", "2019"));

            var service = new Service("heating.xml", "03-2018", finite ? "09-2018" : "");
            service.AssociatedPosition = position;
            return service;
        }

        [TestMethod]
        public void ComputesCorrectCostsForServiceWithFiniteDuration()
        {
            var service = GetInstance(true);
            var actual = service.GetCosts(new DateUtils.Interval("2017", "2020"));
            Assert.AreEqual(27.45M * 7, actual);
        }

        [TestMethod]
        public void ComputesCorrectCostsForServiceWithInfiniteDuration()
        {
            var service = GetInstance(false);
            var actual = service.GetCosts(new DateUtils.Interval("2017", "2020"));
            Assert.AreEqual(27.45M * 22, actual);
        }
    }
}
