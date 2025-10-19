using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Zio;
using Zio.FileSystems;

namespace de.creinbold.FlatShare
{
    public class FileStorage : IStorage
    {
        public class FileStorageContext : IStorageContext
        {
            private static StorageUpDownGrader UP_DOWN_GRADER = new StorageUpDownGrader();

            public bool Modified { get; private set; }

            private FileStorage _Parent;
            private IFileSystem _MutableStorage;
            private StorageLocation _StorageLocation;

            public FileStorageContext(FileStorage parent, StorageLocation storageLocation)
            {
                _Parent = parent;
                _StorageLocation = storageLocation;
                _MutableStorage = parent.GetMutableStorage(storageLocation);

                var sourceVersion = UP_DOWN_GRADER.GetStoredVersion(_MutableStorage);
                var targetVersion = ProductVersion.Get();
                if (UP_DOWN_GRADER.Convert(_MutableStorage, sourceVersion, targetVersion))
                {
                    parent.Backup("_v" + ProductVersion.ToString(sourceVersion));
                    Modified = true;
                }
            }

            private bool InvokeRespectingFailureMode(Action func, FailureMode mode)
            {
                if (mode == FailureMode.Throw)
                {
                    func();
                    return true;
                }
                else
                {
                    try
                    {
                        func();
                        return true;
                    }
                    catch (IntegrityException e)
                    {
                        Console.WriteLine("Integrity constraint violated. " + e.Message);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(String.Format("Exception while updating. Possibly corrupt state."));
                        Console.WriteLine(e);
                    }
                    if (mode == FailureMode.PrintAndRollback) Rollback();
                    return false;
                }
            }

            public bool NotifyStorablesChanged(IEnumerable<IStorable> storables, FailureMode failureMode = FailureMode.Throw)
            {
                Action fn = delegate ()
                {
                    storables.ElementwiseInvoke(e => e.OverwriteStorage(_MutableStorage));
                    var invalidatedListeners = _Parent.GetInvalidatedListeners(storables);
                    var sortedListeners = _Parent.GetListenersInDependencyResolvedOrder(invalidatedListeners);
                    sortedListeners.ElementwiseInvoke(l => l.OnStorageUpdated(_MutableStorage));
                };
                Modified = true;
                return InvokeRespectingFailureMode(fn, failureMode);
            }

            public void NotifyListeners()
            {
                var listeners = _Parent._DependenciesForListener.Keys;
                var sortedListeners = _Parent.GetListenersInDependencyResolvedOrder(listeners);
                sortedListeners.ElementwiseInvoke(l => l.OnStorageUpdated(_MutableStorage));
            }

            public bool NotifyFileSystemChanged(FailureMode failureMode = FailureMode.Throw)
            {
                Modified = true;
                return InvokeRespectingFailureMode(NotifyListeners, failureMode);
            }

            public void Rollback()
            {
                if (!Modified) return;

                _Parent.DisposeMutableStorage(_MutableStorage);
                _MutableStorage = _Parent.GetMutableStorage(_StorageLocation);
                try
                {
                    NotifyFileSystemChanged();
                    Modified = false;
                }
                catch (Exception e)
                {
                    _Parent.DisposeMutableStorage(_MutableStorage);
                    Console.WriteLine("Error while restoring previous state. " + e.Message);
                    Console.Write("Press key to exit application...");
                    Console.ReadKey();
                    Environment.Exit(-1);
                }
            }

            public void Dispose()
            {
                try
                {
                    if (Modified) _Parent.Store(_MutableStorage);
                }
                finally
                {
                    _Parent.DisposeMutableStorage(_MutableStorage);
                    GC.SuppressFinalize(this);
                }
            }
        }

        public enum LoadResult { Success, NoAccess, FailedParsing }

        private static readonly int DELETE_RETRIES = 10;
        private static readonly UPath _StorageFilePath = "/storage.cryp";
        private static readonly UPath _BackupDirectoryPath = "/backups";
        private static readonly UPath _DecryptedDirectoryPath = "/decrypted_storage";

        private IFileSystem _Root;
        private StreamEncryptor _StreamEncryptor;

        private Dictionary<IStorageListener, List<IStorageListener>> _DependenciesForListener = new Dictionary<IStorageListener, List<IStorageListener>>();

        public FileStorage(IFileSystem root, CommandParser cp = null)
        {
            _Root = root;
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("storage", this.ExecuteCommand, "pw | maintain | backup",
                "\"pw\": Change storage password. \"maintain\": Decrypt storage for manual modification. \"backup\": Create a backup.");
        }

        public void Register(IStorageListener listener, params IStorageListener[] dependencies)
        {
            if (_DependenciesForListener.ContainsKey(listener)) throw new ArgumentException("Storable registered already.");
            foreach (var dependency in dependencies.Where(d => d != null))
            {
                if (!_DependenciesForListener.ContainsKey(dependency))
                {
                    throw new ArgumentException(String.Format("Dependeny {0} is not registered at this storage.", dependency));
                }
            }
            _DependenciesForListener[listener] = dependencies.ToList();
        }

        public IStorageContext Open(StorageLocation storageLocation)
        {
            return new FileStorageContext(this, storageLocation);
        }

        public void Backup()
        {
            Backup("");
        }

        public void Backup(string suffix)
        {
            if (!_Root.FileExists(_StorageFilePath)) return;
            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            var backupFileName = String.Format("{0}_{1}{2}{3}", dateString,
                                               _StorageFilePath.GetNameWithoutExtension(),
                                               suffix,
                                               _StorageFilePath.GetExtensionWithDot());
            var backupPath = UPath.Combine(_BackupDirectoryPath, backupFileName);
            backupPath = IO.AddNumberIfExists(_Root, backupPath);
            _Root.CreateDirectory(_BackupDirectoryPath);
            _Root.CopyFile(_StorageFilePath, backupPath, false);
            Console.WriteLine(String.Format("Created backup file \"{0}\".", backupPath.GetName()));
        }

        public void Clear()
        {
            if (!_Root.FileExists(_StorageFilePath)) return;
            _Root.DeleteFile(_StorageFilePath);
        }

        public StreamEncryptor GetEncryptorFromUser()
        {
            while (true)
            {
                Console.Write("Set password: ");
                var encryptor = new StreamEncryptor(IO.ReadPassword(ConsoleKey.Escape));
                Console.Write("Repeat password: ");
                if (encryptor.Authentificate(IO.ReadPassword(ConsoleKey.Escape))) return encryptor;
                else Console.WriteLine("Entered passwords do not match.");
            }
        }

        public bool ChangePassword()
        {
            Console.Write("Enter old password (Press ESC to abort): ");
            try
            {
                if (_StreamEncryptor != null)
                {
                    var pw = IO.ReadPassword(ConsoleKey.Escape);
                    if (!_StreamEncryptor.Authentificate(pw))
                    {
                        Console.WriteLine("Bad password.");
                        return false;
                    }
                }
                var newEncrytor = GetEncryptorFromUser();
                var mutableStorage = GetMutableStorage(StorageLocation.VIRTUAL);
                _StreamEncryptor = newEncrytor;
                Store(mutableStorage);
                DisposeMutableStorage(mutableStorage);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private IFileSystem GetMutableStorage(StorageLocation storageLocation)
        {
            var targetVersion = ProductVersion.Get();
            IFileSystem mutableStorage = null;
            switch (storageLocation)
            {
                case StorageLocation.VIRTUAL:
                    mutableStorage = new MemoryFileSystem();
                    break;
                case StorageLocation.PHYSICAL:
                    mutableStorage = _Root.GetOrCreateSubFileSystem(_DecryptedDirectoryPath);
                    break;
                default:
                    throw new ArgumentException("Unknown storage location " + storageLocation);
            }

            if (!_Root.FileExists(_StorageFilePath)) return mutableStorage;

            using (var archiveStream = new MemoryStream())
            {
                using (var fs = _Root.OpenFile(_StorageFilePath, FileMode.Open, FileAccess.Read))
                {
                    _StreamEncryptor.Decrypt(fs, archiveStream);
                }
                archiveStream.Position = 0;
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read))
                {
                    archive.ExtractToFilesystem(mutableStorage);
                }
            }
            return mutableStorage;
        }

        private void DisposeMutableStorage(IFileSystem mutableStorage)
        {
            var subFs = mutableStorage as SubFileSystem;
            if (subFs == null) return;
            for (int i = DELETE_RETRIES - 1; i >= 0; i--)
            {
                try
                {
                    _Root.DeleteDirectory(subFs.SubPath, true);
                    break;
                }
                catch
                {
                    if (i == 0)
                        throw;
                }
            }
        }

        private void Store(IFileSystem mutableStorage)
        {
            if (!_Root.FileExists(_StorageFilePath))
            {
                while (true)
                {
                    // Force the user to enter a password.
                    try
                    {
                        _StreamEncryptor = GetEncryptorFromUser();
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("A password is mandatory.");
                    }
                }
            }

            using (var archiveStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    archive.PopulateFromFilesystem(mutableStorage);
                }
                archiveStream.Position = 0;
                using (var fsOut = _Root.OpenFile(_StorageFilePath, FileMode.Create, FileAccess.Write))
                {
                    _StreamEncryptor.Encrypt(archiveStream, fsOut);
                }
            }
        }

        public bool MaintainByUser()
        {
            Backup();
            using (var context = Open(StorageLocation.PHYSICAL))
            {
                var winPath = _Root.ConvertPathToInternal(_DecryptedDirectoryPath);
                Process.Start(winPath);
                Console.WriteLine("In maintenance mode. Files ready for modification...");
                Console.WriteLine("[Y]: Apply changes and encrypt.");
                Console.WriteLine("[N]: Discard changes.");
                while (IO.AskUser())
                {
                    if (context.NotifyFileSystemChanged(FailureMode.Print)) return true;
                    Console.WriteLine();
                    Console.WriteLine("[Y]: Apply changes and encrypt.");
                    Console.WriteLine("[N]: Discard changes.");
                }
                context.Rollback();
                return false;
            }
        }

        public LoadResult TryToLoadAndUpdate()
        {
            if (!_Root.FileExists(_StorageFilePath)) return LoadResult.NoAccess;

            while (true)
            {
                Console.Write("Enter password (Press ESC to abort): ");
                try
                {
                    var pw = IO.ReadPassword(ConsoleKey.Escape);
                    _StreamEncryptor = new StreamEncryptor(pw);
                    using (var context = Open(StorageLocation.VIRTUAL))
                    {
                        (context as FileStorageContext).NotifyListeners();
                        return LoadResult.Success;
                    }
                }
                catch (OperationCanceledException)
                {
                    return LoadResult.NoAccess;
                }
                catch (CryptographicException)
                {
                    Console.WriteLine("Bad password.");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Storage is corrupt. " + e.Message);
                    return LoadResult.FailedParsing;
                }
            }
        }

        private string ExecuteCommand(IEnumerable<string> args)
        {
            var args_list = args.ToList();
            if (args_list.Count == 0)
            {
                return "Missing option.";
            }
            switch (args_list[0])
            {
                case "pw":
                    bool changed = ChangePassword();
                    return changed ? "Changed password." : "";
                case "maintain":
                    bool success = MaintainByUser();
                    return success ? "Applied changes." : "Discarded changes.";
                case "backup":
                    Backup();
                    return "";
                default:
                    return "Unknown option " + args_list[0] + ".";
            }
        }

        private IEnumerable<IStorageListener> GetListenersInDependencyResolvedOrder(IEnumerable<IStorageListener> listeners)
        {
            var pendingListeners = new HashSet<IStorageListener>(listeners);
            while (pendingListeners.Count > 0)
            {
                var prevPending = new HashSet<IStorageListener>(pendingListeners);
                foreach (var pendingListener in prevPending)
                {
                    if (pendingListeners.Where(o => _DependenciesForListener[pendingListener].Contains(o)).Count() > 0)
                        continue;
                    yield return pendingListener;
                    pendingListeners.Remove(pendingListener);
                }
                if (pendingListeners.Count == prevPending.Count)
                {
                    throw new Exception("Cyclic dependencies in storage listeners detected.");
                }
            }
        }

        private IEnumerable<IStorageListener> GetInvalidatedListeners(IEnumerable<IStorable> updatedStorables)
        {
            var unresolvedInvalidations = new HashSet<IStorageListener>(updatedStorables);
            var invalidListeners = new HashSet<IStorageListener>(updatedStorables);
            while (unresolvedInvalidations.Count > 0)
            {
                var newDependencies = new HashSet<IStorageListener>();
                foreach (var pair in _DependenciesForListener)
                {
                    if (!pair.Value.Intersect(unresolvedInvalidations).IsEmpty())
                    {
                        if (invalidListeners.Add(pair.Key))
                        {
                            newDependencies.Add(pair.Key);
                        }
                    }
                }
                unresolvedInvalidations = newDependencies;
            }
            return invalidListeners;
        }
    }
}
