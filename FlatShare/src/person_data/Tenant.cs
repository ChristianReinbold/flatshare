using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Tenant : IBankAccountOwner
    {
        public class AddressLine { }

        public class Space : AddressLine
        {
            public override string ToString()
            {
                return "";
            }
        }

        public class Line : AddressLine
        {
            [XmlText]
            public string String { get; set; }

            public override string ToString()
            {
                return String;
            }

            public static implicit operator Line(string s)
            {
                var l = new Line();
                l.String = s;
                return l;
            }

            public static implicit operator string(Line l)
            {
                return l?.ToString();
            }
        }

        public enum Genders { MALE, FEMALE }

        [XmlAttribute]
        [DefaultValueAttribute(null)]
        public string Title { get; set; }
        [XmlAttribute]
        public string FirstName { get; set; }
        [XmlAttribute]
        [DefaultValueAttribute(null)]
        public string MiddleName { get; set; }
        [XmlAttribute]
        public string LastName { get; set; }
        [XmlIgnore]
        public string Name
        {
            get
            {
                var nameList = new string[] { FirstName, MiddleName, LastName };
                return String.Join(" ", nameList.Where(n => !String.IsNullOrEmpty(n)));
            }
        }
        [XmlAttribute]
        public Genders Gender { get; set; }

        [XmlElement("nocharging")]
        [DefaultValueAttribute(null)]
        public object XMLNoChargingFlag { get; set; } = null;

        [XmlIgnore]
        public bool IsCharged
        {
            get { return XMLNoChargingFlag == null; }
            set { XMLNoChargingFlag = value ? null : new object(); }
        }

        [XmlArray("Address")]
        [XmlArrayItem("Line", typeof(Line))]
        [XmlArrayItem("Space", typeof(Space))]
        public List<AddressLine> Address = new List<AddressLine>();

        [XmlIgnore]
        public String AddressAsString
        {
            get
            {
                return String.Join("\n", Address.Select(a => a.ToString()));
            }
            set
            {
                Address.Clear();
                foreach (var line in value.Split('\n').Select(s => s.Trim()))
                {
                    if (String.IsNullOrEmpty(line)) Address.Add(new Space());
                    else Address.Add((Line)line);
                }
            }
        }

        [XmlIgnore]
        public String AddressAsSingleLine
        {
            get
            {
                return String.Join(", ", Address.Select(a => a.ToString()).Where(s => !String.IsNullOrWhiteSpace(s)));
            }
        }

        [DefaultValueAttribute(null)]
        public Deposit Deposit { get; set; } = null;
        public List<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        public List<RoomAllocation> RoomAllocations { get; set; } = new List<RoomAllocation>();
        public List<MonthlyRent> Rents { get; set; } = new List<MonthlyRent>();

        [XmlIgnore]
        public DateUtils.Interval AllocationInterval
        {
            get
            {
                var first = RoomAllocations.First();
                var last = RoomAllocations.Last();
                return new DateUtils.Interval(first.FromAsString, last.ToAsString);
            }
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public Tenant()
        {
        }

        public Tenant(string firstName, string lastName, Genders gender)
        {
            FirstName = firstName;
            LastName = lastName;
            Gender = gender;
        }

        public override string ToString()
        {
            return Name;
        }

        public void CheckIntegrity(DateUtils.IDateProvider dateProvider = null)
        {
            if (RoomAllocations.Count == 0) throw new IntegrityException(String.Format("Tenant \"{0}\": No room allocations", Name));
            RoomAllocations.ElementwiseInvoke(a => a.CheckIntegrity(Name));
            BankAccounts.ElementwiseInvoke(ba => ba.CheckIntegrity(Name));
            Rents.ElementwiseInvoke(r => r.CheckIntegrity(Name));

            var violatedIndex = DateUtils.FirstUnorderedIntervalIndex(RoomAllocations.Select(ra => ra.Interval));
            if (violatedIndex >= 0)
            {
                var template = "Tenant \"{0}\": The {1}. room allocation does not start after the previous one.";
                throw new IntegrityException(String.Format(template, Name, violatedIndex + 1));
            }
            violatedIndex = DateUtils.FirstNonConsecutiveIntervalIndex(Rents.Select(r => r.Interval));
            if (violatedIndex >= 0)
            {
                var template = "Tenant \"{0}\": The {1}. rent entry does not immediately follow the previous one.";
                throw new IntegrityException(String.Format(template, Name, violatedIndex + 1));
            }

            if (IsCharged)
            {
                if (Rents.Count == 0)
                {
                    var msg = String.Format("Tenant \"{0}\": No rents specified. " +
                                            "Consider using the <nocharging /> xml element if the tenant is not " +
                                            "supposed to pay the landlord.", Name);
                    throw new IntegrityException(msg);
                }
                if (Deposit == null) throw new IntegrityException(String.Format("Tenant \"{0}\": No deposit specified.", Name));
            }
            else
            {
                if (Deposit != null) throw new IntegrityException(String.Format("Tenant \"{0}\": Deposit specified although marked as <nocharging />", Name));
                if (Rents.Count > 0) throw new IntegrityException(String.Format("Tenant \"{0}\": Rent specified although marked as <nocharging />", Name));
            }

            if (Deposit != null)
            {
                Deposit.CheckIntegrity(Name);

                if (dateProvider == null) dateProvider = new DateUtils.SystemDateProvider();
                var interval = AllocationInterval;
                bool moveOutDateKnown = interval.IsFinite;
                bool movedOut = moveOutDateKnown ? dateProvider.Now >= interval.To : false;
                if (Deposit.DoPayout && !moveOutDateKnown) throw new IntegrityException(String.Format("Tenant \"{0}\": Deposit payback values already present although rooms still are permanently allocated.", Name));
                if (Deposit.DoPayout && !movedOut) Console.WriteLine(String.Format("Warning: Deposit payback values for Tenant \"{0}\" already entered although tenant did not move out yet.", Name));
                if (movedOut && !Deposit.DoPayout) Console.WriteLine(String.Format("Warning: Tenant \"{0}\" moved out but no deposit payback values are specified.", Name));
            }

            if (Rents.Count > 0)
            {
                if (RoomAllocations.First().Interval.From != Rents.First().Interval.From)
                {
                    throw new IntegrityException(String.Format("Tenant \"{0}\": First room allocation and first rent dates do not match.", Name));
                }
                var interval1 = RoomAllocations.Last().Interval;
                var interval2 = Rents.Last().Interval;
                if (interval1.IsFinite != interval2.IsFinite || (interval1.IsFinite && interval1.To != interval2.To))
                {
                    throw new IntegrityException(String.Format("Tenant \"{0}\": Last room allocation and last rent dates do not match.", Name));
                }
            }
        }
    }
}
