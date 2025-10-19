using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public sealed class RoomData : XmlFileParser
    {

        private Dictionary<string, Room> _HashedRooms = new Dictionary<string, Room>();
        private Dictionary<string, RoomGroup> _HashedRoomGroups = new Dictionary<string, RoomGroup>();

        [XmlElement("Room")]
        public Collection<Room> Rooms { get; } = new ObservableCollection<Room>();

        [XmlElement("RoomGroup")]
        public Collection<RoomGroup> RoomGroups { get; } = new ObservableCollection<RoomGroup>();

        private void RoomsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) { _HashedRooms.Clear(); }
            if (e.OldItems != null)
                foreach (var removed in e.OldItems.Cast<Room>()) { _HashedRooms.Remove(removed.Name); }
            if (e.NewItems != null)
                foreach (var inserted in e.NewItems.Cast<Room>()) { _HashedRooms.Add(inserted.Name, inserted); }
        }

        private void RoomGroupsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) { _HashedRoomGroups.Clear(); }
            if (e.OldItems != null)
                foreach (var removed in e.OldItems.Cast<RoomGroup>()) { _HashedRoomGroups.Remove(removed.Name); }
            if (e.NewItems != null)
                foreach (var inserted in e.NewItems.Cast<RoomGroup>()) { _HashedRoomGroups.Add(inserted.Name, inserted); }
        }

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public RoomData() : base()
        {
            ((ObservableCollection<Room>)Rooms).CollectionChanged += RoomsChanged;
            ((ObservableCollection<RoomGroup>)RoomGroups).CollectionChanged += RoomGroupsChanged;
        }

        public RoomData(IStorage storage) : base(storage, "room_data.xml")
        {
            ((ObservableCollection<Room>)Rooms).CollectionChanged += RoomsChanged;
            ((ObservableCollection<RoomGroup>)RoomGroups).CollectionChanged += RoomGroupsChanged;
        }

        protected override void CopyFrom(object deserialized)
        {
            Rooms.Clear();
            RoomGroups.Clear();
            var obj = deserialized as RoomData;
            if (obj != null)
            {
                foreach (var room in obj.Rooms) Rooms.Add(room);
                foreach (var roomGroup in obj.RoomGroups) RoomGroups.Add(roomGroup);
            }
        }

        protected override void OnUpdated()
        {
            CheckIntegrity();
        }

        public HashSet<Room> GetRooms(string name, HashSet<Room> outSet = null)
        {
            if (outSet == null) { outSet = new HashSet<Room>(); }
            if (String.IsNullOrEmpty(name)) return outSet;
            foreach (var namePart in name.Split('+'))
            {
                if (_HashedRooms.ContainsKey(namePart))
                {
                    outSet.Add(_HashedRooms[namePart]);
                }
                else if (_HashedRoomGroups.ContainsKey(namePart))
                {
                    foreach (var subName in _HashedRoomGroups[namePart].Rooms)
                    {
                        GetRooms(subName, outSet);
                    }
                }
                else
                {
                    throw new ArgumentException(String.Format("Unknown room id: {0}", namePart));
                }
            }
            return outSet;
        }

        public void CheckIntegrity()
        {
            foreach (var room in Rooms) room.CheckIntegrity();
            foreach (var roomGroup in RoomGroups) roomGroup.CheckIntegrity();

            var duplicates = _HashedRooms.Keys.Intersect(_HashedRoomGroups.Keys).ToList();
            if (duplicates.Count > 0)
                throw new IntegrityException(String.Format("Duplicate room(group) ids: {0}", String.Join(", ", duplicates)));
            foreach (var roomGroup in RoomGroups)
            {
                foreach (var subName in roomGroup.Rooms)
                {
                    if (!_HashedRooms.ContainsKey(subName) && !_HashedRoomGroups.ContainsKey(subName))
                    {
                        var template = "RoomGroup \"{0}\" contains unknown room: {1}";
                        throw new IntegrityException(String.Format(template, roomGroup.Name, subName));
                    }
                }
            }
            Stack<string> path = new Stack<string>();
            foreach (var roomGroup in RoomGroups)
            {
                ThrowOnCyclicReference(roomGroup.Name, path);
            }
        }

        private void ThrowOnCyclicReference(string name, Stack<string> path)
        {
            if (!_HashedRoomGroups.ContainsKey(name))
                return;
            path.Push(name);
            foreach (var subName in _HashedRoomGroups[name].Rooms)
            {
                if (path.Contains(subName))
                {
                    var template = "RoomGroup \"{0}\" has cyclic reference via: {1}";
                    throw new IntegrityException(String.Format(template, name, String.Join(", ", path)));
                }
                ThrowOnCyclicReference(subName, path);
            }
            path.Pop();
        }
    }
}
