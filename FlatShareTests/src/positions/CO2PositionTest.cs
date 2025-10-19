using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Zio.FileSystems;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class CO2PositionTest
    {
        private Positions GetPositions()
        {
            var storageMock = new FileStorage(new MemoryFileSystem());
            var meters = new Meters(storageMock);
            var roomData = new RoomData(storageMock);

            // Total of 100sqm of living space
            roomData.Rooms.Add(new Room("Room1", 10M, true));
            roomData.Rooms.Add(new Room("Room2", 40M, false));
            roomData.Rooms.Add(new Room("Room3", 90M, true));

            // Some fake meter with two measurements and start and end of time. Meters are tested elsewhere.
            var meter = new Meter("1", "", "");
            meter.Values.Add(new MeterValue("01-01-2000", 0));
            meter.Values.Add(new MeterValue("01-01-2050", 10));
            meters["1"] = meter;

            return new Positions(storageMock, meters, roomData);
        }

        private CO2Position PopulatePositions(Positions positions)
        {
            var pos = new CO2Position("", AllocationKey.PERS);
            pos.MeterFiles.Add("1");
            pos.Expenses.Add(new CO2Expense(10, 500, "01-2018", "03-2018"));
            pos.Expenses.Add(new CO2Expense(30, 2000, "04-2018", "12-2018"));
            pos.Expenses.Add(new CO2Expense(10, 1000, "2019", "2019"));

            // Scramble order to test resilience to scrambled user inputs
            var table2018 = new CO2AllocationTable(2018);
            table2018.Rows.Add(new CO2Allocation(0, 80));
            table2018.Rows.Add(new CO2Allocation(50, 0));
            table2018.Rows.Add(new CO2Allocation(10, 50));
            var table2019 = new CO2AllocationTable(2019);
            table2019.Rows.Add(new CO2Allocation(0, 90));
            pos.AllocationTables.Add(table2019);
            pos.AllocationTables.Add(table2018);

            positions["pos.xml"] = pos;
            positions.Update();
            return pos;
        }

        private CO2Position GetInstance()
        {
            return PopulatePositions(GetPositions());
        }

        [TestMethod]
        public void DoesNotThrowOnValidPosition()
        {
            var pos = GetInstance();
            pos.CheckIntegrity();
        }

        [TestMethod]
        public void ThrowsOnInfiniteExpense()
        {
            var expense = new CO2Expense(0M, 0M, "2018", "");
            Assert.ThrowsExactly<IntegrityException>(() => expense.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnExpenseOverMultipleYears()
        {
            var expense = new CO2Expense(0M, 0M, "2018", "2019");
            Assert.ThrowsExactly<IntegrityException>(() => expense.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnLargePercentage()
        {
            var expense = new CO2Allocation(0M, 110M);
            Assert.ThrowsExactly<IntegrityException>(() => expense.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnNegativePercentage()
        {
            var expense = new CO2Allocation(0M, -10M);
            Assert.ThrowsExactly<IntegrityException>(() => expense.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnEmptyAllocationTable()
        {
            var table = new CO2AllocationTable(2018);
            Assert.ThrowsExactly<IntegrityException>(() => table.CheckIntegrity());
        }

        [TestMethod]
        public void ThrowsOnAllocationTableNotStartingAtZero()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            pos.AllocationTables[0].Rows[0].StartingAt = 5M;
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void ThrowsOnAmbiguousAllocations()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            var rows = pos.AllocationTables[0].Rows;
            rows.Add(rows[0]);
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void ThrowsOnNonDecreasingAllocations()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            var rows = pos.AllocationTables[0].Rows;
            rows[0].Percentage = 50;
            rows[1].Percentage = 90;
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void ThrowsOnMissingAllocationTables()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            pos.AllocationTables.RemoveAt(0);
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void ThrowsOnAmbiguousAllocationTables()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            var tables = pos.AllocationTables;
            tables.Add(tables[0]);
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }


        [TestMethod]
        public void ThrowsOnMultipleCO2Positions()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            positions["some_other_co2.xml"] = pos;
            Assert.ThrowsExactly<IntegrityException>(() => positions.Update());
        }

        [TestMethod]
        public void TableReturnsCorrectAllocationFactor()
        {
            var table = new CO2AllocationTable(2018);
            table.Rows.Add(new CO2Allocation(0, 100));
            table.Rows.Add(new CO2Allocation(50, 70));
            table.Rows.Add(new CO2Allocation(80, 0));
            table.Rows.Add(new CO2Allocation(60, 40));
            table.Sort();
            Assert.AreEqual(1M, table.GetAllocationFactor(0));
            Assert.AreEqual(1M, table.GetAllocationFactor(49));
            Assert.AreEqual(0.7M, table.GetAllocationFactor(50));
            Assert.AreEqual(0.7M, table.GetAllocationFactor(55));
            Assert.AreEqual(0.4M, table.GetAllocationFactor(60));
            Assert.AreEqual(0M, table.GetAllocationFactor(120));
        }

        [TestMethod]
        public void ReturnsCorrectAllocationTable()
        {
            var positions = GetPositions();
            var pos = PopulatePositions(positions);
            var table2000 = new CO2AllocationTable(2000);
            var table2018 = new CO2AllocationTable(2018);
            var table2020 = new CO2AllocationTable(2020);

            // Add dummy rows to prevent integrity checks from triggering
            var row = new CO2Allocation(0, 100);
            table2000.Rows.Add(row);
            table2018.Rows.Add(row);
            table2020.Rows.Add(row);

            pos.AllocationTables.Clear();
            pos.AllocationTables.Add(table2018);
            pos.AllocationTables.Add(table2000);
            pos.AllocationTables.Add(table2020);
            positions.Update();

            var table = new CO2AllocationTable(2018);
            table.Rows.Add(new CO2Allocation(0, 100));
            table.Rows.Add(new CO2Allocation(50, 70));
            table.Rows.Add(new CO2Allocation(80, 0));
            table.Rows.Add(new CO2Allocation(60, 40));
            table.Sort();

            Assert.AreEqual(table2000, pos.GetAllocationTable(2000));
            Assert.AreEqual(table2000, pos.GetAllocationTable(2010));
            Assert.AreEqual(table2000, pos.GetAllocationTable(2017));
            Assert.AreEqual(table2018, pos.GetAllocationTable(2018));
            Assert.AreEqual(table2018, pos.GetAllocationTable(2019));
            Assert.AreEqual(table2020, pos.GetAllocationTable(2020));
            Assert.AreEqual(table2020, pos.GetAllocationTable(9999));
        }

        [TestMethod]
        public void ReturnsCorrectEmissionr()
        {
            var pos = GetInstance();
            Utils.AssertNumericAlmostEqual(0M, pos.GetEmission(1000));
            Utils.AssertNumericAlmostEqual(0M, pos.GetEmission(2017));
            Utils.AssertNumericAlmostEqual(2500M, pos.GetEmission(2018));
            Utils.AssertNumericAlmostEqual(1000M, pos.GetEmission(2019));
            Utils.AssertNumericAlmostEqual(0M, pos.GetEmission(2020));
            Utils.AssertNumericAlmostEqual(0M, pos.GetEmission(3000));


            var date = new DateUtils.Interval("2018", "03-2018");
            Utils.AssertNumericAlmostEqual(500M, pos.GetEmission(date));
            date = new DateUtils.Interval("2018", "01-2018");
            Utils.AssertNumericAlmostEqual(500M * 31 / 90M, pos.GetEmission(date));
            date = new DateUtils.Interval("2018", "2018");
            Utils.AssertNumericAlmostEqual(2500M, pos.GetEmission(date));
            date = new DateUtils.Interval("2019", "2019");
            Utils.AssertNumericAlmostEqual(1000M, pos.GetEmission(date));
            date = new DateUtils.Interval("2018", "2019");
            Utils.AssertNumericAlmostEqual(3500M, pos.GetEmission(date));
        }

        [TestMethod]
        public void ReturnsCorrectAllocationFactor()
        {
            var pos = GetInstance();
            Utils.AssertNumericAlmostEqual(0M, pos.GetAllocationFactor(1000));
            Utils.AssertNumericAlmostEqual(0M, pos.GetAllocationFactor(2017));
            Utils.AssertNumericAlmostEqual(0.5M, pos.GetAllocationFactor(2018));
            Utils.AssertNumericAlmostEqual(0.9M, pos.GetAllocationFactor(2019));
            Utils.AssertNumericAlmostEqual(0M, pos.GetAllocationFactor(2020));
            Utils.AssertNumericAlmostEqual(0M, pos.GetAllocationFactor(3000));
        }

        [TestMethod]
        public void ReturnsCorrectCosts()
        {
            var pos = GetInstance();

            // When computing billing information, we also compute the costs
            // per year without factoring in the tenant / landlord split. Test
            // that this way of computing the costs is consistent with the
            // regular IPosition.GetCosts() computation.
            Func<DateUtils.Interval, decimal> getCostsAlt = interval =>
            {
                decimal sum = 0M;
                foreach (var intervalYear in interval.SplitByYear())
                {
                    var costs = pos.GetCostsWithoutAllocationFactor(intervalYear);
                    var factor = pos.GetAllocationFactor(intervalYear.From.Year);
                    sum += costs * factor;
                }
                return sum;
            };

            var date = new DateUtils.Interval("2018", "03-2018");
            Utils.AssertNumericAlmostEqual(10M * 0.5M, pos.GetCosts(date));
            Utils.AssertNumericAlmostEqual(10M * 0.5M, getCostsAlt(date));
            date = new DateUtils.Interval("2018", "2018");
            Utils.AssertNumericAlmostEqual(40M * 0.5M, pos.GetCosts(date));
            Utils.AssertNumericAlmostEqual(40M * 0.5M, getCostsAlt(date));
            date = new DateUtils.Interval("2019", "2019");
            Utils.AssertNumericAlmostEqual(10M * 0.9M, pos.GetCosts(date));
            Utils.AssertNumericAlmostEqual(10M * 0.9M, getCostsAlt(date));
            date = new DateUtils.Interval("2018", "2019");
            Utils.AssertNumericAlmostEqual(40M * 0.5M + 10M * 0.9M, pos.GetCosts(date));
            Utils.AssertNumericAlmostEqual(40M * 0.5M + 10M * 0.9M, getCostsAlt(date));
        }
    }
}
