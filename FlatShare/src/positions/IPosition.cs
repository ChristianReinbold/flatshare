using System.Collections.Generic;

namespace de.creinbold.FlatShare
{
    public enum AllocationKey { SQM, PERS }

    public interface IPosition
    {
        string Name { get; }
        AllocationKey Allocation { get; }
        bool Finished { get; }
        DateUtils.Interval Interval { get; }
        void OnPositionsUpdated(Positions positions);
        void CheckIntegrity();
        decimal GetCosts(DateUtils.Interval interval);
    }
    public interface IMeteredPosition : IPosition
    {
        ICollection<Meter> Meters { get; }
    }
}
