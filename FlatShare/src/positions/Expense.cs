using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Expense
    {
        [XmlIgnore]
        public DateUtils.Interval Interval { get; private set; } = new DateUtils.Interval();
        [XmlAttribute]
        public decimal Magnitude { get; set; }

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

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public Expense()
        {
        }

        public Expense(decimal magnitude, string from, string to = "")
        {
            Magnitude = magnitude;
            Interval = new DateUtils.Interval(from, to);
        }

        public virtual void CheckIntegrity(string position = "")
        {
            if (Interval.IsEmpty)
            {
                string template = "Position \"{0}\": \"from\" date of expense located after \"to\" date.";
                throw new IntegrityException(String.Format(template, position));
            }
        }
    }

    public enum Frequency { ONCE, ANNUAL, MONTHLY, DAILY }

    public class RepeatedExpense : Expense
    {

        [XmlAttribute]
        public Frequency Frequency { get; set; }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public RepeatedExpense()
        {
        }

        public RepeatedExpense(
            decimal magnitude, Frequency frequency, string from, string to = "")
            : base(magnitude, from, to)
        {
            Frequency = frequency;
        }

        public decimal CountRepetitions(DateUtils.Interval interval)
        {
            if (!interval.IsFinite) throw new ArgumentException("Only finite intervals allowed.");
            interval = interval.Intersect(Interval);
            if (interval.IsEmpty)
            {
                return 0M;
            }
            decimal skippedFractions = 0M;
            switch (Frequency)
            {
                case Frequency.ONCE:
                    return interval.Days.Value / (decimal)Interval.Days.Value;
                case Frequency.DAILY:
                    return interval.Days.Value;
                case Frequency.MONTHLY:
                    var firstMonth = interval.From.AddDays(1 - interval.From.Day);
                    var trailingMonth = interval.To.AddMonths(1);
                    trailingMonth = trailingMonth.AddDays(1 - trailingMonth.Day);
                    int monthCount = (trailingMonth.Month - firstMonth.Month) + 12 * (trailingMonth.Year - firstMonth.Year);
                    Debug.Assert(monthCount > 0);
                    skippedFractions += (interval.From - firstMonth).Days / (decimal)DateUtils.DayCountForMonth(interval.From);
                    skippedFractions += ((trailingMonth - interval.To).Days - 1) / (decimal)DateUtils.DayCountForMonth(interval.To);
                    return monthCount - skippedFractions;
                case Frequency.ANNUAL:
                    var firstYear = interval.From.AddMonths(1 - interval.From.Month);
                    firstYear = firstYear.AddDays(1 - firstYear.Day);
                    var trailingYear = interval.To.AddYears(1);
                    trailingYear = trailingYear.AddMonths(1 - trailingYear.Month);
                    trailingYear = trailingYear.AddDays(1 - trailingYear.Day);
                    int yearCount = trailingYear.Year - firstYear.Year;
                    Debug.Assert(yearCount > 0);
                    skippedFractions += (interval.From - firstYear).Days / (decimal)DateUtils.DayCountForYear(interval.From);
                    skippedFractions += ((trailingYear - interval.To).Days - 1) / (decimal)DateUtils.DayCountForYear(interval.To);
                    return yearCount - skippedFractions;
            }
            return 0M;
        }

        public override void CheckIntegrity(string position = "")
        {
            base.CheckIntegrity(position);
            if (!Interval.IsFinite && Frequency == Frequency.ONCE)
            {
                string template = "Position \"{0}\": ONCE-frequency cannot be assigned to infinitely ranged expenses.";
                throw new IntegrityException(String.Format(template, position));
            }
        }
    }
}
