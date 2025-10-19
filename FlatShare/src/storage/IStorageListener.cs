using Zio;

namespace de.creinbold.FlatShare
{
    public interface IStorageListener
    {
        void OnStorageUpdated(IFileSystem storage);
    }
}
