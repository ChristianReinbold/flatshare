using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class TransactionDataTest
    {
        private static Tenants GetTenants(IStorage storage)
        {
            var tenants = new Tenants(storage);
            var tenant = new Tenant("Te", "nant1", Tenant.Genders.MALE);
            tenant.BankAccounts.Add(new BankAccount("Tenant1", "DE95674811345042622227"));
            tenant.BankAccounts.Add(new BankAccount("Tenant1", "CZ5338988394782900752225"));
            tenants["1"] = tenant;
            tenant = new Tenant("Te", "nant2", Tenant.Genders.MALE);
            tenant.BankAccounts.Add(new BankAccount("Tenant2", "NL40BBIQ7891403010"));
            tenants["2"] = tenant;
            return tenants;
        }

        private static ServiceProviders GetServiceProviders(IStorage storage)
        {
            var meters = new Meters(storage);
            var roomData = new RoomData(storage);
            var positions = new Positions(storage, meters, roomData);
            var providers = new ServiceProviders(storage, positions);
            var provider = new ServiceProvider("Provider1");
            provider.BankAccounts.Add(new BankAccount("Provider AG", "DE15458428351457957736"));
            providers.Add("1", provider);
            return providers;
        }

        [TestMethod]
        public void AssignsTransactionsToCorrectOwner()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var providers = GetServiceProviders(storage);
            var transactionData = new TransactionData(storage, tenants, providers);
            var t1 = new Transaction("CZ5338988394782900752225", "02-01-2018", 14.4m);
            var t2 = new Transaction("DE95674811345042622227", "02-01-2018", 12.0m);
            var t3 = new Transaction("CZ5338988394782900752225", "02-02-2018", 10.0m);
            var t4 = new Transaction("NL40BBIQ7891403010", "05-02-2018", 13.0m);
            var t5 = new Transaction("DE15458428351457957736", "08-02-2018", 13.0m);
            transactionData.Transactions.Add(t1);
            transactionData.Transactions.Add(t2);
            transactionData.Transactions.Add(t3);
            transactionData.Transactions.Add(t4);
            transactionData.Transactions.Add(t5);
            transactionData.Update();
            var transactions1 = transactionData.GetTransactionsForOwner(tenants["1"]);
            var transactions2 = transactionData.GetTransactionsForOwner(tenants["2"]);
            var transactions3 = transactionData.GetTransactionsForOwner(providers["1"]);
            Assert.AreEqual(3, transactions1.Count());
            Assert.AreEqual(t1, transactions1.ElementAt(0));
            Assert.AreEqual(t2, transactions1.ElementAt(1));
            Assert.AreEqual(t3, transactions1.ElementAt(2));
            Assert.AreEqual(1, transactions2.Count());
            Assert.AreEqual(t4, transactions2.ElementAt(0));
            Assert.AreEqual(1, transactions3.Count());
            Assert.AreEqual(t5, transactions3.ElementAt(0));
        }

        [TestMethod]
        public void ThrowsOnTransactionsInBadOrder()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var transactionData = new TransactionData(storage, tenants);
            var t1 = new Transaction("CZ5338988394782900752225", "02-01-2018", 14.4m);
            var t2 = new Transaction("DE95674811345042622227", "01-02-2019", 12.0m);
            var t3 = new Transaction("CZ5338988394782900752225", "02-02-2018", 10.0m);
            var t4 = new Transaction("NL40BBIQ7891403010", "05-02-2018", 13.0m);
            transactionData.Transactions.Add(t1);
            transactionData.Transactions.Add(t2);
            transactionData.Transactions.Add(t3);
            transactionData.Transactions.Add(t4);
            Assert.ThrowsExactly<IntegrityException>(() => transactionData.Update());
        }

        [TestMethod]
        public void ThrowsWhenIBANIsNotUnique()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var providers = GetServiceProviders(storage);
            tenants["2"].BankAccounts.Add(new BankAccount("Tenant2", "DE15458428351457957736"));
            Assert.ThrowsExactly<IntegrityException>(() => new TransactionData(storage, tenants, providers));
        }

        [TestMethod]
        public void ThrowsWhenIBANIsNotRegisteredForAnyOwner()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var providers = GetServiceProviders(storage);
            var transactionData = new TransactionData(storage, tenants, providers);
            transactionData.Transactions.Add(new Transaction("CH696164672S8600K5V0Q", "05-08-2019", 5.34m));
            Assert.ThrowsExactly<IntegrityException>(() => transactionData.Update());
        }
    }
}
