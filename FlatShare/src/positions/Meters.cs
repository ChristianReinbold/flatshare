namespace de.creinbold.FlatShare
{
    public class Meters : XmlDirectoryParser<Meter>
    {
        private static readonly string ROOT = "meters";

        public Meters(IStorage storage) : base(storage, ROOT) { }

        protected override void OnUpdated()
        {
            base.OnUpdated();
            Values.ElementwiseInvoke(m => m.CheckIntegrity());
        }
    }
}
