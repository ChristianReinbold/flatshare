using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class BillRecord
    {
        public static BillRecord FromBill(Bill bill, Tenants tenants)
        {
            var record = new BillRecord();
            record.TenantFile = tenants.Where(p => p.Value == bill.Tenant).Select(p => p.Key).First();
            record.BillFile = bill.FileName;
            record.CreationDate = bill.CreationDate;
            record.DueDate = bill.DueDate;
            record.Interval = bill.TotalInterval;
            record.TotalCosts = Math.Round(bill.SumShare, 2);
            record.RemainingCosts = Math.Round(bill.SumShare - bill.Prepayment, 2);
            record.Warnings = 0;
            return record;
        }

        [XmlAttribute]
        public string TenantFile { get; set; }

        [XmlAttribute]
        [DefaultValueAttribute(null)]
        public string BillFile { get; set; }

        private DateTime _CreationDate;
        [XmlIgnore]
        public DateTime CreationDate { get { return _CreationDate.Date; } set { _CreationDate = value; } }
        [XmlAttribute("CreatedAt")]
        public string CreationDateAsString
        {
            get { return DateUtils.DateAsString(CreationDate); }
            set { CreationDate = DateUtils.DateFromString(value); }
        }

        private DateTime _DueDate;
        [XmlIgnore]
        public DateTime DueDate { get { return _DueDate.Date; } set { _DueDate = value; } }
        [XmlAttribute("DueAt")]
        public string DueDateAsString
        {
            get { return DateUtils.DateAsString(DueDate); }
            set { DueDate = DateUtils.DateFromString(value); }
        }

        [XmlIgnore]
        public DateUtils.Interval Interval { get; private set; }
        [XmlAttribute("From")]
        public string FromAsString
        {
            get { return Interval.FromAsString; }
            set { Interval.FromAsString = value; }
        }
        [XmlAttribute("To")]
        public string ToAsString
        {
            get { return Interval.ToAsString; }
            set { Interval.ToAsString = value; }
        }

        [XmlAttribute]
        public decimal TotalCosts { get; set; }

        [XmlAttribute]
        public decimal RemainingCosts { get; set; }

        [XmlAttribute("Warnings")]
        [DefaultValueAttribute(0)]
        public int Warnings { get; set; }

        public BillRecord()
        {
            var from = DateTime.Now;
            Interval = new DateUtils.Interval(from);
        }

        public void CheckIntegrity()
        {
            if (!Interval.IsFinite || Interval.IsEmpty)
            {
                var template = "Bill created at {0} does not specify a valid billing period.";
                throw new IntegrityException(String.Format(template, CreationDate.ToShortDateString()));
            }
            if (CreationDate > DueDate)
            {
                var template = "Due date of Bill created at {0} is before creation time.";
                throw new IntegrityException(String.Format(template, CreationDate.ToShortDateString()));
            }
            if (CreationDate < Interval.To)
            {
                var template = "Bill created at {0} has been created during or before the billing period.";
                throw new IntegrityException(String.Format(template, CreationDate.ToShortDateString()));
            }
            if (Warnings < 0)
            {
                var template = "Bill created at {0} has a negative warnings count.";
                throw new IntegrityException(String.Format(template, CreationDate.ToShortDateString()));
            }
            if (TotalCosts < 0)
            {
                var template = "Bill created at {0} has a negative total cost.";
                throw new IntegrityException(String.Format(template, CreationDate.ToShortDateString()));
            }
            if (TotalCosts < RemainingCosts)
            {
                var template = "Bill created at {0} has a remaining cost that surpasses the total costs.";
                throw new IntegrityException(String.Format(template, CreationDate.ToShortDateString()));
            }
        }
    }
}
