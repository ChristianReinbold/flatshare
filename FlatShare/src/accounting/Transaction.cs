using IbanValidation;
using System;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Transaction
    {
        private DateTime _Date;
        [XmlIgnore]
        public DateTime Date { get { return _Date.Date; } set { _Date = value; } }
        [XmlAttribute("Date")]
        public string DateAsString
        {
            get { return DateUtils.DateAsString(Date); }
            set { Date = DateUtils.DateFromString(value); }
        }

        [XmlIgnore]
        public IBAN IBAN { get; set; }
        [XmlAttribute("IBAN")]
        public string IBANAsString { get { return IBAN; } set { IBAN = value; } }

        [XmlAttribute]
        public decimal Amount { get; set; }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public Transaction() { }

        public Transaction(IBAN iban, string date, decimal amount)
        {
            IBAN = iban;
            DateAsString = date;
            Amount = amount;
        }

        public Transaction(IBAN iban, DateTime date, decimal amount)
        {
            IBAN = iban;
            _Date = date;
            Amount = amount;
        }

        public void CheckIntegrity()
        {
            var result = IBAN.Validate();
            if (result != IbanValidationResult.IsValid)
            {
                var template = "Transaction at {0}: Invalid IBAN \"{1}\" (Reason: {2}).";
                throw new IntegrityException(String.Format(template, DateAsString, IBAN, result));
            }
        }

        public override bool Equals(object obj)
        {
            var castObj = obj as Transaction;
            if (castObj == null)
                return false;
            return IBAN.Equals(castObj.IBAN) && Date.Equals(castObj.Date) && Amount.Equals(castObj.Amount);
        }

        public override int GetHashCode()
        {
            return IBAN.GetHashCode() ^ Date.GetHashCode() ^ Amount.GetHashCode();
        }

        public override string ToString()
        {
            var absAmount = Math.Abs(Amount);
            var direction = Amount > 0 ? "from" : "to";
            var template = "{0:N2}{1} at {2} {3} {4}";
            return String.Format(template, absAmount, Currency.CURRENT.Symbol, DateAsString, direction, IBAN);
        }
    }
}
