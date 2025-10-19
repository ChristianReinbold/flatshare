using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class RoomAllocation
    {
        [XmlIgnore]
        public DateUtils.Interval Interval { get; private set; } = new DateUtils.Interval();

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

        [XmlText]
        public string RoomGroup { get; set; }

        [XmlAttribute]
        public int PersonCount { get; set; }

        [XmlAttribute]
        [DefaultValueAttribute(false)]
        public bool AllocateVacantRooms { get; set; }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public RoomAllocation()
        {
        }

        public RoomAllocation(string roomGroup, int personCount, string from, string to = "", bool allocateVacantRooms = false)
        {
            FromAsString = from;
            ToAsString = to;
            RoomGroup = roomGroup;
            PersonCount = personCount;
            AllocateVacantRooms = allocateVacantRooms;
        }

        public void CheckIntegrity(string tenant = "")
        {
            if (Interval.IsEmpty) throw new IntegrityException(String.Format("Tenant \"{0}\": Room allocation end date is located before start date.", tenant));
            if (PersonCount < 1) throw new IntegrityException(String.Format("Tenant \"{0}\": Non-positive number of persons.", tenant));
        }

        public override string ToString()
        {
            if (AllocateVacantRooms)
            {
                return String.Format("\"{0}\" + vacant rooms allocated in {1} for {2} person{3}.",
                                     RoomGroup, Interval, PersonCount, PersonCount > 1 ? "s" : "");
            }
            else
            {
                return String.Format("\"{0}\" allocated in {1} for {2} person{3}.", RoomGroup, Interval, PersonCount, PersonCount > 1 ? "s" : "");
            }

        }
    }
}
