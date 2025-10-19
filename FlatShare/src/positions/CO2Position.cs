using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class CO2Expense : Expense
    {

        [XmlAttribute]
        public decimal Emission { get; set; }

        [XmlIgnore]
        public int Year
        {
            get { return Interval.From.Year; }
        }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public CO2Expense()
        {
        }

        public CO2Expense(decimal magnitude, decimal emission, string from, string to)
            : base(magnitude, from, to)
        {
            Emission = emission;
        }

        public override void CheckIntegrity(string position = "")
        {
            base.CheckIntegrity(position);
            if (!Interval.IsFinite)
            {
                string template = "Position \"{0}\": Expense interval requires end date.";
                throw new IntegrityException(String.Format(template, position));
            }
            if (Interval.From.Year != Interval.To.Year)
            {
                string template = "Position \"{0}\": Expense interval must be contained in a single year.";
                throw new IntegrityException(String.Format(template, position));
            }
        }
    }

    public class CO2Allocation : IComparable<CO2Allocation>
    {
        [XmlAttribute]
        public decimal StartingAt { get; set; }

        [XmlAttribute]
        public decimal Percentage { get; set; }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public CO2Allocation()
        {
        }

        public CO2Allocation(decimal startingAt, decimal percentage)
        {
            StartingAt = startingAt;
            Percentage = percentage;
        }

        public void CheckIntegrity(string position = "")
        {
            if (Percentage > 100M)
            {
                string template = "Position \"{0}\": Tenant allocation must not be greater than 100%.";
                throw new IntegrityException(String.Format(template, position));
            }
            if (Percentage < 0M)
            {
                string template = "Position \"{0}\": Tenant allocation must not be smaller than 0%.";
                throw new IntegrityException(String.Format(template, position));
            }
        }

        public int CompareTo(CO2Allocation other)
        {
            return StartingAt.CompareTo(other.StartingAt);
        }
    }

    public class CO2AllocationTable : IComparable<CO2AllocationTable>
    {
        [XmlAttribute]
        public int EffectiveFromYear { get; set; }

        [XmlElement("Row")]
        public List<CO2Allocation> Rows { get; set; } = new List<CO2Allocation>();


        /// <summary>
        /// For deserialization only.
        /// </summary>
        public CO2AllocationTable()
        {
        }

        public CO2AllocationTable(int effectiveFromYear)
        {
            EffectiveFromYear = effectiveFromYear;
        }

        public decimal GetAllocationFactor(decimal emissionPerSQM)
        {
            CO2Allocation last = Rows[0];
            foreach (var row in Rows)
            {
                if (emissionPerSQM < row.StartingAt) break;
                last = row;
            }
            return last.Percentage / 100M;
        }

        public void Sort()
        {
            Rows.Sort();
        }

        public void CheckIntegrity(string position = "")
        {
            Rows.ElementwiseInvoke(r => r.CheckIntegrity(position));
            string prefix = String.Format("Position \"{0}\" - Allocation Table {1}: ", position, EffectiveFromYear);
            if (Rows.IsEmpty())
            {
                throw new IntegrityException(prefix + "Allocation table must not be empty.");
            }
            if (Rows[0].StartingAt != 0M)
            {
                throw new IntegrityException(prefix + "First entry of allocation table must start at 0kg CO2.");
            }
            CO2Allocation last = Rows[0];
            for (int i = 1; i < Rows.Count; i++)
            {
                CO2Allocation current = Rows[i];
                if (last.StartingAt == current.StartingAt)
                {
                    string template = "Two entries start at the same mark of {0}kg CO2.";
                    throw new IntegrityException(prefix + String.Format(template, current.StartingAt));
                }
                if (last.Percentage <= current.Percentage)
                {
                    throw new IntegrityException(prefix + "Percentages do not decline with increasing CO2 emission.");

                }
                last = current;
            }
        }


        public int CompareTo(CO2AllocationTable other)
        {
            return EffectiveFromYear.CompareTo(other.EffectiveFromYear);
        }
    }

    public sealed class CO2Position : MeteredPositionBase<CO2Expense>
    {
        [XmlElement("TenantAllocationTable")]
        public List<CO2AllocationTable> AllocationTables { get; set; } = new List<CO2AllocationTable>();

        [XmlIgnore]
        public decimal LivingSpaceSize { get; private set; }

        [XmlIgnore]
        public override DateUtils.Interval Interval
        {
            get
            {
                // Force interval to end at the last complete year since we have to know
                // the emission over a complete year to compute costs for it.
                var interval = base.Interval;
                var end = interval.To;
                bool incomplete = end.Day != 31 || end.Month != 12;
                interval.To = new DateTime(incomplete ? end.Year - 1 : end.Year, 12, 31);
                return interval;
            }
        }

        private Dictionary<int, decimal> _CachedEmissions = new Dictionary<int, decimal>();
        private Dictionary<int, decimal> _CachedAllocationFactors = new Dictionary<int, decimal>();

        /// <summary>
        /// For deserialization.
        /// </summary>
        public CO2Position() : base() { }
        public CO2Position(string name, AllocationKey allocation) : base(name, allocation) { }

        public override void OnPositionsUpdated(Positions positions)
        {
            base.OnPositionsUpdated(positions);
            LivingSpaceSize = positions.RoomData.Rooms.Where(r => r.IsLivingSpace).Sum(r => r.SquareMeter);
            // Prevent division by zero
            if (LivingSpaceSize == 0M) LivingSpaceSize = 0.00001M;
            AllocationTables.ElementwiseInvoke(t => t.Sort());
            AllocationTables.Sort();
            _CachedEmissions.Clear();
            _CachedAllocationFactors.Clear();
        }

        public CO2AllocationTable GetAllocationTable(int year)
        {
            CO2AllocationTable last = AllocationTables[0];
            foreach (var table in AllocationTables)
            {
                if (year < table.EffectiveFromYear) break;
                last = table;
            }
            return last;
        }

        public decimal GetEmission(DateUtils.Interval interval)
        {
            return Expenses.Sum(e => getRelativeMeterDifference(interval, e.Interval) * e.Emission);
        }

        public decimal GetEmission(int year)
        {
            if (year < Interval.From.Year || year > Interval.To.Year) return 0M;
            if (!_CachedEmissions.ContainsKey(year))
            {
                var start = new DateTime(year, 1, 1);
                var end = new DateTime(year, 12, 31);
                var interval = new DateUtils.Interval(start, end);
                _CachedEmissions[year] = Expenses.Where(e => e.Year == year).Sum(e => e.Emission);
            }
            return _CachedEmissions[year];
        }

        public decimal GetAllocationFactor(int year)
        {
            if (year < Interval.From.Year || year > Interval.To.Year) return 0M;
            if (!_CachedAllocationFactors.ContainsKey(year))
            {
                var emissionPerSQM = GetEmission(year) / LivingSpaceSize;
                var table = GetAllocationTable(year);
                _CachedAllocationFactors[year] = table.GetAllocationFactor(emissionPerSQM);
            }
            return _CachedAllocationFactors[year];
        }

        public decimal GetCostsWithoutAllocationFactor(DateUtils.Interval costInterval)
        {
            costInterval = costInterval.Intersect(Interval);
            return Expenses.Sum(e =>
                getRelativeMeterDifference(costInterval, e.Interval) *
                e.Magnitude);
        }

        public override decimal GetCosts(DateUtils.Interval costInterval)
        {
            costInterval = costInterval.Intersect(Interval);
            return Expenses.Sum(e =>
                GetAllocationFactor(e.Year) *
                getRelativeMeterDifference(costInterval, e.Interval) *
                e.Magnitude);
        }

        public override void CheckIntegrity()
        {
            base.CheckIntegrity();
            AllocationTables.ElementwiseInvoke(t => t.CheckIntegrity(Name));

            string prefix = String.Format("Position \"{0}\": ", Name);
            if (AllocationTables.IsEmpty() || AllocationTables[0].EffectiveFromYear > Interval.From.Year)
            {
                throw new IntegrityException(prefix + "Some years in which expenses were registered are not covered by allocation tables.");
            }
            CO2AllocationTable last = AllocationTables[0];
            for (int i = 1; i < AllocationTables.Count; i++)
            {
                CO2AllocationTable current = AllocationTables[i];
                if (last.EffectiveFromYear == current.EffectiveFromYear)
                {
                    string template = "Several allocation tables defined for Year {0}.";
                    throw new IntegrityException(prefix + String.Format(template, current.EffectiveFromYear));
                }
                last = current;
            }
        }
    }
}
