using System;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Landlord : XmlFileParser
    {
        [XmlElement]
        public BankAccount BankAccount { get; set; }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public Landlord() : base() { }

        public Landlord(IStorage storage) : base(storage, "landlord.xml")
        {

        }

        protected override void CopyFrom(object deserialized)
        {
            var obj = deserialized as Landlord;
            if (obj != null)
            {
                BankAccount = obj.BankAccount;
            }
        }

        protected override void OnUpdated()
        {
            CheckIntegrity();
        }

        public void CheckIntegrity()
        {
            if (BankAccount == null)
            {
                throw new IntegrityException(String.Format("No bank account supplied for landlord."));
            }
            BankAccount.CheckIntegrity();
        }
    }
}
