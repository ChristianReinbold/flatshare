using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Deposit
    {
        [XmlAttribute]
        public decimal Amount { get; set; } = 0M;
        [XmlIgnore]
        public decimal? Ancillary { get; set; } = null;
        [XmlIgnore]
        public decimal? Retained { get; set; } = null;

        [XmlAttribute("Ancillary")]
        [DefaultValueAttribute("")]
        public string AncillaryAsString
        {
            get
            {
                return Ancillary.ToString();
            }
            set
            {
                if (String.IsNullOrEmpty(value)) Ancillary = null;
                else Ancillary = Decimal.Parse(value);
            }
        }
        [XmlAttribute("Retained")]
        [DefaultValueAttribute("")]
        public string RetainedAsString
        {
            get
            {
                return Retained.ToString();
            }
            set
            {
                if (String.IsNullOrEmpty(value)) Retained = null;
                else Retained = Decimal.Parse(value);
            }
        }

        [XmlIgnore]
        public bool DoPayout { get { return Retained.HasValue; } }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public Deposit()
        {
        }

        public Deposit(decimal amount, decimal? ancillary = null, decimal? retained = null)
        {
            Amount = amount;
            Ancillary = ancillary;
            Retained = retained;
        }

        public void CheckIntegrity(string tenant = "")
        {
            if (Amount < 0M) throw new IntegrityException(String.Format("Tenant \"{0}\": Negative deposit.", tenant));
            if (Retained.HasValue && Retained < 0M) throw new IntegrityException(String.Format("Tenant \"{0}\": Negative retained amount in deposit.", tenant));
            if (Ancillary.HasValue != Retained.HasValue) throw new IntegrityException(String.Format("Tenant \"{0}\": Ancillary and Retained have not been specified both in deposit.", tenant));
            if (Ancillary.HasValue && (Ancillary.Value > Amount || Ancillary.Value < 0M)) throw new IntegrityException(String.Format("Tenant \"{0}\": Invalid ancillary prepayment of deposit.", tenant));
        }

        public override string ToString()
        {
            if (Ancillary.HasValue) return String.Format("deposit of {0} ({1} retained, {2} as prepayment)", Amount, Retained, Ancillary);
            else return String.Format("deposit of {0}", Amount);
        }
    }
}
