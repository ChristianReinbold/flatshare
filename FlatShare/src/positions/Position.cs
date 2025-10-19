using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public abstract class PositionBase<T> : IPosition where T : Expense
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public AllocationKey Allocation { get; set; }

        [XmlElement("Expense")]
        public List<T> Expenses { get; set; } = new List<T>();

        [XmlAttribute]
        [DefaultValueAttribute(false)]
        public bool Finished { get; set; }

        [XmlIgnore]
        public virtual DateUtils.Interval Interval
        {
            get
            {
                var firstInterval = Expenses.First().Interval;
                var lastInterval = Expenses.Last().Interval;
                if (lastInterval.IsFinite)
                {
                    return new DateUtils.Interval(firstInterval.From, lastInterval.To);
                }
                else
                {
                    return new DateUtils.Interval(firstInterval.From);
                }
            }
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public PositionBase()
        {
        }

        public PositionBase(string name, AllocationKey allocation)
        {
            Name = name;
            Allocation = allocation;
        }

        public virtual void CheckIntegrity()
        {
            foreach (var expense in Expenses)
            {
                expense.CheckIntegrity(Name);
            }
            var violatedIndex = DateUtils.FirstNonConsecutiveIntervalIndex(Expenses.Select(e => e.Interval));
            if (violatedIndex >= 0)
            {
                var template = "Position \"{0}\": The {1}. expense does not immediately follow the previous expense.";
                throw new IntegrityException(String.Format(template, Name, violatedIndex + 1));
            }
            if (Expenses.Count < 1)
            {
                var template = "Position \"{0}\": No expenses specified.";
                throw new IntegrityException(String.Format(template, Name));
            }
        }

        public override string ToString()
        {
            return String.Format("{1}({0})", GetType().Name, Name);
        }

        public abstract void OnPositionsUpdated(Positions positions);
        public abstract decimal GetCosts(DateUtils.Interval interval);
    }

    public sealed class Position : PositionBase<RepeatedExpense>
    {
        /// <summary>
        /// For deserialization.
        /// </summary>
        public Position() : base() { }

        public Position(string name, AllocationKey allocation)
            : base(name, allocation)
        {
        }

        public override decimal GetCosts(DateUtils.Interval interval)
        {
            return Expenses.Sum(e => e.CountRepetitions(interval) * e.Magnitude);
        }

        public override void OnPositionsUpdated(Positions positions) { }
    }
}
