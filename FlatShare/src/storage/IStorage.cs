namespace de.creinbold.FlatShare
{
    public enum StorageLocation { VIRTUAL, PHYSICAL };

    public interface IStorage
    {
        void Register(IStorageListener listener, params IStorageListener[] dependencies);
        IStorageContext Open(StorageLocation storageLocation);
        void Backup();
        void Clear();
    }
}
