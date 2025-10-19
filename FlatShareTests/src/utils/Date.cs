using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class Date
    {
        [TestMethod]
        public void CorrectlyRemovesInfiniteIntervalsFromFiniteInterval()
        {
            var interval = new DateUtils.Interval("2018", "2018");
            var extracted = interval.Except(new DateUtils.Interval("11-2018")
                                           , new DateUtils.Interval("04-05-2018", "07-05-2018")
                                           , new DateUtils.Interval("05-2017", "03-02-2018")
                                           , new DateUtils.Interval("06-05-2018", "09-2018")
                                           );
            var intervals = extracted.ToList();
            Assert.AreEqual(2, intervals.Count);
            Utils.AssertDate("04-02-2018", intervals[0].From);
            Utils.AssertDate("03-05-2018", intervals[0].To);
            Utils.AssertDate("01-10-2018", intervals[1].From);
            Utils.AssertDate("31-10-2018", intervals[1].To);
        }

        [TestMethod]
        public void CorrectlyRemovesFiniteIntervalsFromInfiniteInterval()
        {
            var interval = new DateUtils.Interval("2018");
            var extracted = interval.Except(new DateUtils.Interval("11-2018", "2019")
                                           , new DateUtils.Interval("04-05-2018", "07-05-2018")
                                           , new DateUtils.Interval("05-2017", "03-02-2018")
                                           , new DateUtils.Interval("06-05-2018", "09-2018")
                                           );
            var intervals = extracted.ToList();
            Assert.AreEqual(3, intervals.Count);
            Utils.AssertDate("04-02-2018", intervals[0].From);
            Utils.AssertDate("03-05-2018", intervals[0].To);
            Utils.AssertDate("01-10-2018", intervals[1].From);
            Utils.AssertDate("31-10-2018", intervals[1].To);
            Utils.AssertDate("01-01-2020", intervals[2].From);
            Assert.IsFalse(intervals[2].IsFinite);
        }

        [TestMethod]
        public void CorrectlyRemovesInfiniteIntervalsFromInfiniteInterval()
        {
            var interval = new DateUtils.Interval("2018");
            var extracted = interval.Except(new DateUtils.Interval("11-2018")
                                           , new DateUtils.Interval("04-05-2018", "07-05-2018")
                                           , new DateUtils.Interval("05-2017", "03-02-2018")
                                           , new DateUtils.Interval("06-05-2018", "09-2018")
                                           );
            var intervals = extracted.ToList();
            Assert.AreEqual(2, intervals.Count);
            Utils.AssertDate("04-02-2018", intervals[0].From);
            Utils.AssertDate("03-05-2018", intervals[0].To);
            Utils.AssertDate("01-10-2018", intervals[1].From);
            Utils.AssertDate("31-10-2018", intervals[1].To);
        }

        [TestMethod]
        public void CorrectlySplitsFiniteIntervalsByYear()
        {
            var interval = new DateUtils.Interval("05-06-2018", "08-09-2018");
            var split = interval.SplitByYear().ToList();
            Assert.AreEqual(1, split.Count);
            Utils.AssertDate("05-06-2018", split[0].From);
            Utils.AssertDate("08-09-2018", split[0].To);

            interval = new DateUtils.Interval("05-06-2018", "08-02-2019");
            split = interval.SplitByYear().ToList();
            Assert.AreEqual(2, split.Count);
            Utils.AssertDate("05-06-2018", split[0].From);
            Utils.AssertDate("31-12-2018", split[0].To);
            Utils.AssertDate("01-01-2019", split[1].From);
            Utils.AssertDate("08-02-2019", split[1].To);

            interval = new DateUtils.Interval("01-01-2018", "31-12-2018");
            split = interval.SplitByYear().ToList();
            Assert.AreEqual(1, split.Count);
            Utils.AssertDate("01-01-2018", split[0].From);
            Utils.AssertDate("31-12-2018", split[0].To);

            interval = new DateUtils.Interval("31-12-2017", "01-01-2019");
            split = interval.SplitByYear().ToList();
            Assert.AreEqual(3, split.Count);
            Utils.AssertDate("31-12-2017", split[0].From);
            Utils.AssertDate("31-12-2017", split[0].To);
            Utils.AssertDate("01-01-2018", split[1].From);
            Utils.AssertDate("31-12-2018", split[1].To);
            Utils.AssertDate("01-01-2019", split[2].From);
            Utils.AssertDate("01-01-2019", split[2].To);
        }

        [TestMethod]
        public void CorrectlySplitsInfiniteFiniteIntervalsByYear()
        {
            // We cannot check the infinite stream, so we only check the first five elements
            var interval = new DateUtils.Interval("05-06-2018");
            var split = interval.SplitByYear().Take(5).ToList();
            Assert.AreEqual(5, split.Count);
            Utils.AssertDate("05-06-2018", split[0].From);
            Utils.AssertDate("31-12-2018", split[0].To);
            Utils.AssertDate("01-01-2019", split[1].From);
            Utils.AssertDate("31-12-2019", split[1].To);
            Utils.AssertDate("01-01-2020", split[2].From);
            Utils.AssertDate("31-12-2020", split[2].To);
            Utils.AssertDate("01-01-2021", split[3].From);
            Utils.AssertDate("31-12-2021", split[3].To);
            Utils.AssertDate("01-01-2022", split[4].From);
            Utils.AssertDate("31-12-2022", split[4].To);

            interval = new DateUtils.Interval("31-12-2017");
            split = interval.SplitByYear().Take(5).ToList();
            Assert.AreEqual(5, split.Count);
            Utils.AssertDate("31-12-2017", split[0].From);
            Utils.AssertDate("31-12-2017", split[0].To);
            Utils.AssertDate("01-01-2018", split[1].From);
            Utils.AssertDate("31-12-2018", split[1].To);
            Utils.AssertDate("01-01-2019", split[2].From);
            Utils.AssertDate("31-12-2019", split[2].To);
            Utils.AssertDate("01-01-2020", split[3].From);
            Utils.AssertDate("31-12-2020", split[3].To);
            Utils.AssertDate("01-01-2021", split[4].From);
            Utils.AssertDate("31-12-2021", split[4].To);

            interval = new DateUtils.Interval("01-01-2018");
            split = interval.SplitByYear().Take(5).ToList();
            Assert.AreEqual(5, split.Count);
            Utils.AssertDate("01-01-2018", split[0].From);
            Utils.AssertDate("31-12-2018", split[0].To);
            Utils.AssertDate("01-01-2019", split[1].From);
            Utils.AssertDate("31-12-2019", split[1].To);
            Utils.AssertDate("01-01-2020", split[2].From);
            Utils.AssertDate("31-12-2020", split[2].To);
            Utils.AssertDate("01-01-2021", split[3].From);
            Utils.AssertDate("31-12-2021", split[3].To);
            Utils.AssertDate("01-01-2022", split[4].From);
            Utils.AssertDate("31-12-2022", split[4].To);
        }
    }
}
