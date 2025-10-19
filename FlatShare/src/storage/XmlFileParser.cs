using System.IO;
using System.Xml.Serialization;
using Zio;

namespace de.creinbold.FlatShare
{
    public abstract class XmlFileParser : IStorable
    {
        private readonly UPath _XmlPath;

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public XmlFileParser() { }

        public XmlFileParser(IStorage storage, UPath path, params IStorable[] dependencies)
        {
            storage.Register(this, dependencies);
            _XmlPath = path.ToAbsolute();
        }

        public void Update()
        {
            OnUpdated();
        }

        protected virtual void OnUpdated() { }

        protected abstract void CopyFrom(object deserialized);

        public virtual void OnStorageUpdated(IFileSystem storage)
        {
            var serializer = new XmlSerializer(GetType());
            try
            {
                using (var fs = storage.OpenFile(_XmlPath, FileMode.Open, FileAccess.Read))
                {
                    CopyFrom(serializer.Deserialize(fs));
                }
            }
            catch (FileNotFoundException)
            {
                CopyFrom(null);
            }
            Update();
        }

        public void OverwriteStorage(IFileSystem storage)
        {
            var serializer = new XmlSerializer(GetType());
            if (!_XmlPath.GetDirectory().IsRoot())
                storage.CreateDirectory(_XmlPath.GetDirectory());
            using (var fs = storage.OpenFile(_XmlPath, FileMode.Create, FileAccess.Write))
            {
                serializer.Serialize(fs, this, XmlSerializeUtils.EMPTY_NS);
            }
        }
    }
}
