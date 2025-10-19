using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class ServiceProviders : XmlDirectoryParser<ServiceProvider>
    {
        private static readonly string ROOT = "providers";

        private Positions _Positions;

        public ServiceProviders(IStorage storage, Positions positions) : base(storage, ROOT, positions)
        {
            _Positions = positions;
        }

        protected override void OnUpdated()
        {
            base.OnUpdated();
            ResolvePositionRefs();
            CheckIntegrity();
        }

        private void ResolvePositionRefs()
        {
            foreach (var provider in Values)
            {
                foreach (var service in provider.Services)
                {
                    if (!_Positions.ContainsKey(service.PositionRef))
                    {
                        string template = "Service Provider \"{0}\": Position file \"{1}\" not found.";
                        throw new IntegrityException(String.Format(template, provider.Name, service.PositionRef));
                    }
                    service.AssociatedPosition = _Positions[service.PositionRef];
                }
            }
        }

        public ServiceProvider GetMostLikelyServiceProviderFromString(string s)
        {
            int distance;
            return StringMatching.GetMostLikelyMatch(StringMatching.DamerauLevenshteinToSubstring, t => t.Name.ToLower(), s.ToLower(), Values, out distance);
        }

        private void WarnMissingServiceProviders(Dictionary<IPosition, List<DateUtils.Interval>> servedIntervals)
        {
            foreach (var position in _Positions.Values)
            {

                if (!servedIntervals.ContainsKey(position))
                {
                    Console.WriteLine(String.Format("Warning: No service provider for \"{0}\".", position.Name));
                }
                else
                {
                    var unservedIntervals = position.Interval.Except(servedIntervals[position]).ToList();
                    if (unservedIntervals.IsEmpty()) continue;
                    var unservedIntervalsString = String.Join(", ", unservedIntervals.Select(i => i.ToString()));
                    Console.WriteLine(String.Format("Warning: No service provider for \"{0}\" in intervals {1}.", position.Name, unservedIntervalsString));
                }
            }
        }

        public void CheckIntegrity()
        {
            var servedIntervals = new Dictionary<IPosition, List<DateUtils.Interval>>();
            Values.ElementwiseInvoke(p => p.CheckIntegrity());

            foreach (var provider in Values)
            {
                foreach (var service in provider.Services)
                {
                    var intervals = servedIntervals.DefaultCreate(service.AssociatedPosition, () => new List<DateUtils.Interval>());
                    foreach (var otherInterval in intervals)
                    {
                        var intersection = otherInterval.Intersect(service.Interval);
                        if (!intersection.IsEmpty)
                        {
                            var template = "Service Provider \"{0}\": \"{1}\" is provided several times in interval {2}.";
                            throw new IntegrityException(String.Format(template, provider.Name, service.AssociatedPosition.Name, intersection));
                        }
                    }
                    intervals.Add(service.Interval);
                }
            }
            WarnMissingServiceProviders(servedIntervals);
        }
    }
}
