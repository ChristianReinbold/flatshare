using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class Share
    {
        public static Share TryToMerge(Share l, Share r)
        {
            if (l == null || r == null) return null;
            if (l.Rooms.Count != r.Rooms.Count || l.Rooms.Except(r.Rooms).Count() > 0) return null;
            foreach (var key in EnumUtils.AllValues(AllocationKey.PERS))
            {
                if (l.GetAbsolute(key) != r.GetAbsolute(key)) return null;
                if (l.GetTotal(key) != r.GetTotal(key)) return null;
            }
            foreach (var room in l.Rooms)
            {
                if (l._Total.PersonsForRoom[room] != r._Total.PersonsForRoom[room]) return null;
            }
            if (l.Interval.From <= r.Interval.To && l.Interval.To.AddDays(1) >= r.Interval.From)
                return new Share(new DateUtils.Interval(l.Interval.From, r.Interval.To), l._Persons, l.Rooms, l._Total);
            if (r.Interval.From <= l.Interval.To && r.Interval.To.AddDays(1) >= l.Interval.From)
                return new Share(new DateUtils.Interval(r.Interval.From, l.Interval.To), r._Persons, r.Rooms, r._Total);
            return null;
        }

        public DateUtils.Interval Interval { get; set; }
        public List<Room> Rooms { get; private set; }
        private int _Persons;

        private RessourceDeclaration _Total { get; set; }

        public Share(DateUtils.Interval interval, int persons, IEnumerable<Room> rooms, RessourceDeclaration total)
        {
            if (!interval.IsFinite) throw new ArgumentException("Only finite intervals allowed.");
            Interval = interval;
            _Persons = persons;
            Rooms = rooms.ToList();
            _Total = total;
        }

        public decimal GetAbsolute(AllocationKey key)
        {
            switch (key)
            {
                case AllocationKey.PERS:
                    return _Persons;
                case AllocationKey.SQM:
                    return Rooms.Sum(r => GetSquaremetersForRoom(r));
                default:
                    throw new ArgumentException("Unknoen allocation key " + key.ToString());
            }
        }

        public decimal GetTotal(AllocationKey key)
        {
            switch (key)
            {
                case AllocationKey.PERS:
                    return _Total.Persons;
                case AllocationKey.SQM:
                    return _Total.PersonsForRoom.Keys.Sum(r => r.SquareMeter);
                default:
                    throw new ArgumentException("Unknoen allocation key " + key.ToString());
            }
        }

        public decimal GetRelative(AllocationKey key)
        {
            return GetAbsolute(key) / GetTotal(key);
        }

        public decimal GetSquaremetersForRoom(Room room)
        {
            return _Persons / (decimal)GetTotalPersonsForRoom(room) * room.SquareMeter;
        }

        public int GetTotalPersonsForRoom(Room room)
        {
            if (!Rooms.Contains(room))
                throw new KeyNotFoundException("Room is not part of the current share");
            return _Total.PersonsForRoom[room];
        }
    }
}
