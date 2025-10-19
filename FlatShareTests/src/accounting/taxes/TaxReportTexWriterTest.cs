using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class TaxReportTexWriterTest
    {
        [TestMethod]
        public void CanCreateTaxReportWithEmptyLists()
        {
            var storage = new FileStorage(new MemoryFileSystem());
            var meters = new Meters(storage);
            var roomData = new RoomData(storage);
            var positions = new Positions(storage, meters, roomData);
            var serviceProviders = new ServiceProviders(storage, positions);
            var tenants = new Tenants(storage);
            var transactionData = new TransactionData(storage, tenants, serviceProviders);
            var billRecordData = new BillRecordData(storage, tenants);
            var settlementManager = new SettlementManager(storage, tenants, transactionData, billRecordData);
            var report = new TaxReport(tenants
                                      , serviceProviders
                                      , settlementManager
                                      , transactionData
                                      , 0
                                      );
            TaxReportTexWriter writer = new TaxReportTexWriter();

            bool success;
            using (MemoryStream stream = new MemoryStream())
            {
                success = writer.Write(report, stream, true);
            }
            Assert.IsTrue(success);
        }
    }
}
