using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class Room
    {
        [XmlText]
        public string Name { get; set; }

        [XmlAttribute("sqm")]
        public decimal SquareMeter { get; set; }

        [XmlAttribute("LivingSpace")]
        [DefaultValueAttribute(true)]
        public bool IsLivingSpace { get; set; }

        public Room()
        {
            Name = "MyRoom";
            SquareMeter = 0.01m;
            IsLivingSpace = true;
        }

        public Room(string name, decimal squareMeter, bool livingSpace = true)
        {
            Name = name;
            SquareMeter = squareMeter;
            IsLivingSpace = livingSpace;
        }

        public void CheckIntegrity()
        {
            if (Name.IndexOf('+') >= 0) throw new IntegrityException(String.Format("Room \"{0}\": '+' not allowed in room names", Name));
            if (SquareMeter <= 0m) throw new IntegrityException(String.Format("Room \"{0}\": Non-positive square meters", Name));
        }
    }
}
