namespace de.creinbold.FlatShare
{
    static class SampleScenario
    {
        public static void Inject(Meters meters, Positions positions, Tenants tenants, Landlord landlord, RoomData roomData, TransactionData transactionData, ServiceProviders serviceProviders, BillRecordData billRecordData)
        {
            InjectMeters(meters);
            InjectPositions(positions);
            InjectTenants(tenants);
            InjectLandlord(landlord);
            InjectRoomData(roomData);
            InjectTransactionData(transactionData);
            InjectServiceProviders(serviceProviders);
            InjectBillRecordData(billRecordData);
        }

        private static void InjectMeters(Meters meters)
        {
            var gas_meter = new Meter("Gas", "Id1234", "$m^3$");
            gas_meter.Values.Add(new MeterValue("15-09-2017", 14052));
            gas_meter.Values.Add(new MeterValue("02-02-2018", 14943));
            gas_meter.Values.Add(new MeterValue("28-02-2018", 15280));
            gas_meter.Values.Add(new MeterValue("23-08-2018", 15691));
            gas_meter.Values.Add(new MeterValue("03-09-2018", 15699));
            gas_meter.Values.Add(new MeterValue("11-09-2018", 15707));
            gas_meter.Values.Add(new MeterValue("19-10-2018", 15754, true));
            gas_meter.Values.Add(new MeterValue("29-01-2019", 15852, true));
            gas_meter.Values.Add(new MeterValue("01-04-2019", 16455));
            meters["gas_id1234.xml"] = gas_meter;

            gas_meter = new Meter("Gas", "Id73", "$m^3$");
            gas_meter.Values.Add(new MeterValue("01-04-2019", 0));
            gas_meter.Values.Add(new MeterValue("09-04-2019", 31));
            gas_meter.Values.Add(new MeterValue("01-06-2019", 175));
            gas_meter.Values.Add(new MeterValue("11-08-2019", 244, true));
            gas_meter.Values.Add(new MeterValue("22-08-2019", 251, true));
            gas_meter.Values.Add(new MeterValue("18-09-2019", 321, true));
            gas_meter.Values.Add(new MeterValue("01-04-2020", 822, true));
            meters["gas_id73.xml"] = gas_meter;
        }

        private static void InjectPositions(Positions positions)
        {
            var gas_grundpreis = new Position("Gas - Grundpreis", AllocationKey.SQM);
            gas_grundpreis.Expenses.Add(new RepeatedExpense(99.91M, Frequency.ONCE, "15-09-2017", "18-10-2018"));
            gas_grundpreis.Expenses.Add(new RepeatedExpense(89.91M, Frequency.ONCE, "19-10-2018", "18-10-2019"));

            var gas_verbrauch = new MeteredPosition("Gas", AllocationKey.SQM);
            gas_verbrauch.MeterFiles.Add("gas_id1234.xml");
            gas_verbrauch.MeterFiles.Add("gas_id73.xml");
            gas_verbrauch.Expenses.Add(new Expense(401.25M, "15-09-2017", "18-10-2018"));
            gas_verbrauch.Expenses.Add(new Expense(421.05M, "19-10-2018", "18-10-2019"));

            var gas_co2 = new CO2Position("CO2", AllocationKey.SQM);
            gas_co2.MeterFiles.Add("gas_id1234.xml");
            gas_co2.MeterFiles.Add("gas_id73.xml");
            gas_co2.Expenses.Add(new CO2Expense(22.53M, 1855.4M, "2018", "18-10-2018"));
            gas_co2.Expenses.Add(new CO2Expense(12.20M, 1663.5M, "19-10-2018", "2018"));
            gas_co2.Expenses.Add(new CO2Expense(12.05M, 1663.5M, "2019", "18-10-2019"));
            gas_co2.Expenses.Add(new CO2Expense(15.25M, 522.5M, "19-10-2019", "2019"));
            var table = new CO2AllocationTable(2018);
            table.Rows.Add(new CO2Allocation(0, 100));
            table.Rows.Add(new CO2Allocation(12, 90));
            table.Rows.Add(new CO2Allocation(17, 80));
            table.Rows.Add(new CO2Allocation(22, 70));
            table.Rows.Add(new CO2Allocation(27, 60));
            table.Rows.Add(new CO2Allocation(32, 50));
            table.Rows.Add(new CO2Allocation(37, 40));
            table.Rows.Add(new CO2Allocation(42, 30));
            table.Rows.Add(new CO2Allocation(47, 20));
            table.Rows.Add(new CO2Allocation(52, 5));
            gas_co2.AllocationTables.Add(table);

            var grundsteuer = new Position("Grundsteuer", AllocationKey.SQM);
            grundsteuer.Expenses.Add(new RepeatedExpense(293.67M, Frequency.ANNUAL, "2013", "2016"));
            grundsteuer.Expenses.Add(new RepeatedExpense(300.89M, Frequency.ANNUAL, "2017"));

            var heizungswartung = new Position("Heizungswartung", AllocationKey.SQM);
            heizungswartung.Expenses.Add(new RepeatedExpense(152.22M, Frequency.ONCE, "2017", "2017"));
            heizungswartung.Expenses.Add(new RepeatedExpense(528.57M, Frequency.ONCE, "2018", "2018"));
            heizungswartung.Expenses.Add(new RepeatedExpense(160.21M, Frequency.ONCE, "2019", "2019"));

            var gez = new Position("Rundfunkbeitrag", AllocationKey.PERS);
            gez.Expenses.Add(new RepeatedExpense(17.50M, Frequency.MONTHLY, "2017"));

            positions.Clear();
            positions.Add("gas_basic.xml", gas_grundpreis);
            positions.Add("gas_consumption.xml", gas_verbrauch);
            positions.Add("gas_co2.xml", gas_co2);
            positions.Add("land_tax.xml", grundsteuer);
            positions.Add("heating_maintenance.xml", heizungswartung);
            positions.Add("gez.xml", gez);
        }

        private static void InjectTenants(Tenants tenants)
        {
            var landlord = new Tenant("Anna", "Vermieterin", Tenant.Genders.FEMALE);
            landlord.IsCharged = false;
            landlord.AddressAsString = "Musterstrasse 6\n\n1234 Musterstadt";
            landlord.RoomAllocations.Add(new RoomAllocation("Vermieter", 2, "10-2017", "", true));
            landlord.BankAccounts.Add(new BankAccount("Anna Vermieterin", "DE61500105172759258284", "BYWYBYLXXXX"));

            var tenant1 = new Tenant("Max", "Mustermann", Tenant.Genders.MALE);
            tenant1.AddressAsString = "Küstenweg 55\n\n1111 Rebburg";
            tenant1.RoomAllocations.Add(new RoomAllocation("Mieter1", 1, "10-2017", "15-05-2018"));
            tenant1.BankAccounts.Add(new BankAccount("Max Mustermann", "DE42500105176555521727", "BYWYBYLXXXX"));
            tenant1.BankAccounts.Add(new BankAccount("Max Mustermann", "DE25500105176262548956", "BYWYBYLXXXX"));
            tenant1.Rents.Add(new MonthlyRent(210M, 90M, 3, "10-2017", "04-2018", "22-09-2017"));
            tenant1.Rents.Add(new MonthlyRent(105M, 45M, 3, "05-2018", "15-05-2018", "22-09-2017"));
            tenant1.Deposit = new Deposit(500M, 100M, 0M);

            var tenant2 = new Tenant("Felix", "Mustermieter", Tenant.Genders.MALE);
            tenant2.AddressAsString = "Bergstraße 2\n\n9222 Senflingen";
            tenant2.RoomAllocations.Add(new RoomAllocation("Mieter1", 1, "06-2018"));
            tenant2.BankAccounts.Add(new BankAccount("Felix Mustername", "DE12500105172922943271", "BYWYBYLXXXX"));
            tenant2.Rents.Add(new MonthlyRent(210M, 90M, 3, "06-2018", "", "14-05-2018"));
            tenant2.Deposit = new Deposit(500M);

            var tenant3 = new Tenant("Emilia", "Musterfrau", Tenant.Genders.FEMALE);
            tenant3.AddressAsString = "An der Wiese 2c\n\n2333 Kirchfeld";
            tenant3.RoomAllocations.Add(new RoomAllocation("Mieter2", 1, "10-2017"));
            tenant3.BankAccounts.Add(new BankAccount("Fam. Musterfrau", "DE56500105179297371587", "BYWYBYLXXXX"));
            tenant3.Rents.Add(new MonthlyRent(250M, 90M, 3, "10-2017", "2017", "18-09-2017"));
            tenant3.Rents.Add(new MonthlyRent(275M, 80M, 3, "2018", "", "14-11-2017"));
            tenant3.Deposit = new Deposit(600M);

            tenants.Clear();
            tenants.Add("vermieterin.xml", landlord);
            tenants.Add("mustermann.xml", tenant1);
            tenants.Add("mustermieter.xml", tenant2);
            tenants.Add("musterfrau.xml", tenant3);
        }

        private static void InjectLandlord(Landlord landlord)
        {
            landlord.BankAccount = new BankAccount("Anna Vermieterin", "DE84500105176732472736", "BYWYBYLXXXX");
        }

        private static void InjectRoomData(RoomData roomData)
        {
            var rooms = roomData.Rooms;
            rooms.Clear();
            // UG
            rooms.Add(new Room("Waschen UG", 5.65m, false));
            rooms.Add(new Room("Heizen UG", 3.31m, false));
            // EG
            rooms.Add(new Room("Schlafen EG Süd", 11.12m));
            rooms.Add(new Room("Schlafen EG Ost", 9.27m));
            rooms.Add(new Room("Bad EG", 8.09m));
            rooms.Add(new Room("Küche EG", 7.87m));
            rooms.Add(new Room("Wohnen EG", 14.74m));
            rooms.Add(new Room("Flur", 4.27m));
            // OG
            rooms.Add(new Room("Bad OG", 6.40m));
            rooms.Add(new Room("Schlafen OG", 13.22m));
            rooms.Add(new Room("Arbeiten OG", 12.78m));

            var roomGroups = roomData.RoomGroups;
            roomGroups.Clear();
            roomGroups.Add(new RoomGroup("UG", "Waschen UG", "Heizen UG"));
            roomGroups.Add(new RoomGroup("OG", "Bad OG", "Schlafen OG", "Arbeiten OG"));
            roomGroups.Add(new RoomGroup("Gemeinschaft", "Küche EG", "Flur", "UG", "Wohnen EG"));
            roomGroups.Add(new RoomGroup("Vermieter", "Gemeinschaft", "OG"));
            roomGroups.Add(new RoomGroup("Mieter1", "Gemeinschaft", "Bad EG", "Schlafen EG Süd"));
            roomGroups.Add(new RoomGroup("Mieter2", "Gemeinschaft", "Bad EG", "Schlafen EG Ost"));
        }

        private static void InjectTransactionData(TransactionData transactionData)
        {
            var blacklist = transactionData.Blacklist;
            blacklist.Clear();
            blacklist.Add(new BankAccount("Amazon", "DE40500105175274621534", "BYWYBYLXXXX"));
            blacklist.Add(new BankAccount("Blacklisted Account", "DE50500105179139172261", "BYWYBYLXXXX"));
            var transactions = transactionData.Transactions;
            transactions.Clear();

            // Stadtwerke
	        transactions.Add(new Transaction("DE79500105176412367782", "25-10-2018", -523.69M));
            transactions.Add(new Transaction("DE79500105176412367782", "18-10-2019", -535.21M));
            // Rundfunk
	        transactions.Add(new Transaction("DE17500105177839573645", "01-03-2017", -52.50M));
	        transactions.Add(new Transaction("DE17500105177839573645", "01-06-2017", -52.50M));
	        transactions.Add(new Transaction("DE17500105177839573645", "01-09-2017", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-12-2017", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-03-2018", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-06-2018", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-09-2018", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-12-2018", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-03-2019", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-06-2019", -52.50M));
	        transactions.Add(new Transaction("DE38500105175882946982", "01-09-2019", -52.50M));
            transactions.Add(new Transaction("DE38500105175882946982", "01-12-2019", -52.50M));
            // Finanzamt
	        transactions.Add(new Transaction("DE61500105178734368753", "16-06-2013", -293.67M));
	        transactions.Add(new Transaction("DE61500105178734368753", "16-06-2014", -293.67M));
	        transactions.Add(new Transaction("DE61500105178734368753", "16-06-2015", -293.67M));
	        transactions.Add(new Transaction("DE61500105178734368753", "16-06-2016", -293.67M));
	        transactions.Add(new Transaction("DE61500105178734368753", "16-06-2017", -300.89M));
	        transactions.Add(new Transaction("DE61500105178734368753", "16-06-2018", -300.89M));
            transactions.Add(new Transaction("DE61500105178734368753", "16-06-2019", -300.89M));
            // Sanitärexperten
            transactions.Add(new Transaction("DE63500105174568333856", "18-08-2017", -152.22M));
            transactions.Add(new Transaction("DE63500105174568333856", "02-11-2018", -528.57M));
            // Sagenhaft
            transactions.Add(new Transaction("DE48500105177521363326", "06-10-2019", -160.21M));
            // Max Mustermann
	        transactions.Add(new Transaction("DE42500105176555521727", "22-09-2017", 800M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-10-2017", 300M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-11-2017", 300M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-12-2017", 300M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-01-2018", 300M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-02-2018", 300M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-03-2018", 300M));
	        transactions.Add(new Transaction("DE25500105176262548956", "22-04-2018", 150M));
            transactions.Add(new Transaction("DE25500105176262548956", "15-05-2018", -400M));
            // Felix Mustermann
	        transactions.Add(new Transaction("DE12500105172922943271", "25-05-2018", 500M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-06-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-07-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-08-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-09-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-10-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "28-11-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-12-2018", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-01-2019", 400M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-02-2019", 200M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-03-2019", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-04-2019", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-05-2019", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-06-2019", 600M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-08-2019", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-09-2019", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-10-2019", 300M));
	        transactions.Add(new Transaction("DE12500105172922943271", "02-11-2019", 300M));
            transactions.Add(new Transaction("DE12500105172922943271", "02-12-2019", 300M));
            // Emilia Musterfrau
	        transactions.Add(new Transaction("DE56500105179297371587", "27-09-2017", 600M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-10-2017", 340M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-11-2017", 340M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-12-2017", 340M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-01-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-02-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-03-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-04-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "01-05-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-06-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-07-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-08-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-09-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-10-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-11-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-12-2018", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-01-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-02-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-03-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-04-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-05-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-06-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-07-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-08-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-09-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-10-2019", 355M));
	        transactions.Add(new Transaction("DE56500105179297371587", "02-11-2019", 355M));
            transactions.Add(new Transaction("DE56500105179297371587", "02-12-2019", 355M));

            transactions.Sort((x, y) => x.Date.CompareTo(y.Date));
        }

        private static void InjectServiceProviders(ServiceProviders providers)
        {
            providers.Clear();

            var stadtwerke = new ServiceProvider("Stadtwerke");
            stadtwerke.BankAccounts.Add(new BankAccount("Stadtwerke GmbH", "DE79500105176412367782", "BYWYBYLXXXX"));
            stadtwerke.Services.Add(new Service("gas_basic.xml", "2017"));
            stadtwerke.Services.Add(new Service("gas_consumption.xml", "2017"));
            stadtwerke.Services.Add(new Service("gas_co2.xml", "2017"));

            var rundfunk = new ServiceProvider("Rundfunk");
            rundfunk.BankAccounts.Add(new BankAccount("ARD ZDF DRadio Beitragsservice", "DE17500105177839573645", "BYWYBYLXXXX"));
            rundfunk.BankAccounts.Add(new BankAccount("Rundfunk ARD, ZDF, DRadio", "DE38500105175882946982", "BYWYBYLXXXX"));
            rundfunk.Services.Add(new Service("gez.xml", "2017"));

            var finanzamt = new ServiceProvider("Finanzamt");
            finanzamt.BankAccounts.Add(new BankAccount("Finanzamt Musterstadt", "DE61500105178734368753", "BYWYBYLXXXX"));
            finanzamt.Services.Add(new Service("land_tax.xml", "2013"));

            var wartungsfirma1 = new ServiceProvider("Sanitärexperten GmbH");
            wartungsfirma1.BankAccounts.Add(new BankAccount("Sanitärexperten GmbH", "DE63500105174568333856", "BYWYBYLXXXX"));
            wartungsfirma1.Services.Add(new Service("heating_maintenance.xml", "2017", "2018"));

            var wartungsfirma2 = new ServiceProvider("Familienbetrieb Sagenhaft");
            wartungsfirma2.BankAccounts.Add(new BankAccount("Sagenhaft Sanitär", "DE48500105177521363326", "BYWYBYLXXXX"));
            wartungsfirma2.Services.Add(new Service("heating_maintenance.xml", "2019"));

            providers.Add("stadtwerke.xml", stadtwerke);
            providers.Add("rundfunk.xml", rundfunk);
            providers.Add("finanzamt.xml", finanzamt);
            providers.Add("wartungsfirma1.xml", wartungsfirma1);
            providers.Add("wartungsfirma2.xml", wartungsfirma2);
        }

        private static void InjectBillRecordData(BillRecordData billRecordData)
        {
            var billRecords = billRecordData.BillRecords;
            billRecords.Clear();
        }
    }
}
