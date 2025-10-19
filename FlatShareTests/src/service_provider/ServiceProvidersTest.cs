using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class ServiceProvidersTest
    {
        private static Tuple<Positions, ServiceProviders> GetInstance()
        {
            var storage = new FileStorage(new MemoryFileSystem());
            var meters = new Meters(storage);
            var roomData = new RoomData(storage);
            var positions = new Positions(storage, meters, roomData);

            var pos = new Position("Pos1", AllocationKey.PERS);
            pos.Expenses.Add(new RepeatedExpense(10.0M, Frequency.MONTHLY, "2018"));
            positions.Add("1", pos);
            pos = new Position("Pos2", AllocationKey.PERS);
            pos.Expenses.Add(new RepeatedExpense(12.0M, Frequency.MONTHLY, "2018"));
            positions.Add("2", pos);
            pos = new Position("Pos3", AllocationKey.PERS);
            pos.Expenses.Add(new RepeatedExpense(13.0M, Frequency.MONTHLY, "2018"));
            positions.Add("3", pos);

            var providers = new ServiceProviders(storage, positions);

            var provider = new ServiceProvider("1");
            provider.BankAccounts.Add(new BankAccount("Acc1", "NL40BBIQ7891403010"));
            provider.BankAccounts.Add(new BankAccount("Acc2", "DE20667878142334609181"));
            provider.Services.Add(new Service("1", "2018", "2019"));
            provider.Services.Add(new Service("2", "2018"));
            providers.Add("1", provider);


            provider = new ServiceProvider("2");
            provider.BankAccounts.Add(new BankAccount("Acc3", "DE15458428351457957736"));
            provider.Services.Add(new Service("1", "2020"));
            provider.Services.Add(new Service("3", "2018"));
            providers.Add("2", provider);

            positions.Update();
            providers.Update();
            return Tuple.Create(positions, providers);
        }

        [TestMethod]
        public void PositionReferencesAreCorretlyResolved()
        {
            var inst = GetInstance();
            var positions = inst.Item1;
            var providers = inst.Item2;

            Assert.AreSame(positions["1"], providers["1"].Services[0].AssociatedPosition);
            Assert.AreSame(positions["2"], providers["1"].Services[1].AssociatedPosition);
            Assert.AreSame(positions["1"], providers["2"].Services[0].AssociatedPosition);
            Assert.AreSame(positions["3"], providers["2"].Services[1].AssociatedPosition);
        }

        [TestMethod]
        public void ThrowsOnInvalidPositionReference()
        {
            var inst = GetInstance();
            var providers = inst.Item2;

            providers["1"].Services.Add(new Service("invalid", "2018"));
            Assert.ThrowsExactly<IntegrityException>(() => providers.Update());
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenCostIsServedTwice()
        {
            var inst = GetInstance();
            var providers = inst.Item2;

            providers["1"].Services.Add(new Service("3", "2018"));
            Assert.ThrowsExactly<IntegrityException>(() => providers.Update());
        }


    }
}
