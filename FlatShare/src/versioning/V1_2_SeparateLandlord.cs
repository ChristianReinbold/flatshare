using System;
using System.IO;
using System.Linq;
using System.Xml;
using Zio;

namespace de.creinbold.FlatShare.versioning
{
    class V1_2_SeparateLandlord : VersionChange
    {
        private static bool isCharged(XmlElement root)
        {
            var rents = root["Rents"];
            var deposit = root["Deposit"];
            var landlord = root["Landlord"];

            if (rents != null)
            {
                foreach (var rent in rents.ChildNodes.Cast<XmlNode>())
                {
                    var net = decimal.Parse(rent.Attributes["Net"].Value);
                    var ancillary = decimal.Parse(rent.Attributes["Ancillary"].Value);
                    if (net > 0 || ancillary > 0) return true;
                }
            }
            if (deposit != null)
            {
                if (decimal.Parse(deposit.Attributes["Amount"].Value) > 0) return true;
            }
            return false;
        }

        public override Version SourceVersion { get { return new Version(); } }
        public override Version TargetVersion { get { return new Version(1, 2); } }

        public override void Upgrade(IFileSystem fs)
        {
            XmlNode landlordBankAccount = null;
            foreach (var path in fs.EnumerateFiles("/tenants"))
            {
                XmlDocument doc = new XmlDocument();
                using (var stream = fs.OpenFile(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    doc.Load(stream);
                }
                var root = doc.DocumentElement;
                if (root["Landlord"] != null)
                {
                    landlordBankAccount = root["BankAccounts"].FirstChild;
                    root.RemoveChild(root["Landlord"]);
                }
                if (!isCharged(root))
                {
                    if (root["Rents"] != null) root.RemoveChild(root["Rents"]);
                    if (root["Deposit"] != null) root.RemoveChild(root["Deposit"]);
                    var noChargingElem = doc.CreateElement("nocharging");
                    root.PrependChild(noChargingElem);
                }

                using (var stream = fs.OpenFile(path, FileMode.Create, FileAccess.Write))
                {
                    doc.Save(stream);
                }
            }

            using (var stream = fs.OpenFile("/landlord.xml", FileMode.CreateNew, FileAccess.ReadWrite))
            {
                XmlDocument doc = new XmlDocument();
                var landlord = doc.CreateElement("Landlord");
                landlordBankAccount = doc.ImportNode(landlordBankAccount, true);
                landlord.AppendChild(landlordBankAccount);
                doc.AppendChild(landlord);
                doc.Save(stream);
            }
        }
    }
}
