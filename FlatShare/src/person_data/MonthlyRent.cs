using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class MonthlyRent
    {
        [XmlIgnore]
        public DateUtils.Interval Interval { get; private set; } = new DateUtils.Interval();
        [XmlAttribute]
        public decimal Net { get; set; }
        [XmlAttribute]
        public decimal Ancillary { get; set; }
        [XmlAttribute]
        public int DueDay { get; set; }

        private DateTime _Announced;
        [XmlIgnore]
        public DateTime Announced { get { return _Announced.Date; } set { _Announced = value; } }
        [XmlAttribute("Announced")]
        public string AnnouncedAsString
        {
            get { return DateUtils.DateAsString(Announced); }
            set { Announced = DateUtils.DateFromString(value); }
        }

        [XmlAttribute("From")]
        public string FromAsString
        {
            get { return Interval.FromAsString; }
            set { Interval.FromAsString = value; }
        }
        [XmlAttribute("To")]
        [DefaultValueAttribute("")]
        public string ToAsString
        {
            get { return Interval.ToAsString; }
            set { Interval.ToAsString = value; }
        }

        public IEnumerable<DateTime> DueDates
        {
            get
            {
                var currentDueDate = Interval.From.AddDays(DueDay - Interval.From.Day);
                Debug.Assert(currentDueDate >= Interval.From);
                while (Interval.Contains(currentDueDate))
                {
                    yield return currentDueDate;
                    currentDueDate = currentDueDate.AddMonths(1);
                }
            }
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public MonthlyRent()
        {
        }

        public MonthlyRent(decimal net, decimal ancillary, int dueDay, string from, string to = "", string announced = "01-01-1900")
        {
            FromAsString = from;
            ToAsString = to;
            AnnouncedAsString = announced;
            Net = net;
            Ancillary = ancillary;
            DueDay = dueDay;
        }

        public void CheckIntegrity(string tenant = "")
        {
            if (Announced > Interval.From) throw new IntegrityException(String.Format("Tenant \"{0}\": Rent is announced after start date.", tenant));
            if (Interval.IsEmpty) throw new IntegrityException(String.Format("Tenant \"{0}\": Rent end date is located before start date.", tenant));
            var minDay = Interval.From.Day;
            var maxDay = 28;
            if (Interval.IsFinite)
            {
                var months = DateUtils.Range(Interval, d => d.AddMonths(1));
                maxDay = months.Select(d => DateUtils.DayCountForMonth(d)).Max();
                maxDay = Math.Min(maxDay, Interval.To.Day);
            }
            if (DueDay < minDay || DueDay > maxDay) throw new IntegrityException(String.Format("Tenant \"{0}\": Invalid rent due day ({1} not in interval [{2}, {3}].", tenant, DueDay, minDay, maxDay));
            if (Net < 0M) throw new IntegrityException(String.Format("Tenant \"{0}\": Net Rent has to be non-negative.", tenant));
            if (Ancillary < 0M) throw new IntegrityException(String.Format("Tenant \"{0}\": Ancillary Rent has to be non-negative.", tenant));
        }

        public override string ToString()
        {
            return String.Format("monthly rent of {0} + {1} in {2}.", Net, Ancillary, Interval);
        }
    }
}
