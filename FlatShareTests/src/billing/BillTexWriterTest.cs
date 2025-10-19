using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class BillTexWriterTest
    {
        [TestMethod]
        public void CanCreateBillWithEmptyLists()
        {
            var storage = new FileStorage(new MemoryFileSystem());
            var tenants = new Tenants(storage);
            var tenant = new Tenant("Te", "nant1", Tenant.Genders.MALE);
            tenants["1"] = tenant;
            var landlord = new Landlord(storage);
            var transactionData = new TransactionData(storage, tenants);
            var writer = new BillTexWriter(tenants, transactionData);
            var bill = new Bill(tenant
                               , landlord
                               , Enumerable.Empty<Share>()
                               , Enumerable.Empty<IPosition>()
                               , Enumerable.Empty<Meter>()
                               , Enumerable.Empty<Room>()
                               , 0
                               , DateUtils.DateFromString("01-01-1970")
                               , DateUtils.DateFromString("01-01-1970")
                               );
            bool success;
            using (MemoryStream stream = new MemoryStream())
            {
                success = writer.Write(bill, stream, true);
            }
            Assert.IsTrue(success);
        }
    }
}
