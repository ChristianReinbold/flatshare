using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{
    public class Shares : IStorageListener
    {
        private struct RessourceInfo
        {
            public DateUtils.Interval Interval { get; set; }
            public RessourceDeclaration Ressource { get; set; }
            public IList<Room> VacantRooms { get; set; }
            public RessourceInfo(Shares inst, DateUtils.Interval interval, IEnumerable<Room> allRooms, IEnumerable<RoomAllocation> allocations)
            {
                Interval = interval;
                Ressource = allocations.Select(a => inst.AllocationToRessourceDeclaration(a)).Aggregate(new RessourceDeclaration(), (a1, a2) => a1 + a2);
                VacantRooms = allRooms.Where(r => r.IsLivingSpace).Except(Ressource.PersonsForRoom.Keys).ToList();
                if (Ressource.Persons != 0 && VacantRooms.Count != 0)
                {
                    var PersonsForVacantRooms = allocations.Where(a => a.AllocateVacantRooms).Select(a => a.PersonCount).Sum();
                    if (PersonsForVacantRooms == 0)
                    {
                        string template = "Vacant rooms in interval {0}. Assign true to the AllocateVacantRooms " +
                                          "attribute for at least one room allocation in this interval or assign " +
                                          "all available living space.";
                        throw new IntegrityException(String.Format(template, interval));
                    }
                    foreach (var room in VacantRooms)
                    {
                        Ressource.PersonsForRoom[room] = PersonsForVacantRooms;
                    }
                }
            }
        }

        private class RoomAllocationChangeEventArgs : IComparable<RoomAllocationChangeEventArgs>
        {
            public enum EventType { ALLOCATE, FREE }

            public DateTime Time { get; set; }
            public RoomAllocation Allocation { get; set; }
            public EventType Type { get; set; }

            public RoomAllocationChangeEventArgs(RoomAllocation allocation, EventType type)
            {
                Time = (type == EventType.ALLOCATE) ? allocation.Interval.From : allocation.Interval.To.AddDays(1);
                Allocation = allocation;
                Type = type;
            }

            public int CompareTo(RoomAllocationChangeEventArgs other)
            {
                return Time.CompareTo(other.Time);
            }
        }

        private IStorage _Storage;
        private RoomData _RoomData;
        private Tenants _Tenants;
        private List<RessourceInfo> _TotalResources = new List<RessourceInfo>();
        public Shares(IStorage storage, RoomData roomData, Tenants tenants)
        {
            _RoomData = roomData;
            _Tenants = tenants;
            _Storage = storage;
            OnStorageUpdated();
            storage.Register(this, roomData, tenants);
        }

        public void OnStorageUpdated(IFileSystem storage = null)
        {
            _TotalResources.Clear();
            _TotalResources.AddRange(EnumerateRessourceLists());
        }

        public IEnumerable<Share> EnumerateShares(Tenant tenant, DateUtils.Interval interval)
        {
            if (!interval.IsFinite) throw new ArgumentException("Only finite intervals allowed.");
            IEnumerator<RoomAllocation> allocIter = tenant.RoomAllocations.GetEnumerator();
            IEnumerator<RessourceInfo> ressourceIter = _TotalResources.GetEnumerator();
            if (!allocIter.MoveNext()) yield break;
            if (!ressourceIter.MoveNext()) yield break;


            if (!allocIter.SkipWhile(alloc => alloc.Interval.CompareTo(interval.From) < 0)) yield break;
            Share pendingShare = null;
            foreach (var alloc in allocIter.TakeWhile(a => a.Interval.CompareTo(interval.To) <= 0))
            {
                bool hasValue = ressourceIter.SkipWhile(r => !alloc.Interval.Contains(r.Interval));
                Debug.Assert(hasValue);
                foreach (var ressource in ressourceIter.TakeWhile(r => alloc.Interval.Contains(r.Interval)))
                {
                    var intersectedInterval = ressource.Interval.Intersect(interval);
                    if (intersectedInterval.IsEmpty) continue;
                    var share = ComputeShare(intersectedInterval, alloc, ressource);
                    var mergedShare = Share.TryToMerge(pendingShare, share);
                    if (mergedShare != null)
                    {
                        pendingShare = mergedShare;
                    }
                    else
                    {
                        if (pendingShare != null) yield return pendingShare;
                        pendingShare = share;
                    }
                }
            }
            if (pendingShare != null) yield return pendingShare;
        }

        private Share ComputeShare(DateUtils.Interval interval, RoomAllocation alloc, RessourceInfo rInfo)
        {
            IEnumerable<Room> rooms = _RoomData.GetRooms(alloc.RoomGroup).Where(r => r.IsLivingSpace);
            if (alloc.AllocateVacantRooms)
            {
                rooms = rooms.Concat(rInfo.VacantRooms);
            }
            return new Share(interval, alloc.PersonCount, rooms, rInfo.Ressource);
        }

        private RessourceDeclaration AllocationToRessourceDeclaration(RoomAllocation allocation)
        {
            var ressourceDecl = new RessourceDeclaration();
            ressourceDecl.Persons = allocation.PersonCount;
            foreach (var room in _RoomData.GetRooms(allocation.RoomGroup).Where(r => r.IsLivingSpace))
            {
                ressourceDecl.PersonsForRoom[room] = allocation.PersonCount;
            }
            return ressourceDecl;
        }

        private IEnumerable<RessourceInfo> EnumerateRessourceLists()
        {
            var events = new List<RoomAllocationChangeEventArgs>();
            foreach (var tenant in _Tenants.Entries)
            {
                foreach (var allocation in tenant.RoomAllocations)
                {
                    events.Add(new RoomAllocationChangeEventArgs(allocation, RoomAllocationChangeEventArgs.EventType.ALLOCATE));
                    if (allocation.Interval.IsFinite)
                        events.Add(new RoomAllocationChangeEventArgs(allocation, RoomAllocationChangeEventArgs.EventType.FREE));
                }
            }
            if (events.Count == 0) yield break;
            events.Sort();
            var currentAllocations = new HashSet<RoomAllocation>();
            DateTime lastDate = events[0].Time;
            foreach (var e in events)
            {
                if (e.Time != lastDate)
                {
                    Debug.Assert(e.Time > lastDate);
                    yield return new RessourceInfo(this, new DateUtils.Interval(lastDate, e.Time.AddDays(-1)), _RoomData.Rooms, currentAllocations);
                    lastDate = e.Time;
                }

                bool opResult = false;
                switch (e.Type)
                {
                    case RoomAllocationChangeEventArgs.EventType.ALLOCATE:
                        opResult = currentAllocations.Add(e.Allocation);
                        break;
                    case RoomAllocationChangeEventArgs.EventType.FREE:
                        opResult = currentAllocations.Remove(e.Allocation);
                        break;
                }
                Debug.Assert(opResult);
            }
            yield return new RessourceInfo(this, new DateUtils.Interval(lastDate), _RoomData.Rooms, currentAllocations);

        }
    }
}
