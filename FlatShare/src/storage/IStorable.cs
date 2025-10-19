using Zio;

namespace de.creinbold.FlatShare
{
    public interface IStorable : IStorageListener
    {
        void OverwriteStorage(IFileSystem storage);
    }
}
