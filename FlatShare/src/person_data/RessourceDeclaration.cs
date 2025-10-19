using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class RessourceDeclaration
    {
        public Dictionary<Room, int> PersonsForRoom { get; } = new Dictionary<Room, int>();
        public int Persons { get; set; } = 0;

        public override bool Equals(object obj)
        {
            var castObj = obj as RessourceDeclaration;
            if (castObj == null)
                return false;
            bool dictEqual = castObj.PersonsForRoom.Count == PersonsForRoom.Count && !castObj.PersonsForRoom.Except(PersonsForRoom).Any();
            return castObj.Persons == Persons && dictEqual;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            foreach (var tuple in PersonsForRoom) hash ^= tuple.GetHashCode();
            hash ^= Persons.GetHashCode();
            return hash;
        }

        public static RessourceDeclaration operator +(RessourceDeclaration l, RessourceDeclaration r)
        {
            var result = new RessourceDeclaration();
            result.Persons = l.Persons + r.Persons;
            foreach (var room in l.PersonsForRoom.Keys.Union(r.PersonsForRoom.Keys))
            {
                result.PersonsForRoom[room] = l.PersonsForRoom.GetValueOrDefault(room, 0) + r.PersonsForRoom.GetValueOrDefault(room, 0);
            }
            return result;
        }
    }
}
