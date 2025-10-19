using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class Bill
    {
        public struct Adjustment
        {
            public decimal Value;
            public DateTime BeginningAt;
            public decimal NewRent;
        }

        public class MeterData
        {
            public string Name { get; private set; }
            public string Id { get; private set; }
            public string Unit { get; private set; }

            public IMeteredPosition[] AssociatedPositions;

            public DateUtils.Interval[] Intervals { get; private set; }
            public long[] Start { get; private set; }
            public long[] End { get; private set; }
            public long[] Diff { get; private set; }
            public decimal[,] Item { get; private set; }
            public long DiffSum { get; private set; }
            public decimal[] Sum { get; private set; }

            public bool HasData { get { return Intervals.Length > 0; } }

            public MeterData(Meter meter, Share[] shares, IPosition[] positions)
            {
                Name = meter.Name;
                Id = meter.Id;
                Unit = meter.Unit;
                AssociatedPositions = positions.Select(p => p as IMeteredPosition).Where(c => c != null && c.Meters.Contains(meter)).ToArray();

                var positionsInterval = AssociatedPositions.Select(p => p.Interval).Span();
                var measuredInterval = meter.Interval.Intersect(positionsInterval);
                FillIntervals(shares, measuredInterval);
                InitArrays(Intervals.Length, AssociatedPositions.Length);
                Fill(meter);
            }

            private void FillIntervals(Share[] shares, DateUtils.Interval measuredInterval)
            {
                if (AssociatedPositions.IsEmpty())
                {
                    Intervals = new DateUtils.Interval[0];
                    return;
                }

                var intervals = new List<DateUtils.Interval>();
                foreach (var share in shares)
                {
                    var interval = share.Interval.Intersect(measuredInterval);
                    if (!interval.IsEmpty)
                        intervals.Add(interval);
                }
                Intervals = intervals.ToArray();
            }

            private void InitArrays(int intervalCount, int positionCount)
            {
                Start = new long[intervalCount];
                End = new long[intervalCount];
                Diff = new long[intervalCount];
                Item = new decimal[intervalCount, positionCount];
                Sum = new decimal[positionCount];
            }

            private void Fill(Meter meter)
            {
                DiffSum = 0;
                Sum.Initialize();
                for (var intervalIdx = 0; intervalIdx < Intervals.Length; intervalIdx++)
                {
                    var interval = Intervals[intervalIdx];
                    var start = Convert.ToInt32(meter.GetInterpolatedValue(interval.From));
                    var end = Convert.ToInt32(meter.GetInterpolatedValue(interval.To.AddDays(1)));
                    var diff = end - start;
                    Start[intervalIdx] = start;
                    End[intervalIdx] = end;
                    Diff[intervalIdx] = diff;
                    DiffSum += diff;
                    for (var i = 0; i < AssociatedPositions.Length; i++)
                    {
                        var pos = AssociatedPositions[i];
                        var costs = pos.GetCosts(interval);
                        Item[intervalIdx, i] = costs;
                        Sum[i] += costs;
                    }
                }
            }
        }

        public struct CO2Data
        {
            public decimal LivingSpaceSize;
            public DateUtils.Interval[] Intervals { get; set; }

            public decimal[] EmissionsPerInterval { get; set; }
            public decimal[] CostsTotalPerInterval { get; set; }
            public decimal[] CostsSharePerInterval { get; set; }
            public decimal CostsTotal { get; set; }
            public decimal CostsShare { get; set; }
            public decimal[] KeyPerYear { get; set; }
            public decimal[] EmissionsPerYear { get; set; }
            public decimal[] EmissionsPerYearSqm { get; set; }
        }

        public Tenant Tenant { get; private set; }
        public Landlord Landlord { get; private set; }
        public DateUtils.Interval TotalInterval { get; private set; }
        public DateTime CreationDate { get; private set; }
        public DateTime DueDate { get; private set; }

        public decimal Prepayment { get; private set; }

        public decimal[,] ItemTotal { get; private set; }
        public decimal[,] ItemShare { get; private set; }

        public decimal[] SumPerPosTotal { get; private set; }
        public decimal[] SumPerPosShare { get; private set; }

        public decimal[] SumPerIntervalTotal { get; private set; }
        public decimal[] SumPerIntervalShare { get; private set; }

        public decimal SumTotal { get; private set; }
        public decimal SumShare { get; private set; }

        public IPosition[] Positions { get; private set; }
        public Share[] Shares { get; private set; }

        public IEnumerable<MeterData> Meters { get; private set; }

        public decimal RoomSizeSum { get; private set; }
        public Room[] Rooms { get; private set; }
        public decimal?[,] SharePerRoomAndInterval { get; private set; }
        public decimal[] AbsRoomSharePerInterval { get; private set; }
        public decimal[] RelRoomSharePerInterval { get; private set; }

        public CO2Data? CO2 { get; private set; }

        public Adjustment? AdjustedPrepayment { get; set; } = null;

        public string FileName { get; set; } = null;

        public Bill(
            Tenant tenant,
            Landlord landlord,
            IEnumerable<Share> shares,
            IEnumerable<IPosition> positions,
            IEnumerable<Meter> meters,
            IEnumerable<Room> livingAreaRooms,
            decimal prepayment,
            DateTime creationDate,
            DateTime dueDate)
        {
            CreationDate = creationDate;
            DueDate = dueDate;
            Tenant = tenant;
            Landlord = landlord;
            Prepayment = prepayment;

            TotalInterval = new DateUtils.Interval(DateTime.MaxValue, DateTime.MinValue);
            foreach (var share in shares)
            {
                TotalInterval.From = DateUtils.Min(TotalInterval.From, share.Interval.From);
                TotalInterval.To = DateUtils.Max(TotalInterval.To, share.Interval.To);
            }

            var positionsAsList = positions.Where(p => !p.Interval.Intersect(TotalInterval).IsEmpty).ToList();
            positionsAsList.Sort((p1, p2) => p1.Name.CompareTo(p2.Name));

            Positions = positionsAsList.ToArray();
            Shares = shares.ToArray();
            Rooms = livingAreaRooms.ToArray();
            InitArrays(Shares.Length, Positions.Length, Rooms.Count());

            FillInCosts();

            var meterData = meters.Select(m => new MeterData(m, Shares, Positions)).Where(md => md.HasData).ToList();
            meterData.Sort((m1, m2) => m1.Name.CompareTo(m2.Name));
            Meters = meterData;

            FillInRooms();

            var co2Positions = Positions.Where(p => p is CO2Position).Select(p => p as CO2Position);
            if (co2Positions.IsEmpty()) CO2 = null;
            else FillInCO2(co2Positions.First());
        }

        private void InitArrays(int intervalCount, int positionsCount, int roomCount)
        {
            ItemTotal = new decimal[intervalCount, positionsCount];
            ItemShare = new decimal[intervalCount, positionsCount];
            SumPerPosTotal = new decimal[positionsCount];
            SumPerPosShare = new decimal[positionsCount];
            SumPerIntervalTotal = new decimal[intervalCount];
            SumPerIntervalShare = new decimal[intervalCount];

            SharePerRoomAndInterval = new decimal?[roomCount, intervalCount];
            AbsRoomSharePerInterval = new decimal[intervalCount];
            RelRoomSharePerInterval = new decimal[intervalCount];
        }

        private void FillInCosts()
        {
            SumTotal = 0;
            SumShare = 0;
            SumPerPosTotal.Initialize();
            SumPerPosShare.Initialize();
            SumPerIntervalTotal.Initialize();
            SumPerIntervalShare.Initialize();
            for (var shareIdx = 0; shareIdx < Shares.Length; shareIdx++)
            {
                var share = Shares[shareIdx];
                for (var posIdx = 0; posIdx < Positions.Length; posIdx++)
                {
                    var pos = Positions[posIdx];
                    var itemTotal = pos.GetCosts(share.Interval);
                    var itemShare = itemTotal * share.GetRelative(pos.Allocation);

                    ItemTotal[shareIdx, posIdx] = itemTotal;
                    ItemShare[shareIdx, posIdx] = itemShare;
                    SumPerPosTotal[posIdx] += itemTotal;
                    SumPerPosShare[posIdx] += itemShare;
                    SumPerIntervalTotal[shareIdx] += itemTotal;
                    SumPerIntervalShare[shareIdx] += itemShare;
                    SumTotal += itemTotal;
                    SumShare += itemShare;
                }
            }
        }

        private void FillInRooms()
        {
            RoomSizeSum = 0;
            AbsRoomSharePerInterval.Initialize();
            SharePerRoomAndInterval.Initialize();

            for (var roomIdx = 0; roomIdx < Rooms.Length; roomIdx++)
            {
                var room = Rooms[roomIdx];
                RoomSizeSum += room.SquareMeter;
                for (var shareIdx = 0; shareIdx < Shares.Length; shareIdx++)
                {
                    var share = Shares[shareIdx];
                    try
                    {
                        var sizeShare = share.GetSquaremetersForRoom(room);
                        SharePerRoomAndInterval[roomIdx, shareIdx] = sizeShare / room.SquareMeter;
                        AbsRoomSharePerInterval[shareIdx] += sizeShare;
                    }
                    catch (KeyNotFoundException) { }
                }
            }
            for (var shareIdx = 0; shareIdx < Shares.Length; shareIdx++)
            {
                RelRoomSharePerInterval[shareIdx] = AbsRoomSharePerInterval[shareIdx] / RoomSizeSum;
            }
        }

        private void FillInCO2(CO2Position pos)
        {
#pragma warning disable IDE0017 // Simplify object initialization
            var r = new CO2Data();
#pragma warning restore IDE0017 // Simplify object initialization
            r.LivingSpaceSize = pos.LivingSpaceSize;
            r.Intervals = TotalInterval.Intersect(pos.Interval).SplitByYear().ToArray();
            var nYears = r.Intervals.Length;
            r.EmissionsPerInterval = new decimal[nYears];
            r.CostsTotalPerInterval = new decimal[nYears];
            r.CostsSharePerInterval = new decimal[nYears];
            r.KeyPerYear = new decimal[nYears];
            r.EmissionsPerYear = new decimal[nYears];
            r.EmissionsPerYearSqm = new decimal[nYears];
            r.CostsTotal = 0M;
            r.CostsShare = 0M;

            for (var i = 0; i < nYears; i++)
            {
                var interval = r.Intervals[i];
                r.EmissionsPerInterval[i] = pos.GetEmission(interval);
                var emissionInYear = pos.GetEmission(interval.From.Year);
                r.EmissionsPerYear[i] = emissionInYear;
                r.EmissionsPerYearSqm[i] = emissionInYear / pos.LivingSpaceSize;
                var factor = pos.GetAllocationFactor(interval.From.Year);
                r.KeyPerYear[i] = factor;
                var costs = pos.GetCostsWithoutAllocationFactor(interval);
                r.CostsTotalPerInterval[i] = costs;
                r.CostsSharePerInterval[i] = factor * costs;
                r.CostsTotal += costs;
                r.CostsShare += factor * costs;
            }
            CO2 = r;
        }

        public override string ToString()
        {
            return String.Format("Ancillary bill of {0} from {1} to {2} over {3:N2}{5} (prepaid {4:N2}{5})",
                Tenant.Name, TotalInterval.FromAsString, TotalInterval.ToAsString, SumShare, Prepayment, Currency.CURRENT.Symbol);
        }
    }
}
