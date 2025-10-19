using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class MeterFile
    {
        [XmlText]
        public string Name { get; set; }


        /// <summary>
        /// For deserialization.
        /// </summary>
        public MeterFile() { }

        public MeterFile(string name)
        {
            Name = name;
        }


        public static implicit operator MeterFile(string s) => new MeterFile(s);
    }

    public abstract class MeteredPositionBase<T> : PositionBase<T>, IMeteredPosition where T : Expense
    {

        public List<MeterFile> MeterFiles { get; set; } = new List<MeterFile>();

        [XmlIgnore]
        private List<Meter> Meters_ = new List<Meter>();
        public ICollection<Meter> Meters { get { return Meters_; } }

        [XmlIgnore]
        public virtual DateUtils.Interval MeteredInterval
        {
            get
            {
                return new DateUtils.Interval(Meters.First().Interval.From, Meters.Last().Interval.To);
            }
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public MeteredPositionBase() : base() { }


        public MeteredPositionBase(string name, AllocationKey allocation) : base(name, allocation) { }

        protected decimal getRelativeMeterDifference(DateUtils.Interval nomInterval, DateUtils.Interval denomInterval)
        {
            if (!MeteredInterval.Contains(denomInterval))
            {
                string template = "Insufficient meter values for calculating the consumption from {0} to {1} for position \"{2}\".";
                throw new IntegrityException(String.Format(template, denomInterval.FromAsString, denomInterval.ToAsString, Name));
            }
            nomInterval = nomInterval.Intersect(denomInterval);

            decimal denom = 0;
            decimal nom = 0;
            foreach (var meter in Meters)
            {
                var meterNomInterval = meter.Interval.Intersect(nomInterval);
                var meterDenomInterval = meter.Interval.Intersect(denomInterval);
                denom += meter.GetDifference(meterDenomInterval);
                nom += meter.GetDifference(meterNomInterval);
            }
            return denom == 0M ? 0M : nom / denom;
        }

        public override void OnPositionsUpdated(Positions positions)
        {
            Meters_.Clear();
            var availableMeters = positions.Meters;
            foreach (var meterFile in MeterFiles)
            {
                Meter referencedMeter;
                try
                {
                    referencedMeter = availableMeters[meterFile.Name];
                }
                catch (KeyNotFoundException)
                {
                    var wrongName = meterFile.Name;
                    var altName = availableMeters.Keys.OrderBy(file => wrongName.DamerauLevenshteinDistanceTo(file)).FirstOrDefault();
                    if (altName != null)
                    {
                        var template = "Position \"{0}\" references unknown meter file \"{1}\". Alternative meter file: \"{2}\" ({3})?";
                        throw new IntegrityException(String.Format(template, Name, wrongName, altName, availableMeters[altName].Name));
                    }
                    else
                    {
                        var template = "Position \"{0}\" references unknown meter file \"{1}\".";
                        throw new IntegrityException(String.Format(template, Name, wrongName));
                    }
                }
                Meters_.Add(referencedMeter);
            }
        }

        public override void CheckIntegrity()
        {
            base.CheckIntegrity();

            foreach (var meter in Meters)
            {
                if (!meter.Unit.Equals(Meters.First().Unit))
                {
                    var template = "Position \"{0}\": Assigned meters have different units.";
                    throw new IntegrityException(String.Format(template, Name));
                }
            }

            var violatedIndex = DateUtils.FirstNonConsecutiveIntervalIndex(Meters.Select(r => r.Interval));
            if (violatedIndex >= 0)
            {
                var cur = Meters_[violatedIndex];
                var prev = Meters_[violatedIndex - 1];
                var template = "Position \"{0}\": The metered interval {1} of \"{2}\" does not immediately follow the metered interval {3} of \"{4}\".";
                throw new IntegrityException(String.Format(template, Name, cur.Interval.ToString(), cur.Name, prev.Interval.ToString(), prev.Name));
            }

            foreach (var expense in Expenses)
            {
                if (!expense.Interval.IsFinite)
                {
                    var template = "Position \"{0}\": No expenses with an infinite interval permitted.";
                    throw new IntegrityException(String.Format(template, Name));
                }
            }
        }
    }

    public sealed class MeteredPosition : MeteredPositionBase<Expense>
    {
        /// <summary>
        /// For deserialization.
        /// </summary>
        public MeteredPosition() : base() { }

        public MeteredPosition(string name, AllocationKey allocation) : base(name, allocation) { }

        public override decimal GetCosts(DateUtils.Interval interval)
        {
            return Expenses.Sum(e => getRelativeMeterDifference(interval, e.Interval) * e.Magnitude);
        }
    }
}
