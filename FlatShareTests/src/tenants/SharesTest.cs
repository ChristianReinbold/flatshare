using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class SharesTest
    {
        private Tuple<RoomData, Tenants, Shares> GetScenario()
        {
            var storage = new FileStorage(new MemoryFileSystem());

            RoomData roomData = new RoomData(storage);
            var rooms = roomData.Rooms;
            var roomGroups = roomData.RoomGroups;

            rooms.Clear();
            rooms.Add(new Room("Common", 30m));
            rooms.Add(new Room("Common_Storage", 2.3m, false));
            rooms.Add(new Room("Room1", 13m));
            rooms.Add(new Room("Room2", 11m));
            rooms.Add(new Room("VacantRoom", 10m));

            roomGroups.Clear();
            roomGroups.Add(new RoomGroup("RG1", "Common", "Common_Storage", "Room1"));
            roomGroups.Add(new RoomGroup("RG2", "Common", "Common_Storage", "Room2"));

            Tenants tenants = new Tenants(storage);
            var tenant = new Tenant("Tenant", "1", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("RG1", 1, "01-2018", "01-2018"));
            tenant.RoomAllocations.Add(new RoomAllocation("RG1", 2, "03-2018"));
            tenants.Add("1", tenant);

            tenant = new Tenant("Tenant", "2", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("RG2", 1, "01-2018", "03-2018"));
            tenants.Add("2", tenant);

            tenant = new Tenant("Tenant", "3", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("RG2", 1, "04-2018", "04-2018"));
            tenants.Add("3", tenant);

            tenant = new Tenant("Vacant", "Tenant", Tenant.Genders.FEMALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "01-2018", "", true));
            tenants.Add("vacant", tenant);

            Shares shares = new Shares(storage, roomData, tenants);
            return new Tuple<RoomData, Tenants, Shares>(roomData, tenants, shares);
        }

        [TestMethod]
        public void ThrowsOnRoomNotAllocatedForTenant()
        {
            var scenario = GetScenario();
            var shares = scenario.Item3.EnumerateShares(scenario.Item2["1"], new DateUtils.Interval("2018", "05-2018")).ToList();
            var room2 = scenario.Item1.GetRooms("Room2").First();
            Assert.ThrowsExactly<KeyNotFoundException>(() => shares[0].GetSquaremetersForRoom(room2));
        }

        [TestMethod]
        public void ComputesCorrectSharesForTenant1()
        {
            var scenario = GetScenario();
            var shares = scenario.Item3.EnumerateShares(scenario.Item2["1"], new DateUtils.Interval("2018", "05-2018")).ToList();
            var commonRoom = scenario.Item1.GetRooms("Common").First();
            var room1 = scenario.Item1.GetRooms("Room1").First();
            Assert.AreEqual(shares.Count, 3);

            var share = shares[0];
            Assert.AreEqual(share.Interval.FromAsString, "2018");
            Assert.AreEqual(share.Interval.ToAsString, "01-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 3);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 3m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 28m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 28m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 15m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room1), 13m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 2);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room1), 1);

            share = shares[1];
            Assert.AreEqual(share.Interval.FromAsString, "03-2018");
            Assert.AreEqual(share.Interval.ToAsString, "04-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 2);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 2m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 33m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 33m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 20m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room1), 13m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 3);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room1), 2);

            share = shares[2];
            Assert.AreEqual(share.Interval.FromAsString, "05-2018");
            Assert.AreEqual(share.Interval.ToAsString, "05-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 2);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 3);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 2m / 3m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 43m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 43m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 30m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room1), 13m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 2);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room1), 2);
        }

        [TestMethod]
        public void ComputesNoSharesForNonIntersectingInterval()
        {
            var scenario = GetScenario();
            var interval = new DateUtils.Interval("25-03-2017", "05-04-2017");

            var sharesTenant1 = scenario.Item3.EnumerateShares(scenario.Item2["1"], interval).ToList();
            Assert.AreEqual(sharesTenant1.Count, 0);
        }

        [TestMethod]
        public void ComputesCorrectSharesForTenantsInSmallInterval()
        {
            var scenario = GetScenario();
            var interval = new DateUtils.Interval("25-03-2018", "05-04-2018");

            var commonRoom = scenario.Item1.GetRooms("Common").First();
            var room1 = scenario.Item1.GetRooms("Room1").First();
            var room2 = scenario.Item1.GetRooms("Room2").First();

            var sharesTenant1 = scenario.Item3.EnumerateShares(scenario.Item2["1"], interval).ToList();
            var sharesTenant2 = scenario.Item3.EnumerateShares(scenario.Item2["2"], interval).ToList();
            var sharesTenant3 = scenario.Item3.EnumerateShares(scenario.Item2["3"], interval).ToList();

            Assert.AreEqual(sharesTenant1.Count, 1);
            Assert.AreEqual(sharesTenant2.Count, 1);
            Assert.AreEqual(sharesTenant3.Count, 1);

            var share = sharesTenant1[0];
            Assert.AreEqual(share.Interval.FromAsString, "25-03-2018");
            Assert.AreEqual(share.Interval.ToAsString, "05-04-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 2);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 2m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 33m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 33m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 20m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room1), 13m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 3);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room1), 2);

            share = sharesTenant2[0];
            Assert.AreEqual(share.Interval.FromAsString, "25-03-2018");
            Assert.AreEqual(share.Interval.ToAsString, "03-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 21m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 21m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 10m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room2), 11m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 3);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);

            share = sharesTenant3[0];
            Assert.AreEqual(share.Interval.FromAsString, "04-2018");
            Assert.AreEqual(share.Interval.ToAsString, "05-04-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 21m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 21m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 10m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room2), 11m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 3);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);
        }


        [TestMethod]
        public void ComputesCorrectSharesForTenant2()
        {
            var scenario = GetScenario();
            var shares = scenario.Item3.EnumerateShares(scenario.Item2["2"], new DateUtils.Interval("2018", "05-2018")).ToList();
            var commonRoom = scenario.Item1.GetRooms("Common").First();
            var room2 = scenario.Item1.GetRooms("Room2").First();
            Assert.AreEqual(shares.Count, 3);

            var share = shares[0];
            Assert.AreEqual(share.Interval.FromAsString, "2018");
            Assert.AreEqual(share.Interval.ToAsString, "01-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 3);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 3m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 26m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 26m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 15m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room2), 11m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 2);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);

            share = shares[1];
            Assert.AreEqual(share.Interval.FromAsString, "02-2018");
            Assert.AreEqual(share.Interval.ToAsString, "02-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 2);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 2m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 41m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 41m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 30m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room2), 11m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 1);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);

            share = shares[2];
            Assert.AreEqual(share.Interval.FromAsString, "03-2018");
            Assert.AreEqual(share.Interval.ToAsString, "03-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 21m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 21m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 10m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room2), 11m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 3);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);
        }

        [TestMethod]
        public void ComputesCorrectSharesForTenant3()
        {
            var scenario = GetScenario();
            var shares = scenario.Item3.EnumerateShares(scenario.Item2["3"], new DateUtils.Interval("2018", "05-2018")).ToList();
            var commonRoom = scenario.Item1.GetRooms("Common").First();
            var room2 = scenario.Item1.GetRooms("Room2").First();
            Assert.AreEqual(shares.Count, 1);

            var share = shares[0];
            Assert.AreEqual(share.Interval.FromAsString, "04-2018");
            Assert.AreEqual(share.Interval.ToAsString, "04-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 21m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 21m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(commonRoom), 10m);
            Utils.AssertNumericAlmostEqual(share.GetSquaremetersForRoom(room2), 11m);
            Assert.AreEqual(share.GetTotalPersonsForRoom(commonRoom), 3);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);
        }

        [TestMethod]
        public void ComputesCorrectSharesForVacantTenant()
        {
            var scenario = GetScenario();
            var shares = scenario.Item3.EnumerateShares(scenario.Item2["vacant"], new DateUtils.Interval("2018", "05-2018")).ToList();
            var vacantRoom = scenario.Item1.GetRooms("VacantRoom").First();
            var room1 = scenario.Item1.GetRooms("Room1").First();
            var room2 = scenario.Item1.GetRooms("Room2").First();
            Assert.AreEqual(shares.Count, 4);

            var share = shares[0];
            Assert.AreEqual(share.Interval.FromAsString, "2018");
            Assert.AreEqual(share.Interval.ToAsString, "01-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 3);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 3m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 10m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 10m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 1);
            Assert.AreEqual(share.GetTotalPersonsForRoom(vacantRoom), 1);

            share = shares[1];
            Assert.AreEqual(share.Interval.FromAsString, "02-2018");
            Assert.AreEqual(share.Interval.ToAsString, "02-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 2);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 2m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 23m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 23m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room1), 1);
            Assert.AreEqual(share.GetTotalPersonsForRoom(vacantRoom), 1);

            share = shares[2];
            Assert.AreEqual(share.Interval.FromAsString, "03-2018");
            Assert.AreEqual(share.Interval.ToAsString, "04-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 4);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 4m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 10m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 10m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 1);
            Assert.AreEqual(share.GetTotalPersonsForRoom(vacantRoom), 1);

            share = shares[3];
            Assert.AreEqual(share.Interval.FromAsString, "05-2018");
            Assert.AreEqual(share.Interval.ToAsString, "05-2018");
            Assert.AreEqual(share.GetAbsolute(AllocationKey.PERS), 1);
            Assert.AreEqual(share.GetTotal(AllocationKey.PERS), 3);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.PERS), 1m / 3m);
            Utils.AssertNumericAlmostEqual(share.GetAbsolute(AllocationKey.SQM), 21m);
            Utils.AssertNumericAlmostEqual(share.GetTotal(AllocationKey.SQM), 64m);
            Utils.AssertNumericAlmostEqual(share.GetRelative(AllocationKey.SQM), 21m / 64m);
            Assert.AreEqual(share.Rooms.Count(), 2);
            Assert.AreEqual(share.GetTotalPersonsForRoom(room2), 1);
            Assert.AreEqual(share.GetTotalPersonsForRoom(vacantRoom), 1);
        }
    }
}
