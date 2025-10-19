using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class TenantTest
    {
        private static Tenants GetTenants(string dateString)
        {
            return new Tenants(new FileStorage(new MemoryFileSystem()),
                               new DateProviderMock(DateUtils.DateFromString(dateString)));
        }

        private static void Check(Tenant tenant)
        {
            tenant.CheckIntegrity(new DateProviderMock(DateUtils.DateFromString("05-06-2019")));
        }

        [TestMethod]
        public void ThrowsWhenNoLandlordExists()
        {
            var tenants = GetTenants("05-06-2019");

            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.BankAccounts.Add(new BankAccount("Mutter Degen", "DE95674811345042622227"));
            tenants["1"] = tenant;

            Assert.ThrowsExactly<IntegrityException>(() => tenants.Update());
        }

        [TestMethod]
        public void DoesNotThrowForValidRoomAllocation()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.IsCharged = false;
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            Check(tenant);
        }

        [TestMethod]
        public void DoesNotThrowForOneAllocationAndTwoRentEntry()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.Deposit = new Deposit(0M);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.Rents.Add(new MonthlyRent(100M, 50M, 20, "10-01-2018", "01-2018"));
            tenant.Rents.Add(new MonthlyRent(150M, 50M, 3, "02-2018"));
            Check(tenant);
        }

        [TestMethod]
        public void ThrowsOnNoRoomAllocation()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnNonpositiveRoomAllocationInterval()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "16-01-2018", "15-01-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnNonpositiveRoomAllocationPersonCount()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 0, "10-01-2018", "15-01-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenRoomAllocationsAreNotSorted()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018", "15-01-2018"));
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "12-01-2018", "16-01-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenNonFinalRoomAllocationIsInfinite()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "12-01-2018", "16-01-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenNonRentAndAllocationStartMismatch()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.Rents.Add(new MonthlyRent(100M, 50M, 20, "12-02-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenNonRentAndAllocationEndMismatch()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.Rents.Add(new MonthlyRent(100M, 50M, 20, "10-01-2018", "23-03-2019"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenRentsAreNotConcurrent()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.Rents.Add(new MonthlyRent(100M, 50M, 20, "10-01-2018", "01-2019"));
            tenant.Rents.Add(new MonthlyRent(100M, 50M, 20, "03-2018"));
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

        [TestMethod]
        public void ThrowsOnIntegrityCheckWhenDepositPayoutForPermanentTenant()
        {
            var tenant = new Tenant("Hau", "Degen", Tenant.Genders.MALE);
            tenant.RoomAllocations.Add(new RoomAllocation("", 1, "10-01-2018"));
            tenant.Rents.Add(new MonthlyRent(100M, 50M, 20, "10-01-2018", "01-2018"));
            tenant.Rents.Add(new MonthlyRent(150M, 50M, 3, "02-2018"));
            tenant.Deposit = new Deposit(300M, 100M, 100M);
            Assert.ThrowsExactly<IntegrityException>(() => Check(tenant));
        }

    }
}
