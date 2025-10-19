using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Service
    {
        [XmlText]
        public string PositionRef { get; set; }

        [XmlIgnore]
        public IPosition AssociatedPosition { get; set; }

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

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public Service()
        {
        }

        public Service(string positionRef, string from = "", string to = "")
        {
            PositionRef = positionRef;
            Interval = new DateUtils.Interval(from, to);
        }

        public override string ToString()
        {
            return String.Format("{0} in {1}", AssociatedPosition.Name, Interval);
        }

        public decimal GetCosts(DateUtils.Interval interval)
        {
            return AssociatedPosition.GetCosts(interval.Intersect(Interval));
        }
    }
}
