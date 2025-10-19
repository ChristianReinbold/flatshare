using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class MonthlyRentTest
    {
        private static MonthlyRent GetValidRent()
        {
            return new MonthlyRent(150M, 120.02M, 10, "05-01-2018", "22-05-2018", "01-01-2018");
        }

        [TestMethod]
        public void DoesNotThrowForValidRent()
        {
            var rent = GetValidRent();
            rent.CheckIntegrity();
        }

        [TestMethod]
        public void ValidDueDatesForRent()
        {
            var rent = GetValidRent();
            var dueDates = rent.DueDates.ToList();
            Assert.AreEqual(dueDates.Count, 5);
            Utils.AssertDate("10-01-2018", dueDates[0]);
            Utils.AssertDate("10-02-2018", dueDates[1]);
            Utils.AssertDate("10-03-2018", dueDates[2]);
            Utils.AssertDate("10-04-2018", dueDates[3]);
            Utils.AssertDate("10-05-2018", dueDates[4]);
        }

        [TestMethod]
        public void ThrowsOnEarlyDueDayForRent()
        {
            var rent = GetValidRent();
            rent.DueDay = 3;
            Assert.ThrowsExactly<IntegrityException>(() => rent.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnLateDueDayForRent()
        {
            var rent = GetValidRent();
            rent.DueDay = 25;
            Assert.ThrowsExactly<IntegrityException>(() => rent.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnStartAfterEndForRent()
        {
            var rent = GetValidRent();
            rent.FromAsString = "05-01-2019";
            Assert.ThrowsExactly<IntegrityException>(() => rent.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnNegativeNetValueForRent()
        {
            var rent = GetValidRent();
            rent.Net = -150M;
            Assert.ThrowsExactly<IntegrityException>(() => rent.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnNegativeAncillaryValueForRent()
        {
            var rent = GetValidRent();
            rent.Ancillary = -120.02M;
            Assert.ThrowsExactly<IntegrityException>(() => rent.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnLateAnnouncementForRent()
        {
            var rent = GetValidRent();
            rent.AnnouncedAsString = "07-01-2018";
            Assert.ThrowsExactly<IntegrityException>(() => rent.CheckIntegrity());
        }
    }
}
