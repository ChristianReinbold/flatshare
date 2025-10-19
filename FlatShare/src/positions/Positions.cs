using System;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class Positions : XmlDirectoryParser<IPosition>
    {
        private static readonly string ROOT = "positions";

        public Meters Meters { get; private set; }
        public RoomData RoomData { get; private set; }

        public Positions(IStorage storage, Meters meters, RoomData roomData) : base(storage, ROOT, meters, roomData)
        {
            Meters = meters;
            RoomData = roomData;
        }

        protected override void OnUpdated()
        {
            base.OnUpdated();
            Values.ElementwiseInvoke(e => e.OnPositionsUpdated(this));
            Values.ElementwiseInvoke(e => e.CheckIntegrity());
            var co2PositionFiles = this.Where(item => item.Value is CO2Position).Select(item => item.Key);
            if (co2PositionFiles.Count() > 1)
            {
                string template = "Only one CO2 position is supported, but found multiple such positions ({0}).";
                throw new IntegrityException(String.Format(template, String.Join(", ", co2PositionFiles)));
            }
        }

        public override string ToString()
        {
            return "[" + String.Join(", ", Values.Select(entry => entry.ToString())) + "]";
        }
    }
}
