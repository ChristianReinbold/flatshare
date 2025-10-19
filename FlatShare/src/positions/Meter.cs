using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class MeterValue
    {
        private DateTime _Date;
        [XmlIgnore]
        public DateTime MeasureDate { get { return _Date.Date; } set { _Date = value; } }
        [XmlIgnore]
        public DateTime Date { get { return EndOfDay ? _Date.Date.AddDays(1) : _Date.Date; } }
        [XmlAttribute("Date")]
        public string DateAsString
        {
            get { return DateUtils.DateAsString(MeasureDate); }
            set { MeasureDate = DateUtils.DateFromString(value); }
        }

        [XmlAttribute]
        [DefaultValueAttribute(false)]
        public bool EndOfDay { get; set; }

        [XmlText]
        public long Value { get; set; }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public MeterValue()
        {
        }

        public MeterValue(DateTime date, long value, bool endOfDay = false)
        {
            MeasureDate = date;
            Value = value;
            EndOfDay = endOfDay;
        }

        public MeterValue(string date, long value, bool endOfDay = false)
        {
            DateAsString = date;
            Value = value;
            EndOfDay = endOfDay;
        }

        public override string ToString()
        {
            return String.Format("Value {0} on {1}", Value, Date.ToString("dd-MM-yyyy"));
        }
    }

    public class Meter
    {
        [XmlElement("Value")]
        public List<MeterValue> Values { get; set; } = new List<MeterValue>();

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Id { get; set; }

        [XmlAttribute]
        public string Unit { get; set; }

        //[XmlIgnore]
        public DateUtils.Interval Interval
        {
            get
            {
                var from = Values.First().Date;
                // Subtract one day since a value resembles a measurement at the start of "Date",
                // and we can only track meter value differences up the day before that.
                var to = Values.Last().Date.AddDays(-1);
                return new DateUtils.Interval(from, to);
            }
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public Meter()
        {
        }

        public Meter(string name, string id, string unit)
        {
            Name = name;
            Id = id;
            Unit = unit;
        }

        public override string ToString()
        {
            return Name;
        }

        public decimal GetDifference(DateUtils.Interval interval)
        {
            if (!interval.IsFinite) throw new ArgumentException("Only finite intervals allowed.");
            if (interval.IsEmpty) return 0M;
            if (!Interval.Contains(interval))
            {
                string template = "Insufficient values of meter \"{0}\" for calculating the consumption from {0} to {1}.";
                throw new IntegrityException(String.Format(template, Name, interval.FromAsString, interval.ToAsString));
            }

            decimal endValue = GetInterpolatedValue(interval.To.AddDays(1));
            decimal startValue = GetInterpolatedValue(interval.From);
            return endValue - startValue;
        }

        public decimal GetInterpolatedValue(DateTime date)
        {
            int l = 0;
            int r = Values.Count - 1;
            if (Values[l].Date > date || Values[r].Date < date)
            {
                string format = "dd-MM-yyyy";
                string template = "Insufficient values of meter \"{0}\" for getting value at {1}.";
                throw new IntegrityException(String.Format(template, Name, date.ToString(format)));
            }

            while (r - l > 1)
            {
                int mid = (r + l) / 2;
                if (Values[mid].Date > date) { r = mid; }
                else { l = mid; }
            }

            if (r == l) return Values[l].Value;

            var mLow = Values[l];
            var mHigh = Values[r];
            var lambda = (date - mLow.Date).Days / (decimal)(mHigh.Date - mLow.Date).Days;
            return lambda * mHigh.Value + (1 - lambda) * mLow.Value;
        }

        public void CheckIntegrity()
        {
            if (Values.IsEmpty())
            {
                var template = "In meter \"{0}\": No values supplied.";
                throw new IntegrityException(String.Format(template, Name));
            }
            var violatedIndex = DateUtils.FirstNonOrderedDayIndex(Values.Select(mv => mv.Date), true);
            if (violatedIndex >= 0)
            {
                var template = "In meter \"{0}\": The {1}. value is not dated after the previous one.";
                throw new IntegrityException(String.Format(template, Name, violatedIndex + 1));
            }
            for (int idx = 0; idx < Values.Count - 1; idx++)
            {
                var delta = Values[idx + 1].Value - Values[idx].Value;
                if (delta < 0)
                {
                    var template = "In meter \"{0}\": The {1}. meter value is smaller than the previous one.";
                    throw new IntegrityException(String.Format(template, Name, idx + 1));
                }
            }
        }
    }
}
