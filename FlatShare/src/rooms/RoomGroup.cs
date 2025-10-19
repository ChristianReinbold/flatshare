using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class RoomGroup
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlElement("Room")]
        public List<string> Rooms { get; } = new List<string>();

        public RoomGroup() { }

        public RoomGroup(string name, params string[] roomNames)
        {
            Name = name;
            foreach (var roomName in roomNames)
            {
                Rooms.Add(roomName);
            }
        }

        public void CheckIntegrity()
        {
            if (Name.IndexOf('+') >= 0) throw new IntegrityException(String.Format("RoomGroup \"{0}\": '+' not allowed in room group names", Name));
        }
    }
}
