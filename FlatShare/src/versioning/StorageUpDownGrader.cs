using Dijkstra.NET.Graph;
using Dijkstra.NET.ShortestPath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{

    public class StorageUpDownGrader
    {
        private static readonly string VERSION_FILE = "/.version";
        private static readonly Version DEFAULT_VERSION = new Version();

        private sealed class Op : IEquatable<Op>
        {
            private Action<IFileSystem> _Fn;
            public Op(Action<IFileSystem> fn) { _Fn = fn; }
            public bool Equals(Op other) { return this._Fn == other._Fn; }
            public void Invoke(IFileSystem fs) { _Fn(fs); }
        }

        private static bool OverridesMethod(object obj, string method)
        {
            return obj.GetType().GetMethod(method).DeclaringType == obj.GetType();
        }

        private static IEnumerable<VersionChange> InstantiateVersionChangesInAssembly()
        {
            var assembly = typeof(StorageUpDownGrader).Assembly;
            var types = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(VersionChange)));
            return types.Select(t => (VersionChange)Activator.CreateInstance(t));
        }

        private static Version Prune(Version v)
        {
            return new Version(v.Major, v.Minor);
        }

        private List<Version> _RegisteredVersions;
        private Graph<Version, Op> _VersionGraph;
        private Dictionary<Version, uint> _Keys;

        public StorageUpDownGrader()
        {
            _VersionGraph = new Graph<Version, Op>();
            _Keys = new Dictionary<Version, uint>();
            _Keys.Add(DEFAULT_VERSION, _VersionGraph.AddNode(DEFAULT_VERSION));

            foreach (var vc in InstantiateVersionChangesInAssembly())
            {
                var sv = Prune(vc.SourceVersion);
                var tv = Prune(vc.TargetVersion);
                if (!_Keys.ContainsKey(sv)) _Keys.Add(sv, _VersionGraph.AddNode(sv));
                if (!_Keys.ContainsKey(tv)) _Keys.Add(tv, _VersionGraph.AddNode(tv));
                if (OverridesMethod(vc, "Upgrade")) _VersionGraph.Connect(_Keys[sv], _Keys[tv], 1, new Op(vc.Upgrade));
                if (OverridesMethod(vc, "Downgrade")) _VersionGraph.Connect(_Keys[tv], _Keys[sv], 1, new Op(vc.Downgrade));
            }
            _RegisteredVersions = _Keys.Keys.ToList();
            _RegisteredVersions.Sort();
        }

        public Version GetStoredVersion(IFileSystem fs)
        {
            if (!fs.FileExists(VERSION_FILE)) return ProductVersion.Get();
            return Version.Parse(fs.ReadAllText(VERSION_FILE));
        }

        public void StoreVersion(IFileSystem fs, Version version)
        {
            fs.WriteAllText(VERSION_FILE, GetRegisteredVersion(version).ToString(2));
        }

        public bool Convert(IFileSystem fs, Version source, Version target)
        {
            source = GetRegisteredVersion(source);
            target = GetRegisteredVersion(target);

            if (source.Equals(target))
            {
                StoreVersion(fs, source);
                return false;
            }

            var dijsktraResult = _VersionGraph.Dijkstra(_Keys[source], _Keys[target]);
            if (!dijsktraResult.IsFounded)
            {
                var msg = string.Format("Storage version (v{0}) is not compatible with program version (v{1}).",
                                        source.ToString(2), ProductVersion.Get().ToString(2));
                throw new InvalidDataException(msg);
            }

            var nodeIDs = dijsktraResult.GetPath().ToList();
            var current = source;
            for (int i = 0; i < nodeIDs.Count - 1; i++)
            {
                var fromNode = _VersionGraph[nodeIDs[i]] as Node<Version, Op>;
                var updateOp = fromNode.GetFirstEdgeCustom(nodeIDs[i + 1]);
                var newCurrent = _VersionGraph[nodeIDs[i + 1]].Item;
                updateOp.Invoke(fs);
                var msg = string.Format("Converted storage: v{0} --> v{1}",
                                        current.ToString(2), newCurrent.ToString(2));
                Console.WriteLine(msg);
                current = newCurrent;
            }
            Debug.Assert(target.Equals(current));
            StoreVersion(fs, current);
            return true;
        }

        private Version GetRegisteredVersion(Version v)
        {
            int idx = _RegisteredVersions.BinarySearch(v);
            if (idx < 0) idx = ~idx - 1;
            return _RegisteredVersions[idx];
        }
    }
}
