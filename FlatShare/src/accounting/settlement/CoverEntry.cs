using System;

namespace de.creinbold.FlatShare
{
    public class CoverEntry
    {
        public object Source { get; private set; }
        public decimal Amount { get; private set; }

        public CoverEntry(decimal amount, object source)
        {
            Amount = amount;
            Source = source;
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool invertedAmount)
        {
            var factor = invertedAmount ? -1 : 1;
            return String.Format("{0} by {1}", factor * Amount, Source);
        }
    }
}
