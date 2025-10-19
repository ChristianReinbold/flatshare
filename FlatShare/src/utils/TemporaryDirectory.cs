using System;
using System.IO;

namespace de.creinbold.FlatShare
{
    public class TemporaryDirectory : IDisposable
    {
        public static implicit operator string(TemporaryDirectory dir)
        {
            return dir._Path;
        }

        private readonly string _Path;
        public TemporaryDirectory()
        {
            string uniquePath;
            do
            {
                Guid guid = Guid.NewGuid();
                string uniqueSubFolderName = guid.ToString();
                uniquePath = Path.GetTempPath() + uniqueSubFolderName;
            } while (Directory.Exists(uniquePath));
            Directory.CreateDirectory(uniquePath);
            _Path = uniquePath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_Path, true);
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}
