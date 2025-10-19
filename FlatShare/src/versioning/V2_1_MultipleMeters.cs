using System;
using System.IO;
using System.Xml;
using Zio;

namespace de.creinbold.FlatShare.versioning
{
    class V2_1_MultipleMeters : VersionChange
    {
        private void ConvertMeterFileAttributesToLists(IFileSystem fs)
        {
            foreach (var path in fs.EnumerateFiles("/positions"))
            {
                XmlDocument doc = new XmlDocument();
                using (var stream = fs.OpenFile(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    doc.Load(stream);
                }
                var root = doc.DocumentElement;
                if (!root.HasAttribute("MeterFile")) continue;

                var oldAttr = root.GetAttributeNode("MeterFile");
                var newElemList = doc.CreateElement("MeterFiles");
                var newElem = doc.CreateElement("MeterFile");
                newElem.InnerText = oldAttr.Value;
                newElemList.AppendChild(newElem);

                root.RemoveAttributeNode(oldAttr);
                root.PrependChild(newElemList);

                using (var stream = fs.OpenFile(path, FileMode.Create, FileAccess.Write))
                {
                    doc.Save(stream);
                }
            }
        }

        private void AddIdAttributeToMeters(IFileSystem fs)
        {
            foreach (var path in fs.EnumerateFiles("/meters"))
            {
                XmlDocument doc = new XmlDocument();
                using (var stream = fs.OpenFile(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    doc.Load(stream);
                }
                var root = doc.DocumentElement;
                root.SetAttribute("Id", "unknown");
                using (var stream = fs.OpenFile(path, FileMode.Create, FileAccess.Write))
                {
                    doc.Save(stream);
                }
            }
        }

        public override Version SourceVersion { get { return new Version(2, 0); } }
        public override Version TargetVersion { get { return new Version(2, 1); } }

        public override void Upgrade(IFileSystem fs)
        {
            ConvertMeterFileAttributesToLists(fs);
            AddIdAttributeToMeters(fs);
        }
    }
}
