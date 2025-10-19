using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class BillRecordTest
    {
        public static BillRecord GetValidBill()
        {
            var bill = new BillRecord();
            bill.CreationDateAsString = "10-01-2018";
            bill.DueDateAsString = "09-02-2018";
            bill.FromAsString = "10-2017";
            bill.ToAsString = "12-2017";
            bill.TotalCosts = 50M;
            bill.RemainingCosts = 12M;
            bill.Warnings = 1;
            bill.TenantFile = "myTenant.xml";
            return bill;
        }

        [TestMethod]
        public void DoesNotThrowOnValidBillRecord()
        {
            GetValidBill().CheckIntegrity();
        }

        [TestMethod]
        public void DoesThrowOnInvalidDueDateOfBillRecord()
        {
            var bill = GetValidBill();
            bill.DueDateAsString = "09-01-2018";
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }

        [TestMethod]
        public void DoesThrowOnNegativeWarningCountOfBillRecord()
        {
            var bill = GetValidBill();
            bill.Warnings = -2;
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }

        [TestMethod]
        public void DoesThrowOnNegativeTotalCostsOfBillRecord()
        {
            var bill = GetValidBill();
            bill.TotalCosts = -10M;
            bill.RemainingCosts = -20M;
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }

        [TestMethod]
        public void DoesThrowOnInvalidRemainingCostsOfBillRecord()
        {
            var bill = GetValidBill();
            bill.TotalCosts = 10M;
            bill.RemainingCosts = 30M;
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }

        [TestMethod]
        public void DoesThrowOnInvalidBillingPeriod()
        {
            var bill = GetValidBill();
            bill.FromAsString = "01-2018";
            bill.ToAsString = "12-2017";
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }

        [TestMethod]
        public void DoesThrowOnInfiniteBillingPeriod()
        {
            var bill = GetValidBill();
            bill.ToAsString = "";
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }

        [TestMethod]
        public void DoesThrowOnCreationNotAfterBillingPeriodOfBillRecord()
        {
            var bill = GetValidBill();
            bill.CreationDateAsString = "15-12-2017";
            Assert.ThrowsExactly<IntegrityException>(() => bill.CheckIntegrity());
        }
    }
}
