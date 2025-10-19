using System;
using System.Collections.Generic;

namespace de.creinbold.FlatShare
{
    public enum FailureMode { Throw, Print, PrintAndRollback };

    public interface IStorageContext : IDisposable
    {
        bool NotifyStorablesChanged(IEnumerable<IStorable> storables, FailureMode failureMode = FailureMode.Throw);
        bool NotifyFileSystemChanged(FailureMode failureMode = FailureMode.Throw);
        void Rollback();
        bool Modified { get; }
    }
}
