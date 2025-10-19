using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class RoomDataTest
    {
        private RoomData GetInstance()
        {
            var data = new RoomData();
            var roomList = data.Rooms;
            var groupList = data.RoomGroups;
            roomList.Add(new Room("Kitchen", 10.5m));
            roomList.Add(new Room("Living", 30m));
            roomList.Add(new Room("Entrance", 5.3m));
            var group = new RoomGroup("NoFloor");
            group.Rooms.Add("Kitchen");
            group.Rooms.Add("Living");
            groupList.Add(group);
            return data;
        }

        [TestMethod]
        public void ReturnsRoomsForRoomGroup()
        {
            var data = GetInstance();
            var roomNames = data.GetRooms("NoFloor").Select(r => r.Name);
            Assert.AreEqual(roomNames.Count(), 2);
            Assert.IsTrue(roomNames.Contains("Kitchen"));
            Assert.IsTrue(roomNames.Contains("Living"));
        }

        [TestMethod]
        public void ReturnsRoomsForConcatenatedRoomAndRoomGroup()
        {
            var data = GetInstance();
            var roomNames = data.GetRooms("NoFloor+Entrance").Select(r => r.Name);
            Assert.AreEqual(roomNames.Count(), 3);
            Assert.IsTrue(roomNames.Contains("Kitchen"));
            Assert.IsTrue(roomNames.Contains("Living"));
            Assert.IsTrue(roomNames.Contains("Entrance"));
        }

        [TestMethod]
        public void ThrowsOnCyclicRoomGroupReference()
        {
            var data = new RoomData();
            var groupList = data.RoomGroups;
            var group = new RoomGroup("RG1");
            group.Rooms.Add("RG2");
            groupList.Add(group);
            group = new RoomGroup("RG2");
            group.Rooms.Add("RG3");
            groupList.Add(group);
            group = new RoomGroup("RG3");
            group.Rooms.Add("RG1");
            groupList.Add(group);
            Assert.ThrowsExactly<IntegrityException>(() => data.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnNonnegativeRoomSize()
        {
            var data = new RoomData();
            var roomList = data.Rooms;
            roomList.Add(new Room("Kitchen", -3m));
            Assert.ThrowsExactly<IntegrityException>(() => data.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnDuplicateNames()
        {
            var data = new RoomData();
            var roomList = data.Rooms;
            var groupList = data.RoomGroups;
            roomList.Add(new Room("Kitchen", 10.5m));
            roomList.Add(new Room("Entrance", 5.3m));
            var group = new RoomGroup("Kitchen");
            groupList.Add(group);
            Assert.ThrowsExactly<IntegrityException>(() => data.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnRoomGroupReferenceToNonexistingRoom()
        {
            var data = new RoomData();
            var groupList = data.RoomGroups;
            var group = new RoomGroup("RoomGroup");
            group.Rooms.Add("NonexistingRoom");
            groupList.Add(group);
            Assert.ThrowsExactly<IntegrityException>(() => data.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnPlusInRoomName()
        {
            var data = new RoomData();
            var roomList = data.Rooms;
            roomList.Add(new Room("Kitchen+Entrance", 10.5m));
            Assert.ThrowsExactly<IntegrityException>(() => data.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnPlusInRoomGroupName()
        {
            var data = new RoomData();
            var roomList = data.Rooms;
            roomList.Add(new Room("Kitchen", 10.5m));
            var groupList = data.RoomGroups;
            var group = new RoomGroup("RoomGroup+OtherGroup");
            group.Rooms.Add("Kitchen");
            groupList.Add(group);
            Assert.ThrowsExactly<IntegrityException>(() => data.CheckIntegrity());
        }
    }
}
