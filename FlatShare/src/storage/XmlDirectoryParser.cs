using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Zio;

namespace de.creinbold.FlatShare
{
    public abstract class XmlDirectoryParser<T> : Dictionary<string, T>, IStorable
    {
        private readonly UPath _Directory;

        public IEnumerable<T> Entries { get { return Values; } }

        public XmlDirectoryParser(IStorage storage, UPath directory, params IStorable[] dependentTargets)
        {
            storage.Register(this, dependentTargets);
            _Directory = directory.ToAbsolute();
        }

        public void Update()
        {
            OnUpdated();
        }

        protected virtual void OnUpdated() { }

        public void OnStorageUpdated(IFileSystem storage)
        {
            Clear();
            if (!storage.DirectoryExists(_Directory))
            {
                Update();
                return;
            }

            foreach (var path in storage.EnumerateFiles(_Directory))
            {
                using (var fs = storage.OpenFile(path, FileMode.Open, FileAccess.Read))
                {
                    string rootName;
                    using (var xml = XmlReader.Create(fs))
                    {
                        while (xml.NodeType != XmlNodeType.Element)
                            xml.Read();
                        rootName = xml.Name;
                    }
                    fs.Position = 0;
                    var assembly = GetType().Assembly;
                    var typeInXml = assembly.GetType(String.Format("{0}.{1}", GetType().Namespace, rootName));
                    var serializer = new XmlSerializer(typeInXml);
                    var deserialized = (T)serializer.Deserialize(fs);
                    Add(path.GetName(), deserialized);
                }
            }
            Update();
        }

        public void OverwriteStorage(IFileSystem storage)
        {
            if (storage.DirectoryExists(_Directory))
                storage.DeleteDirectory(_Directory, true);
            storage.CreateDirectory(_Directory);
            foreach (var tuple in this)
            {
                var fileName = tuple.Key;
                var entry = tuple.Value;
                var serializer = new XmlSerializer(entry.GetType());
                using (var fs = storage.OpenFile(UPath.Combine(_Directory, fileName), FileMode.Create, FileAccess.Write))
                {
                    serializer.Serialize(fs, entry, XmlSerializeUtils.EMPTY_NS);
                }
            }
        }
    }
}
