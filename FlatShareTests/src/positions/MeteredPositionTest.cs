using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class MeteredPositionTest
    {
        private static List<decimal> GetListOfDailyCosts()
        {
            // Total meter diff: 178 - 104 = 74
            var list = new List<decimal>();
            list.Add(100 * 1 / 74m);          // 5.1. Meter diff: 105 - 104 = 1
            list.Add(100 * 1 / 74m);          // 6.1. Meter diff: 106 - 105 = 1
            list.Add(100 * 1 / 74m);          // 7.1. Meter diff: 107 - 106 = 1
            list.Add(100 * 1 / 74m);          // 8.1. Meter diff: 108 - 107 = 1
            list.Add(100 * 1 / 74m);          // 9.1. Meter diff: 109 - 108 = 1
            list.Add(100 * 1 / 74m);          // 10.1. Meter diff: 110 - 109 = 1
            list.Add(100 * 4 / 74m);          // 11.1. Meter diff: 114 - 110 = 4
            list.Add(100 * 4 / 74m);          // 12.1. Meter diff: 118 - 114 = 4
            list.Add(100 * 4 / 74m);          // 13.1. Meter diff: 122 - 118 = 4
            list.Add(100 * 4 / 74m);          // 14.1. Meter diff: 126 - 122 = 4
            list.Add(100 * 4 / 74m);          // 15.1. Meter diff: 130 - 126 = 4
            list.Add(100 * 12 / 74m);          // 16.1. Meter diff: 142 - 130 = 12
            list.Add(100 * 12 / 74m);          // 17.1. Meter diff: 154 - 142 = 12
            list.Add(100 * 12 / 74m);         // 18.1. Meter diff: 166 - 154 = 12
            list.Add(100 * 12 / 74m);         // 19.1. Meter diff: 178 - 166 = 12
            return list;
        }

        private static Positions GetInstance(bool multipleMeters = false)
        {
            var storageMock = new FileStorage(new MemoryFileSystem());
            var meters = new Meters(storageMock);
            var roomData = new RoomData(storageMock);
            var pos = new MeteredPosition("", AllocationKey.PERS);
            pos.Expenses.Add(new Expense(100, "05-01-2018", "19-01-2018"));
            var meterValue1 = new MeterValue("01-01-2018", 100);
            var meterValue2 = new MeterValue("11-01-2018", 110);
            var meterValue3 = new MeterValue("16-01-2018", 130);
            var meterValue4 = new MeterValue("21-01-2018", 190);

            if (multipleMeters)
            {
                var meter1 = new Meter("1", "Id1", "kWh");
                meter1.Values.Add(meterValue1);
                meter1.Values.Add(meterValue2);
                meters["1"] = meter1;
                pos.MeterFiles.Add("1");

                var startMeterValue = new MeterValue(meterValue2.Date, 0);
                meterValue3.Value -= meterValue2.Value;
                meterValue4.Value -= meterValue2.Value;
                var meter2 = new Meter("2", "Id1", "kWh");
                meter2.Values.Add(startMeterValue);
                meter2.Values.Add(meterValue3);
                meter2.Values.Add(meterValue4);
                meters["2"] = meter2;
                pos.MeterFiles.Add("2");
            }
            else
            {
                var meter = new Meter("1", "Id1", "kWh");
                meter.Values.Add(meterValue1);
                meter.Values.Add(meterValue2);
                meter.Values.Add(meterValue3);
                meter.Values.Add(meterValue4);
                meters["1"] = meter;
                pos.MeterFiles.Add("1");
            }

            Positions positions = new Positions(storageMock, meters, roomData);
            positions.Add("pos.xml", pos);
            positions.Update();
            return positions;
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ComputesCostsWithRespectToMeterValueSplit(bool multipleMeters)
        {
            var positions = GetInstance(multipleMeters);
            var pos = positions["pos.xml"];
            var expectedList = GetListOfDailyCosts();
            for (int startDayIdx = -1; startDayIdx < expectedList.Count; startDayIdx++)
            {
                var startDate = new DateTime(2018, 01, 05 + startDayIdx);
                for (int endDayIdx = Math.Max(0, startDayIdx); endDayIdx < expectedList.Count + 1; endDayIdx++)
                {
                    var endDate = new DateTime(2018, 01, 05 + endDayIdx);
                    var listStartIdx = Math.Max(0, startDayIdx);
                    var listEndIdx = Math.Min(endDayIdx, expectedList.Count - 1);
                    var expected = expectedList.GetRange(listStartIdx, listEndIdx - listStartIdx + 1).Sum();
                    var actual = pos.GetCosts(new DateUtils.Interval(startDate, endDate));
                    Utils.AssertNumericAlmostEqual(actual, expected);
                }
            }
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenInfiniteRangesExist()
        {
            var positions = GetInstance();
            var pos = positions["pos.xml"] as MeteredPosition;
            pos.Expenses.Add(new Expense(100, "20-01-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => pos.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnDifferentUnitsInMeters()
        {
            var positions = GetInstance(true);
            positions.Meters["2"].Unit = "$m^3$";
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ThrowsOnGapsBetweenMeterIntervals(bool modifyEndOfDay)
        {
            var positions = GetInstance(true);
            // hole of one day between meter 1 and 2.
            if (modifyEndOfDay)
            {
                positions.Meters["2"].Values[0].DateAsString = "12-01-2018";
            }
            else
            {
                positions.Meters["2"].Values[0].EndOfDay = true;
            }
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void ThrowsOnOverlapBetweenMeterIntervals()
        {
            var positions = GetInstance(true);
            positions.Meters["2"].Values = positions.Meters["1"].Values;
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void ThrowsOnMetersNotOrdered()
        {
            var positions = GetInstance(true);
            var pos = positions["pos.xml"] as MeteredPosition;
            pos.MeterFiles.Reverse();
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }
    }
}
