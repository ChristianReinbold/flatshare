using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class ServiceProvider : IBankAccountOwner
    {
        [XmlAttribute]
        public string Name { get; set; }

        public List<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        public List<Service> Services { get; set; } = new List<Service>();

        /// <summary>
        /// For deserialization.
        /// </summary>
        public ServiceProvider()
        {
        }

        public ServiceProvider(string name)
        {
            Name = name;
        }

        public void CheckIntegrity()
        {
            if (Services.Count < 1)
            {
                var template = "Service Provider \"{0}\": No services specified.";
                throw new IntegrityException(String.Format(template, Name));
            }
            BankAccounts.ElementwiseInvoke(a => a.CheckIntegrity(Name));
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
