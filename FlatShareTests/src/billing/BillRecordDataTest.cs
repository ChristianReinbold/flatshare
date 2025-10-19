using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class BillRecordDataTest
    {
        private static Tenants GetTenants(IStorage storage)
        {
            var tenants = new Tenants(storage);
            var tenant = new Tenant("Tenant", "1", Tenant.Genders.MALE);
            tenants["Tenant1.xml"] = tenant;
            tenant = new Tenant("Tenant", "2", Tenant.Genders.MALE);
            tenants["Tenant2.xml"] = tenant;
            return tenants;
        }

        [TestMethod]
        public void AssignsBillRecordsToCorrectTenantSortedByDueDate()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var billRecordData = new BillRecordData(storage, tenants);
            var record1 = new BillRecord();
            record1.TenantFile = "Tenant1.xml";
            record1.CreationDateAsString = "10-01-2018";
            record1.DueDateAsString = "30-01-2018";
            record1.FromAsString = "12-2017";
            record1.ToAsString = "12-2017";
            billRecordData.BillRecords.Add(record1);
            var record2 = new BillRecord();
            record2.TenantFile = "Tenant2.xml";
            record2.CreationDateAsString = "13-01-2018";
            record2.DueDateAsString = "30-01-2018";
            record2.FromAsString = "12-2017";
            record2.ToAsString = "12-2017";
            billRecordData.BillRecords.Add(record2);
            var record3 = new BillRecord();
            record3.TenantFile = "Tenant1.xml";
            record3.CreationDateAsString = "15-01-2018";
            record3.DueDateAsString = "15-01-2018";
            record3.FromAsString = "12-2017";
            record3.ToAsString = "12-2017";
            billRecordData.BillRecords.Add(record3);
            billRecordData.Update();
            var records1 = billRecordData.GetBillRecordsForTenant(tenants["Tenant1.xml"]);
            var records2 = billRecordData.GetBillRecordsForTenant(tenants["Tenant2.xml"]);
            Assert.AreEqual(2, records1.Count());
            Assert.AreEqual(record3, records1.ElementAt(0));
            Assert.AreEqual(record1, records1.ElementAt(1));
            Assert.AreEqual(1, records2.Count());
            Assert.AreEqual(record2, records2.ElementAt(0));
        }

        [TestMethod]
        public void ThrowsOnBillRecordCreationsInBadOrder()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var billRecordData = new BillRecordData(storage, tenants);
            var record1 = new BillRecord();
            record1.TenantFile = "Tenant1.xml";
            record1.CreationDateAsString = "15-01-2018";
            record1.DueDateAsString = "30-01-2018";
            record1.FromAsString = "12-2017";
            record1.ToAsString = "12-2017";
            billRecordData.BillRecords.Add(record1);
            var record2 = new BillRecord();
            record2.TenantFile = "Tenant2.xml";
            record2.CreationDateAsString = "13-01-2018";
            record2.DueDateAsString = "30-01-2018";
            record2.FromAsString = "12-2017";
            record2.ToAsString = "12-2017";
            billRecordData.BillRecords.Add(record2);
            Assert.ThrowsExactly<IntegrityException>(() => billRecordData.Update());
        }

        [TestMethod]
        public void ThrowsWhenReferencedTenantFileDoesNotExist()
        {
            IStorage storage = new FileStorage(new MemoryFileSystem());
            var tenants = GetTenants(storage);
            var billRecordData = new BillRecordData(storage, tenants);
            var record1 = BillRecordTest.GetValidBill();
            record1.TenantFile = "IDoNotExist.xml";
            billRecordData.BillRecords.Add(record1);
            Assert.ThrowsExactly<IntegrityException>(() => billRecordData.Update());
        }
    }
}
