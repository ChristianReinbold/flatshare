using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class DepositTest
    {
        private static Deposit GetValidDeposit(bool finished)
        {
            return new Deposit(1000M, finished ? (decimal?)200M : null, finished ? (decimal?)50M : null);
        }

        [TestMethod]
        public void DoesNotThrowForValidDeposits()
        {
            var deposit = GetValidDeposit(false);
            deposit.CheckIntegrity();
            deposit = GetValidDeposit(true);
            deposit.CheckIntegrity();
        }

        [TestMethod]
        public void ThrowsOnNegativeDeposit()
        {
            var deposit = GetValidDeposit(false);
            deposit.Amount = -100M;
            Assert.ThrowsExactly<IntegrityException>(() => deposit.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnPartiallyCompleteDeposit()
        {
            var deposit = GetValidDeposit(false);
            deposit.Retained = 100M;
            Assert.ThrowsExactly<IntegrityException>(() => deposit.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnNegativeRetainedDeposit()
        {
            var deposit = GetValidDeposit(true);
            deposit.Retained = -100M;
            Assert.ThrowsExactly<IntegrityException>(() => deposit.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnNegativeAncillaryDeposit()
        {
            var deposit = GetValidDeposit(true);
            deposit.Ancillary = -100M;
            Assert.ThrowsExactly<IntegrityException>(() => deposit.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnAncillarySurpassingDeposit()
        {
            var deposit = GetValidDeposit(true);
            deposit.Ancillary = 1200M;
            Assert.ThrowsExactly<IntegrityException>(() => deposit.CheckIntegrity());
        }
    }
}
