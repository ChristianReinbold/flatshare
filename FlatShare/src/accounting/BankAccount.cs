using IbanValidation;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class BankAccount
    {
        [XmlAttribute]
        public string Owner { get; set; }

        [XmlIgnore]
        public IBAN IBAN { get; set; }
        [XmlAttribute("IBAN")]
        public string IBANAsString { get { return IBAN; } set { IBAN = value; } }

        [XmlIgnore]
        public BIC BIC { get; set; } = null;
        [XmlAttribute("BIC")]
        [DefaultValueAttribute("")]
        public string BICAsString { get { return BIC; } set { BIC = String.IsNullOrEmpty(value) ? null : value; } }

        public BankAccount() { }

        public BankAccount(string owner, IBAN iban, BIC bic = null)
        {
            Owner = owner;
            IBAN = iban;
            BIC = bic;
        }

        public override bool Equals(object obj)
        {
            var other = obj as BankAccount;
            if (other == null) return false;
            return IBAN.Equals(other.IBAN);
        }

        public override int GetHashCode()
        {
            return IBAN.GetHashCode();
        }

        public void CheckIntegrity(string prefix = null)
        {
            var template = "Invalid {0} \"{1}\" (Reason: {2}).";
            if (String.IsNullOrWhiteSpace(prefix)) template = prefix + ": " + template;

            var iban_check = IBAN.Validate();
            if (iban_check != IbanValidationResult.IsValid)
            {
                throw new IntegrityException(String.Format(template, "IBAN", IBAN, iban_check));
            }
            var bic_check = BIC?.Validate();
            if (bic_check.HasValue && bic_check != BIC.ValidationResult.IsValid)
            {
                throw new IntegrityException(String.Format(template, "BIC", BIC, bic_check));
            }
        }

        public override string ToString()
        {
            if (String.IsNullOrEmpty(BIC)) return String.Format("bank account of {0} (IBAN: {1})", Owner, IBAN);
            else return String.Format("bank account of {0} (IBAN: {1}, BIC: {2})", Owner, IBAN, BIC);
        }
    }
}
