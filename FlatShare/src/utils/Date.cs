using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public static class DateUtils
    {
        private static readonly string[] DATE_FORMATS = { "yyyy", "MM-yyyy", "dd-MM-yyyy" };

        public enum DateFillMode { None, First, Last };

        public static string DateAsString(DateTime date, DateFillMode fillMode = DateFillMode.None)
        {
            if (fillMode == DateFillMode.None) return date.ToString(DATE_FORMATS.Last());
            foreach (string format in DATE_FORMATS)
            {
                var s = date.ToString(format);
                var parsedDate = DateFromString(s, fillMode);
                if ((date - parsedDate).Days == 0)
                {
                    return s;
                }
            }
            throw new FormatException("Not able to cast date to a parsable string.");
        }

        public static DateTime DateFromString(string s, DateFillMode fillMode = DateFillMode.None)
        {
            DateTime date;
            if (fillMode == DateFillMode.None)
            {
                if (DateTime.TryParseExact(s, DATE_FORMATS.Last(), null,
                        System.Globalization.DateTimeStyles.AssumeUniversal |
                        System.Globalization.DateTimeStyles.NoCurrentDateDefault,
                    out date))
                {
                    return date.Date;
                }
            }
            else
            {
                for (int i = 0; i < DATE_FORMATS.Length; i++)
                {
                    if (DateTime.TryParseExact(s, DATE_FORMATS[i], null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.NoCurrentDateDefault,
                        out date))
                    {
                        if (fillMode == DateFillMode.Last)
                        {
                            switch (i)
                            {
                                case 0:
                                    date = date.AddYears(1);
                                    break;
                                case 1:
                                    date = date.AddMonths(1);
                                    break;
                                case 2:
                                    date = date.AddDays(1);
                                    break;
                            }
                            date = date.AddDays(-1);
                        }
                        return date.Date;
                    }
                }
            }
            throw new FormatException("Could not interprete string as date: " + s);
        }

        public static DateTime Max(params DateTime[] dates)
        {
            return dates.Max();
        }

        public static DateTime Min(params DateTime[] dates)
        {
            return dates.Min();
        }

        public static IEnumerable<DateTime> Range(Interval interval, Func<DateTime, DateTime> stepFunction)
        {
            DateTime current = interval.From;
            while (!interval.IsFinite || current <= interval.To)
            {
                yield return current;
                current = stepFunction(current);
            }
        }

        public static int DayCountForMonth(DateTime date)
        {
            date = date.AddDays(1 - date.Day);
            return (date.AddMonths(1) - date).Days;
        }

        public static int DayCountForYear(DateTime date)
        {
            date = date.AddDays(1 - date.Day);
            date = date.AddMonths(1 - date.Month);
            return (date.AddYears(1) - date).Days;
        }

        public interface IDateProvider
        {
            DateTime Now { get; }
        }

        public class SystemDateProvider : IDateProvider
        {
            public DateTime Now { get { return DateTime.Now.Date; } }
        }

        public class Interval : IComparable<Interval>, IComparable<DateTime>
        {
            private DateTime _From;
            private DateTime? _To;
            public DateTime From { get { return _From.Date; } set { _From = value; } }
            public DateTime To
            {
                get
                {
                    Debug.Assert(_To.HasValue);
                    return _To.Value.Date;
                }
                set { _To = value; }
            }

            public bool IsFinite { get { return _To.HasValue; } }

            public IntegerWithInfinity Days
            {
                get
                {
                    return IsFinite ? (To - From).Days + 1 : IntegerWithInfinity.INFINITY;
                }
            }

            public bool IsEmpty { get { return Days <= 0; } }

            public string FromAsString
            {
                get { return DateUtils.DateAsString(From, DateFillMode.First); }
                set { this.From = DateUtils.DateFromString(value, DateFillMode.First); }
            }
            public string ToAsString
            {
                get { return IsFinite ? DateUtils.DateAsString(To, DateFillMode.Last) : ""; }
                set
                {
                    if (String.IsNullOrEmpty(value))
                    {
                        this._To = null;
                    }
                    else
                    {
                        this.To = DateUtils.DateFromString(value, DateFillMode.Last);
                    }
                }
            }

            public Interval()
            {
                From = DateTime.MinValue;
                _To = null;
            }

            public Interval(Interval other)
            {
                _From = other._From;
                _To = other._To;
            }

            public Interval(DateTime from, DateTime? to = null)
            {
                From = from;
                _To = to;
            }

            public Interval(string from, string to = "")
            {
                FromAsString = from;
                ToAsString = to;
            }

            public Interval Intersect(Interval interval)
            {
                DateTime? to;
                if (!IsFinite) to = interval._To;
                else if (!interval.IsFinite) to = _To;
                else to = DateUtils.Min(To, interval.To);
                return new Interval(DateUtils.Max(From, interval.From), to);
            }

            private IEnumerable<Interval> _Except(Interval interval)
            {
                var startsBefore = interval.CompareTo(From) > 0;
                var endsAfter = IsFinite ? interval.CompareTo(To) < 0 : interval.IsFinite;
                if (startsBefore)
                {
                    var end = interval.From.AddDays(-1);
                    if (IsFinite) end = Min(end, To);
                    yield return new Interval(From, end);
                }
                if (endsAfter)
                {
                    yield return new Interval(Max(From, interval.To.AddDays(1)), _To);
                }
            }

            public IEnumerable<Interval> Except(IEnumerable<Interval> intervals)
            {
                var remainingIntervals = this.ToEnumerable();
                foreach (var interval in intervals)
                {
                    remainingIntervals = remainingIntervals.SelectMany(i => i._Except(interval));
                }
                return remainingIntervals;
            }

            public IEnumerable<Interval> Except(params DateUtils.Interval[] intervals)
            {
                return Except(intervals.AsEnumerable());
            }

            public IEnumerable<Interval> SplitByYear()
            {
                Interval current = new Interval(this);
                while (!IsFinite || current.From.Year < To.Year)
                {
                    current.To = new DateTime(current.From.Year, 12, 31);
                    yield return new Interval(current);
                    current.From = new DateTime(current.From.Year + 1, 1, 1);
                }
                current.To = To;
                yield return current;
            }

            public override bool Equals(object obj)
            {
                var castObj = obj as Interval;
                if (castObj == null)
                    return false;
                return castObj.From == From && castObj._To?.Date == _To?.Date;
            }

            public override int GetHashCode()
            {
                return From.GetHashCode() ^ To.GetHashCode();
            }

            public int CompareTo(Interval other)
            {
                if (IsEmpty || other.IsEmpty) throw new InvalidOperationException("Cannot compare empty intervals.");
                if (IsFinite && To < other.From) return -1;
                if (other.IsFinite && From > other.To) return 1;
                if (From == other.From && _To?.Date == other._To?.Date) return 0;
                throw new InvalidOperationException("Intervals are not comparable.");
            }

            public bool Contains(Interval other)
            {
                if (From > other.From) return false;
                if (!IsFinite) return true;
                if (!other.IsFinite) return false;
                return To >= other.To;
            }

            public bool Contains(DateTime date)
            {
                return CompareTo(date) == 0;
            }

            public int CompareTo(DateTime date)
            {
                if (Days <= 0) throw new InvalidOperationException("Cannot compare for intervals with nonpositive day count.");
                if (date < From) return 1;
                else if (IsFinite && date > To) return -1;
                else return 0;
            }

            public override string ToString()
            {
                if (IsFinite) return String.Format("[{0} -- {1}]", FromAsString, ToAsString);
                else return String.Format("[{0} -- inf]", FromAsString);
            }
        }

        public static DateTime ProjectOnto(this DateTime date, DateUtils.Interval interval)
        {
            if (interval.IsEmpty) throw new InvalidOperationException("Cannot project onto empty intervals.");
            var compareResult = interval.CompareTo(date);
            if (compareResult == 0)
                return date;
            else if (compareResult < 0)
                return interval.To;
            else
                return interval.From;
        }

        // Returns the smallest intervals that contains all intervals of the enumerable.
        public static Interval Span(this IEnumerable<Interval> intervals)
        {
            DateTime from = DateTime.MaxValue;
            DateTime? to = DateTime.MinValue;
            foreach (var interval in intervals)
            {
                from = Min(from, interval.From);
                if (!interval.IsFinite) to = null;
                if (to.HasValue) to = Max(to.Value, interval.To);
            }
            return new Interval(from, to);
        }

        public static int binarySearchInInterval(List<Interval> intervals, DateTime date)
        {
            int l = 0;
            int r = intervals.Count - 1;
            if (intervals[l].From > date || date > intervals[r].To) { return -1; }
            if (date <= intervals[l].To) { return l; }
            if (date >= intervals[r].From) { return r; }
            while (r - l > 1)
            {
                int mid = (r + l) / 2;
                if (date > intervals[mid].To) { l = mid; }
                else if (date < intervals[mid].From) { r = mid; }
                else { return mid; }
            }
            return -1;
        }

        public static int FirstNonConsecutiveIntervalIndex(IEnumerable<Interval> intervals)
        {
            return _FirstViolatedConditionIndex(intervals, _AreConsecutive);
        }

        public static int FirstUnorderedIntervalIndex(IEnumerable<Interval> intervals)
        {
            return _FirstViolatedConditionIndex(intervals, (l, r) => { try { return l.CompareTo(r) < 0; } catch { return false; } });
        }

        public static int FirstNonOrderedDayIndex(IEnumerable<DateTime> dates, bool strongOrder = false)
        {
            var offset = strongOrder ? 1 : 0;
            return _FirstViolatedConditionIndex(dates, (l, r) => (r - l).Days >= offset);
        }

        private static bool _AreConsecutive(Interval l, Interval r)
        {
            if (!l.IsFinite) return false;
            var span = r.From - l.To;
            return span.Days == 1;
        }

        private static int _FirstViolatedConditionIndex<T>(IEnumerable<T> intervals, Func<T, T, bool> cond)
        {
            if (intervals.IsEmpty()) return -1;
            var lastInterval = intervals.First();
            int idx = 1;
            foreach (var interval in intervals.Skip(1))
            {
                if (!cond(lastInterval, interval)) return idx;
                lastInterval = interval;
                idx++;
            }
            return -1;
        }
    }
}
