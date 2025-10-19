using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class MetereTest
    {
        [TestMethod]
        public void DoesNotThrowForValidMeter()
        {
            var meter = new Meter("Gas", "1234", "$m^3$");
            meter.Values.Add(new MeterValue("01-01-2018", 100));
            meter.Values.Add(new MeterValue("05-01-2018", 130));
            meter.Values.Add(new MeterValue("07-01-2019", 180));
            meter.CheckIntegrity();
        }

        [TestMethod]
        public void ComputesCorrectDifferencesForMeter()
        {
            var meter = new Meter("Gas", "1234", "m^3");
            meter.Values.Add(new MeterValue("01-01-2018", 0));
            meter.Values.Add(new MeterValue("08-01-2018", 32, true));
            meter.Values.Add(new MeterValue("01-02-2018", 100));

            Assert.AreEqual(4M, meter.GetDifference(new DateUtils.Interval("01-01-2018", "01-01-2018")));
            Assert.AreEqual(20M, meter.GetDifference(new DateUtils.Interval("03-01-2018", "07-01-2018")));
            Assert.AreEqual(12M, meter.GetDifference(new DateUtils.Interval("06-01-2018", "08-01-2018")));
            Assert.AreEqual(80M, meter.GetDifference(new DateUtils.Interval("06-01-2018", "31-01-2018")));
            Assert.AreEqual(100M, meter.GetDifference(new DateUtils.Interval("01-01-2018", "31-01-2018")));
        }

        [TestMethod]
        public void ComputesCorrectInterpolatedValuesForMeter()
        {
            var meter = new Meter("Gas", "1234", "m^3");
            meter.Values.Add(new MeterValue("01-01-2018", 0));
            meter.Values.Add(new MeterValue("08-01-2018", 32, true));
            meter.Values.Add(new MeterValue("01-02-2018", 100));

            Assert.AreEqual(0M, meter.GetInterpolatedValue(DateUtils.DateFromString("01-01-2018")));
            Assert.AreEqual(32M, meter.GetInterpolatedValue(DateUtils.DateFromString("09-01-2018")));
            Assert.AreEqual(100M, meter.GetInterpolatedValue(DateUtils.DateFromString("01-02-2018")));
            Assert.AreEqual(8M, meter.GetInterpolatedValue(DateUtils.DateFromString("03-01-2018")));
            Utils.AssertNumericAlmostEqual(16M, meter.GetInterpolatedValue(DateUtils.DateFromString("05-01-2018")));
            Utils.AssertNumericAlmostEqual(32M + 6M * 68M / 23M, meter.GetInterpolatedValue(DateUtils.DateFromString("15-01-2018")));
        }

        [TestMethod]
        public void ThrowsIntegrityExceptionWhenMeterValuesAreMissing()
        {
            var meter = new Meter("", "", "");
            meter.Values.Add(new MeterValue("01-10-2018", 100));
            Assert.ThrowsExactly<IntegrityException>(() => meter.GetDifference(new DateUtils.Interval("05-01-2018", "10-01-2018")));

        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenMeterValuesDecrease()
        {
            var meter = new Meter("", "", "");
            meter.Values.Add(new MeterValue("01-01-2018", 100));
            meter.Values.Add(new MeterValue("05-01-2018", 50));
            Assert.ThrowsExactly<IntegrityException>(() => meter.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenMeterDatesDecrease()
        {
            var meter = new Meter("", "", "");
            meter.Values.Add(new MeterValue("05-01-2018", 100));
            meter.Values.Add(new MeterValue("01-01-2018", 200));
            Assert.ThrowsExactly<IntegrityException>(() => meter.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenMeterDatesIdentical()
        {
            var meter = new Meter("", "", "");
            meter.Values.Add(new MeterValue("05-01-2018", 100));
            meter.Values.Add(new MeterValue("05-01-2018", 200));
            Assert.ThrowsExactly<IntegrityException>(() => meter.CheckIntegrity());
        }

        [TestMethod]
        public void IdenticalMeterDatesWithOneEndOfDayValid()
        {
            var meter = new Meter("", "", "");
            meter.Values.Add(new MeterValue("05-01-2018", 100));
            meter.Values.Add(new MeterValue("05-01-2018", 200, true));
            meter.CheckIntegrity();
        }
    }
}
