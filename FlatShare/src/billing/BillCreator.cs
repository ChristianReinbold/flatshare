using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Zio;

namespace de.creinbold.FlatShare
{
    public class BillCreator
    {
        private IFileSystem _Filesystem;
        private IStorage _Storage;
        private DateUtils.IDateProvider _DateProvider;
        private Tenants _Tenants;
        private Landlord _Landlord;
        private RoomData _RoomData;
        private Meters _Meters;
        private Positions _Positions;
        private BillRecordData _BillRecordData;
        private Shares _Shares;
        private SettlementManager _SettlementManager;

        private BillTexWriter _BillWriter;

        public BillCreator(
            IFileSystem filesystem,
            BillTexWriter billWriter,
            IStorage storage,
            RoomData roomData,
            Tenants tenants,
            Landlord landlord,
            Meters meters,
            Positions positions,
            BillRecordData billRecordData,
            SettlementManager settlementManager,
            DateUtils.IDateProvider dateProvider = null)
        {
            _Filesystem = filesystem;
            _BillWriter = billWriter;
            _Storage = storage;
            if (dateProvider == null) dateProvider = new DateUtils.SystemDateProvider();
            _DateProvider = dateProvider;
            _Tenants = tenants;
            _Landlord = landlord;
            _RoomData = roomData;
            _Meters = meters;
            _Positions = positions;
            _BillRecordData = billRecordData;
            _SettlementManager = settlementManager;
            _Shares = new Shares(storage, roomData, tenants);
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("bill", _BillCommand, "[-v] <from> [<to>] [<tenantName>]",
                "Creates ancillary bills for the given tenant in order to settle costs in the given time span. " +
                "If the tenant is omitted, create bills for all tenants. " +
                "Allowed date formats: YYYY, MM-YYYY, DD-MM-YYYY. " +
                "If <to> is not supplied, the string literal of <from> is reused, expanding missing day or month values to the end of the respective month or year. " +
                "If the v(erbose) flag is set, log messages while creating the pdf are printed.");
        }

        private string _BillCommand(IEnumerable<string> args)
        {
            if (_Tenants.IsEmpty()) return "No tenants in the database.";

            var argList = args.ToList();
            string invalid_usage = "Invalid argument list. Usage: bill [-v] <from> [<to>] [<tenantName>]. Allowed date formats: YYYY, MM-YYYY, DD-MM-YYYY";
            DateTime from;
            DateTime to;
            string tenantNameArg = null;
            bool verbose = false;
            Tenant tenant = null;
            bool exactTenantMatch;

            if (argList.Count > 0 && "-v".Equals(argList[0]))
            {
                verbose = true;
                argList.RemoveAt(0);
            }

            if (argList.IsEmpty())
            {
                return invalid_usage;
            }

            try { from = DateUtils.DateFromString(argList[0], DateUtils.DateFillMode.First); }
            catch (FormatException) { return invalid_usage; }

            switch (argList.Count)
            {
                case 1:
                    try
                    {
                        from = DateUtils.DateFromString(argList[0], DateUtils.DateFillMode.First);
                        to = DateUtils.DateFromString(argList[0], DateUtils.DateFillMode.Last);
                    }
                    catch (FormatException)
                    {
                        return invalid_usage;
                    }
                    break;
                case 2:
                    try
                    {
                        from = DateUtils.DateFromString(argList[0], DateUtils.DateFillMode.First);
                        to = DateUtils.DateFromString(argList[0], DateUtils.DateFillMode.Last);
                    }
                    catch (FormatException)
                    {
                        return invalid_usage;
                    }

                    try { to = DateUtils.DateFromString(argList[1], DateUtils.DateFillMode.Last); }
                    catch (FormatException)
                    {
                        tenantNameArg = argList[1];
                        tenant = _Tenants.GetMostLikelyTenantFromString(argList[1], out exactTenantMatch);
                    }
                    break;
                case 3:
                    try
                    {
                        from = DateUtils.DateFromString(argList[0], DateUtils.DateFillMode.First);
                        to = DateUtils.DateFromString(argList[1], DateUtils.DateFillMode.Last);
                    }
                    catch (FormatException)
                    {
                        return invalid_usage;
                    }
                    tenantNameArg = argList[2];
                    tenant = _Tenants.GetMostLikelyTenantFromString(argList[2], out exactTenantMatch);
                    break;
                default:
                    return invalid_usage;
            }
            var interval = new DateUtils.Interval(from, to);

            if (interval.IsEmpty)
            {
                return "\"from\" date located after \"to\" date.";
            }

            TryToDeleteOrphanedBillFiles();

            try
            {
                interval = AdjustIntervalOrCancelByUser(interval);
            }
            catch (OperationCanceledException)
            {
                return "Canceled.";
            }

            IEnumerable<Bill> bills;
            if (tenant != null) bills = CreateBills(interval, tenant);
            else bills = CreateBillsForAllTenants(interval);

            try
            {
                bills = bills.ToList();
            }
            catch (IntegrityException e)
            {
                Console.WriteLine(e.Message);
                return "Aborted due to errors while creating bills.";
            }

            GetLastBillPerTenant(bills).ElementwiseInvoke(b => AskUserForAdjustedPrepayment(b));

            var acceptedBills = bills.Where(b => ProcessBill(b, verbose)).ToList();
            acceptedBills.ElementwiseInvoke(b => _BillRecordData.BillRecords.Add(BillRecord.FromBill(b, _Tenants)));
            var updatedRentsCount = acceptedBills.Count(b => AdjustPrepaymentIfRequired(b));
            TryToDeleteOrphanedBillFiles();

            var changedStorables = new List<IStorable>();
            if (acceptedBills.Count > 0) changedStorables.Add(_BillRecordData);
            if (updatedRentsCount > 0) changedStorables.Add(_Tenants);

            if (changedStorables.Count > 0)
            {
                using (var context = _Storage.Open(StorageLocation.VIRTUAL))
                {
                    bool success = context.NotifyStorablesChanged(changedStorables, FailureMode.PrintAndRollback);
                    if (!success) return "";
                }
            }
            return String.Format("{0} bill(s) issued.", acceptedBills.Count);
        }

        private DateUtils.Interval AdjustIntervalOrCancelByUser(DateUtils.Interval interval)
        {
            const string msg_future = "Billing period is located in the future.";
            const string msg_infinite = "Position \"{0}\" continue infinitely long.";
            const string msg_not_finished = "Position \"{0}\" are only specified up to {1}. If this is intended, suppress this warning by adding the attribute \"Finished=true\".";

            var warnings = new List<string>();

            bool endInFuture = false;
            if (_DateProvider.Now < interval.To)
            {
                endInFuture = true;
                warnings.Add(msg_future);
                interval.To = _DateProvider.Now;
                if (interval.IsEmpty)
                {
                    Console.WriteLine("Error: Billing period starts in the future.");
                    throw new OperationCanceledException("Operation canceled since billing period starts in the future.");
                }
            }

            DateTime maxDateWithFullSpecification = interval.To;


            foreach (var pos in _Positions.Entries)
            {
                if (!pos.Interval.IsFinite) warnings.Add(String.Format(msg_infinite, pos.Name));
                if (!pos.Finished)
                {
                    if (pos.Interval.IsFinite) maxDateWithFullSpecification = DateUtils.Min(pos.Interval.To, maxDateWithFullSpecification);
                    if (pos.Interval.CompareTo(interval.To) < 0) warnings.Add(String.Format(msg_not_finished, pos.Name, pos.Interval.ToAsString));
                }
            }

            if (warnings.IsEmpty()) return interval;


            warnings.ElementwiseInvoke(w => Console.WriteLine("Warning: " + w));

            var canAdjust = interval.Contains(maxDateWithFullSpecification) && interval.To != maxDateWithFullSpecification;
            if (canAdjust)
            {
                var maxDateString = DateUtils.DateAsString(maxDateWithFullSpecification, DateUtils.DateFillMode.Last);
                var toString = DateUtils.DateAsString(interval.To, DateUtils.DateFillMode.Last);
                Console.WriteLine("[Y/A/N]: Continue?");
                if (endInFuture) Console.WriteLine("[Y]: Continue with billing period end adjusted to today.");
                else Console.WriteLine("[Y]: Continue with original billing period.");
                Console.WriteLine("[A]: Adjust billing period end to " + maxDateString + " and continue.");
                Console.WriteLine("[N]: Cancel command.");
                var key = IO.AskForKey(ConsoleKey.Y, ConsoleKey.A, ConsoleKey.N);
                if (key == ConsoleKey.N) throw new OperationCanceledException("Operation canceled by user.");
                if (key == ConsoleKey.A) return new DateUtils.Interval(interval.From, maxDateWithFullSpecification);
                return interval;
            }
            else
            {
                if (endInFuture) Console.WriteLine("[Y/N]: Continue with billing period end adjusted to today?");
                else Console.WriteLine("[Y/N]: Continue?");
                if (!IO.AskUser()) throw new OperationCanceledException("Operation canceled by user.");
                else return interval;
            }
        }

        private bool VerifyBillByUser(Bill bill)
        {
            Console.WriteLine();
            Console.WriteLine(bill);
            Console.WriteLine("[Y]: Accept and issue bill.");
            Console.WriteLine("[N]: Discard bill.");
            return IO.AskUser();
        }

        private IEnumerable<Bill> GetLastBillPerTenant(IEnumerable<Bill> bills)
        {
            var lastBills = new Dictionary<Tenant, Bill>();
            foreach (var bill in bills)
            {
                var lastBill = lastBills.DefaultCreate(bill.Tenant, () => bill);
                if (bill.TotalInterval.CompareTo(lastBill.TotalInterval) > 0)
                {
                    lastBills[bill.Tenant] = bill;
                }
            }
            return lastBills.Values;
        }

        private void AskUserForAdjustedPrepayment(Bill bill)
        {
            var rents = bill.Tenant.Rents;
            var beginningAt = bill.DueDate;
            if (beginningAt.Day != 1)
            {
                beginningAt = beginningAt.AddMonths(1);
                beginningAt = beginningAt.AddDays(1 - beginningAt.Day);
            }

            // No rent to update.
            if (rents.Count == 0) return;
            var lastRent = rents.Last();
            // Skip the adjustment if it would not fall into the interval of the last known rent.
            if (lastRent.Interval.CompareTo(beginningAt) != 0) return;

            var template = "Do you wish to keep the current prepayment of {0}{1} for {2}?";
            Console.WriteLine();
            Console.WriteLine(String.Format(template, lastRent.Ancillary, Currency.CURRENT.Symbol, bill.Tenant.Name));

            var totalDays = bill.Shares.Aggregate(new IntegerWithInfinity(0), (prev, s) => prev + s.Interval.Days);
            var avgCostsPerMonth = bill.SumShare / ((decimal)totalDays * 12 / 365);
            template = "Avg. ancillary costs for the current billing period: {0:N0}{1} per month.";
            Console.WriteLine(String.Format(template, avgCostsPerMonth, Currency.CURRENT.Symbol));

            int? newValue = null;
            do
            {
                Console.Write("New prepayment (leave blank for no change): ");
                var s = Console.ReadLine();
                if (String.IsNullOrEmpty(s))
                    return;
                int i;
                if (int.TryParse(s, out i) && i >= 0)
                {
                    newValue = i;
                }
                else
                {
                    Console.WriteLine("Value has to be an integral, nonnegative number.");
                }
            } while (!newValue.HasValue);
            Bill.Adjustment adjustment;
            adjustment.Value = newValue.Value;
            adjustment.BeginningAt = beginningAt;
            adjustment.NewRent = lastRent.Net + newValue.Value;
            bill.AdjustedPrepayment = adjustment;
        }

        private bool AdjustPrepaymentIfRequired(Bill b)
        {
            if (!b.AdjustedPrepayment.HasValue) return false;
            Debug.Assert(b.Tenant.Rents.Count > 0);
            var adjustment = b.AdjustedPrepayment.Value;
            var rents = b.Tenant.Rents;
            var lastRent = rents.Last();
            Debug.Assert(adjustment.Value + lastRent.Net == adjustment.NewRent);
            var newRent = new MonthlyRent(
                lastRent.Net,
                adjustment.Value,
                lastRent.DueDay,
                DateUtils.DateAsString(adjustment.BeginningAt),
                lastRent.ToAsString,
                DateUtils.DateAsString(b.CreationDate)
                );
            lastRent.Interval.To = adjustment.BeginningAt.AddDays(-1);
            if (lastRent.Interval.IsEmpty) rents.RemoveAt(rents.Count - 1);
            rents.Add(newRent);
            return true;
        }

        private IEnumerable<Bill> CreateBillsForAllTenants(DateUtils.Interval interval)
        {
            return _Tenants.Entries.SelectMany(t => CreateBills(interval, t));
        }

        private IEnumerable<Bill> CreateBills(DateUtils.Interval interval, Tenant tenant)
        {
            if (!interval.IsFinite) throw new ArgumentException("Only finite intervals permitted.");
            var records = _BillRecordData.GetBillRecordsForTenant(tenant);
            var uncoveredIntervals = interval.Except(records.Select(r => r.Interval));
            foreach (var uncoveredInterval in uncoveredIntervals)
            {
                var shares = _Shares.EnumerateShares(tenant, uncoveredInterval);
                var prunedInterval = new DateUtils.Interval(DateTime.MaxValue, DateTime.MinValue);
                foreach (var share in shares)
                {
                    prunedInterval.From = DateUtils.Min(prunedInterval.From, share.Interval.From);
                    prunedInterval.To = DateUtils.Max(prunedInterval.To, share.Interval.To);
                }
                if (prunedInterval.IsEmpty) continue;

                var settlement = _SettlementManager.GetSettlementForTenant(tenant);
                var claimsWithPrepayment = settlement.Claims.OfType<ClaimWithPrepayment>();
                var claimsWithPrepaymentInInterval = claimsWithPrepayment.Where(c => prunedInterval.Contains(c.PrepaymentDate));
                var prepayment = claimsWithPrepaymentInInterval.Select(c => c.CoveredPrepayment).Sum();
                var rooms = _RoomData.Rooms.Where(r => r.IsLivingSpace);
                var now = _DateProvider.Now;
                yield return new Bill(tenant, _Landlord, shares, _Positions.Entries, _Meters.Entries, rooms, prepayment, now, now.AddMonths(1).AddDays(-1));
            }
        }

        private void TryToDeleteOrphanedBillFiles()
        {
            string regex = @"\A\d\d\d\d-\d\d-\d\d_";
            foreach (var path in _Filesystem.EnumerateFiles("/"))
            {
                var name_without_extension = path.GetNameWithoutExtension();
                bool valid_bill_file_name = Regex.Matches(name_without_extension, regex).Count > 0;
                bool orphaned = _BillRecordData.BillRecords.All(b => !String.Equals(b.BillFile, name_without_extension));
                if (orphaned && valid_bill_file_name)
                {
                    try
                    {
                        _Filesystem.DeleteFile(path);
                    }
                    catch { }
                }
            }
        }

        private bool ProcessBill(Bill bill, bool verbose)
        {
            char[] invalidFileChars = Path.GetInvalidFileNameChars();
            var fileName = bill.Tenant.Name.RemoveCharacters(invalidFileChars).Replace(' ', '_');
            UPath filePath = String.Format("/{0}_{1}.{2}",
                bill.CreationDate.ToString("yyyy-MM-dd"),
                fileName,
                _BillWriter.Extension);
            filePath = IO.AddNumberIfExists(_Filesystem, filePath);

            bill.FileName = filePath.GetNameWithoutExtension();

            var billCreated = true;
            using (var fs = _Filesystem.OpenFile(filePath, FileMode.CreateNew, FileAccess.Write))
            {
                billCreated = _BillWriter.Write(bill, fs, verbose);
            }

            if (!billCreated)
            {
                try
                {
                    _Filesystem.DeleteFile(filePath);
                }
                catch { }
                var errorMsg = "Cannot create " + bill.ToString() + ".";
                if (!verbose) errorMsg += " Run command again with -v flag for detailed information.";
                Console.WriteLine(errorMsg);
                return false;
            }

            // Open the bill for the user in order to check and/or print it.
            var winPath = _Filesystem.ConvertPathToInternal(filePath);
            System.Diagnostics.Process.Start(winPath);
            return VerifyBillByUser(bill);
        }
    }
}
