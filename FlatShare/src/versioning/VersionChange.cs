using System;
using Zio;

namespace de.creinbold.FlatShare
{
    public abstract class VersionChange : IEquatable<VersionChange>
    {
        public abstract Version SourceVersion { get; }
        public abstract Version TargetVersion { get; }
        public virtual void Upgrade(IFileSystem fs) { }
        public virtual void Downgrade(IFileSystem fs) { }

        public bool Equals(VersionChange other)
        {
            return this == other;
        }
    }
}
