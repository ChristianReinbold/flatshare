using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class ServiceProviderTest
    {
        private static ServiceProvider GetInstance()
        {
            var provider = new ServiceProvider("1");
            provider.BankAccounts.Add(new BankAccount("Acc1", "NL40BBIQ7891403010"));
            provider.BankAccounts.Add(new BankAccount("Acc2", "DE20667878142334609181"));
            provider.Services.Add(new Service("1", "2018", "2019"));
            provider.Services.Add(new Service("2", "2018"));
            return provider;
        }

        [TestMethod]
        public void IntegrityCheckDoesNotThrowForValidServiceProvider()
        {
            GetInstance().CheckIntegrity();
        }

        [TestMethod]
        public void ThrowsIfServiceProviderProvidesNoService()
        {
            var provider = GetInstance();
            provider.Services.Clear();
            Assert.ThrowsExactly<IntegrityException>(() => provider.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsIfServiceProviderHasInvalidIBAN()
        {
            var provider = GetInstance();
            provider.BankAccounts.Add(new BankAccount("MyOwner", "XXX239"));
            Assert.ThrowsExactly<IntegrityException>(() => provider.CheckIntegrity());
        }
    }
}
