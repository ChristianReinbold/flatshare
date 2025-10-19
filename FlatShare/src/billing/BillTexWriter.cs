using Antlr4.StringTemplate;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{
    public class BillTexWriter
    {
        private static int MAX_NUMBER_OF_INTERVALS_PER_LIVING_AREA_TABLE = 4;

        private struct CostsLine
        {
            public string From;
            public string To;
            public IEnumerable<string> Allocations;

            public string Name;
            public string Key;

            public string Total;
            public string Share;
            public bool Dash;
        }

        private struct DetailedBlock
        {
            public string From;
            public string To;
            public IEnumerable<CostsLine> Lines;
            public string Total;
            public string Share;
            public IEnumerable<Allocation> Allocations;
        }

        private struct Allocation
        {
            public string Key;
            public string RelativeShare;
        }

        private struct Meter
        {
            public string Name;
            public string Id;
            public string Unit;
            public IEnumerable<string> PosNames;
            public IEnumerable<MeterLine> Lines;
            public IEnumerable<string> PosSums;
            public string DiffSum;

            public int PosCountPlusSeven { get { return PosNames.Count() + 7; } }
        }

        private struct MeterLine
        {
            public string From;
            public string To;
            public string Start;
            public string End;
            public string Diff;
            public IEnumerable<string> Costs;
            public bool Dash;
        }

        private struct CO2
        {
            public string LivingSpaceSize;
            public IEnumerable<CO2CostsLine> CostsLines;
            public string CostsTotal;
            public string CostsShare;
            public IEnumerable<CO2EmissionLine> EmissionLines;
        }

        private struct CO2CostsLine
        {
            public string From;
            public string To;
            public string Emission;
            public string Key;
            public string Total;
            public string Share;
            public bool Dash;
        }
        private struct CO2EmissionLine
        {
            public string Year;
            public string Emission;
            public string EmissionPerSqm;
            public string Key;
            public bool Dash;
        }

        private struct LivingSpaceTable
        {
            public string Unit;
            public string TotalSum;
            public IEnumerable<LivingSpaceTableLine> Lines;
            public IEnumerable<string> Intervals;
            public IEnumerable<string> AbsShareSums;
            public IEnumerable<string> ShareSums;
        }

        private struct LivingSpaceTableLine
        {
            public string Room;
            public string Size;
            public IEnumerable<string> Shares;
            public bool Dash;
        }

        private struct Adjust
        {
            public string Prepayment;
            public string Date;
            public string Total;
        }

        private Tenants _Tenants;
        private TransactionData _TransactionData;
        private IFileSystem _TexDump;

        private static IFormat F = Format.CURRENT;

        public string Extension { get { return "pdf"; } }

        public BillTexWriter(Tenants tenants, TransactionData transactionData, IFileSystem texDump = null)
        {
            _Tenants = tenants;
            _TransactionData = transactionData;
            _TexDump = texDump;
        }

        public bool Write(Bill bill, Stream outStream, bool verbose)
        {
            var texSource = BuildTexSource(bill);

            using (var compiler = new TexCompiler())
            {
                compiler.SourcePath = PrepareTempDirectory(compiler.WorkingDirectory, texSource);
                var success = compiler.TryToCompile(2, verbose);

                if (_TexDump != null)
                {
                    try
                    {
                        UPath targetDir = "/" + bill.FileName;
                        compiler.CopyWorkingDirectory(_TexDump, targetDir);
                    }
                    catch { }
                }

                if (success)
                {
                    try
                    {
                        using (var compiledStream = compiler.WorkingDirectory.OpenFile(compiler.CompiledPath, FileMode.Open, FileAccess.Read))
                        {
                            compiledStream.CopyTo(outStream);
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }
                return success;
            }
        }

        private UPath PrepareTempDirectory(IFileSystem tempFilesystem, string texSource)
        {
            using (var writer = new StreamWriter(tempFilesystem.OpenFile("/bill.tex", FileMode.CreateNew, FileAccess.Write)))
            {
                writer.Write(texSource);
            }
            using (var writer = new StreamWriter(tempFilesystem.OpenFile("/sender.lco", FileMode.CreateNew, FileAccess.Write)))
            {
                writer.Write(Properties.Resources.TexSender);
            }
            using (var writer = new BinaryWriter(tempFilesystem.OpenFile("/signature.pdf", FileMode.CreateNew, FileAccess.Write)))
            {
                writer.Write(Properties.Resources.Signature);
            }
            return "/bill.tex";
        }

        private string BuildTexSource(Bill bill)
        {
            var templateString = TexCompiler.PreprocessTemplateString(Properties.Resources.TexBill);
            var template = new Template(templateString);

            // Fill template
            template.Add("tenant", bill.Tenant);
            template.Add("is_male", bill.Tenant.Gender == Tenant.Genders.MALE);
            AddAdressLines(template, bill.Tenant.Address);
            template.Add("creation_date", F.DateToLongString(bill.CreationDate));
            template.Add("due_date", F.DateToString(bill.DueDate));
            template.Add("from", F.DateToString(bill.TotalInterval.From));
            template.Add("to", F.DateToString(bill.TotalInterval.To));
            template.Add("total_costs_incl_currency", F.CostToString(bill.SumTotal, true));
            template.Add("share_costs_incl_currency", F.CostToString(bill.SumShare, true));
            template.Add("total_costs", F.CostToString(bill.SumTotal));
            template.Add("share_costs", F.CostToString(bill.SumShare));
            template.Add("prepayment", F.CostToString(bill.Prepayment, true));
            template.Add("currency_symbol", Currency.CURRENT.TexSymbol);

            bool isRefund = bill.Prepayment > bill.SumShare;
            decimal balance = Math.Abs(bill.Prepayment - bill.SumShare);

            template.Add("is_refund", isRefund);
            template.Add("balance", F.CostToString(balance, true));
            BankAccount account;
            if (isRefund) account = _TransactionData.GetRecentBankAccountForTenant(bill.Tenant);
            else account = bill.Landlord.BankAccount;
            template.Add("account", account);
            AddEntriesForCostsPerInterval(template, bill);
            AddEntriesForCostsPerPosition(template, bill);
            AddEntriesForCostsPerPositionAndInterval(template, bill);
            // Prevent "attribute not defined" string template error
            template.Add("meters", null);
            bill.Meters.ElementwiseInvoke(m => AddEntriesForMeter(template, m));
            AddEntriesForCO2(template, bill);
            AddEntriesForLivingArea(template, bill);
            AddEntryForPrepaymentUpdate(template, bill);

            return template.Render();
        }

        private void AddAdressLines(Template template, IEnumerable<Tenant.AddressLine> address)
        {
            // Prevent "attribute not defined" string template error
            template.Add("adress_line", null);
            foreach (var l in address)
            {
                if (l is Tenant.Space)
                    template.Add("adress_line", @"\medskip");
                else
                    template.Add("adress_line", l.ToString() + @"\\");
            }
        }

        private void AddEntriesForCostsPerInterval(Template template, Bill bill)
        {
            var keys = EnumUtils.AllValues(AllocationKey.PERS).ToList();
            keys.ElementwiseInvoke(k => template.Add("keys", F.AllocationKeyToString(k)));
            for (var i = 1; i < 10; i++)
            {
                template.Add(String.Format("n_keys_plus_{0}", i), keys.Count + i);
            }

            // Prevent "attribute not defined" string template error
            template.Add("costs_per_interval", null);

            for (var i = 0; i < bill.Shares.Length; i++)
            {
                var share = bill.Shares[i];
                var line = new CostsLine();
                line.From = F.DateToString(share.Interval.From);
                line.To = F.DateToString(share.Interval.To);
                line.Allocations = keys.Select(k => F.RelShareToString(share.GetRelative(k)));
                line.Total = F.CostToString(bill.SumPerIntervalTotal[i]);
                line.Share = F.CostToString(bill.SumPerIntervalShare[i]);
                line.Dash = HasDashAfterLine(i, bill.Shares.Length);
                template.Add("costs_per_interval", line);
            }
        }

        private void AddEntriesForCostsPerPosition(Template template, Bill bill)
        {
            // Prevent "attribute not defined" string template error
            template.Add("costs_per_position", null);

            for (var i = 0; i < bill.Positions.Length; i++)
            {
                var position = bill.Positions[i];
                var line = new CostsLine();
                line.Name = position.Name;
                if (position is CO2Position) line.Name += " - Mieteranteil";
                else if (position is IMeteredPosition) line.Name += " - Verbrauch";
                line.Name = TexCompiler.EscapeSpecialCharacters(line.Name);
                line.Key = F.AllocationKeyToString(position.Allocation);
                line.Total = F.CostToString(bill.SumPerPosTotal[i]);
                line.Share = F.CostToString(bill.SumPerPosShare[i]);
                line.Dash = HasDashAfterLine(i, bill.Positions.Length);
                template.Add("costs_per_position", line);
            }
        }

        private void AddEntriesForCostsPerPositionAndInterval(Template template, Bill bill)
        {
            // Prevent "attribute not defined" string template error
            template.Add("detailed_blocks", null);

            for (var intervalIdx = 0; intervalIdx < bill.Shares.Length; intervalIdx++)
            {
                var detailedBlock = new DetailedBlock();
                var share = bill.Shares[intervalIdx];

                detailedBlock.From = F.DateToString(share.Interval.From);
                detailedBlock.To = F.DateToString(share.Interval.To);
                detailedBlock.Total = F.CostToString(bill.SumPerIntervalTotal[intervalIdx]);
                detailedBlock.Share = F.CostToString(bill.SumPerIntervalShare[intervalIdx]);

                var allocation_keys = EnumUtils.AllValues(AllocationKey.PERS).ToList();
                var allocations = new List<Allocation>();
                foreach (var key in allocation_keys)
                {
                    var allocation = new Allocation();
                    allocation.Key = F.AllocationKeyToString(key);
                    allocation.RelativeShare = F.RelShareToString(share.GetRelative(key));
                    allocations.Add(allocation);
                }
                detailedBlock.Allocations = allocations;

                var lines = new List<CostsLine>();
                for (var i = 0; i < bill.Positions.Length; i++)
                {
                    var position = bill.Positions[i];
                    var line = new CostsLine();
                    line.Name = position.Name;
                    if (position is CO2Position) line.Name += " - Mieteranteil";
                    else if (position is IMeteredPosition) line.Name += " - Verbrauch";
                    line.Name = TexCompiler.EscapeSpecialCharacters(line.Name);
                    line.Key = F.AllocationKeyToString(position.Allocation);
                    line.Total = F.CostToString(bill.ItemTotal[intervalIdx, i]);
                    line.Share = F.CostToString(bill.ItemShare[intervalIdx, i]);
                    line.Dash = HasDashAfterLine(i, bill.Positions.Length);
                    lines.Add(line);
                }
                detailedBlock.Lines = lines;
                template.Add("detailed_blocks", detailedBlock);
            }
        }

        private void AddEntriesForMeter(Template template, Bill.MeterData meter)
        {
            if (meter.AssociatedPositions.IsEmpty()) return;
            var meterStrings = new Meter();
            meterStrings.Name = TexCompiler.EscapeSpecialCharacters(meter.Name);
            meterStrings.Id = meter.Id;
            meterStrings.Unit = meter.Unit;
            meterStrings.PosNames = meter.AssociatedPositions.Select(c => TexCompiler.EscapeSpecialCharacters(c.Name)).ToList();
            meterStrings.PosSums = meter.Sum.Select(s => F.NumberToString(s, 2)).ToList();
            meterStrings.DiffSum = F.NumberToString(meter.DiffSum, 0);

            var lines = new List<MeterLine>();
            for (int intervalIdx = 0; intervalIdx < meter.Intervals.Length; intervalIdx++)
            {
                var line = new MeterLine();
                line.From = F.DateToString(meter.Intervals[intervalIdx].From);
                line.To = F.DateToString(meter.Intervals[intervalIdx].To);
                line.Start = F.NumberToString(meter.Start[intervalIdx], 0);
                line.End = F.NumberToString(meter.End[intervalIdx], 0);
                line.Diff = F.NumberToString(meter.Diff[intervalIdx], 0);
                line.Dash = HasDashAfterLine(intervalIdx, meter.Intervals.Length);
                line.Costs = Enumerable.Range(0, meter.AssociatedPositions.Length)
                                       .Select(i => F.CostToString(meter.Item[intervalIdx, i]))
                                       .ToList();
                lines.Add(line);
            }
            meterStrings.Lines = lines;

            template.Add("meters", meterStrings);
        }

        private void AddEntriesForCO2(Template template, Bill bill)
        {
            if (!bill.CO2.HasValue)
            {
                // Prevent "attribute not defined" string template error
                template.Add("co2", null);
                return;
            }

            var data = bill.CO2.Value;

            var strings = new CO2();
            strings.LivingSpaceSize = F.RoomSizeToString(data.LivingSpaceSize);
            strings.CostsTotal = F.CostToString(data.CostsTotal);
            strings.CostsShare = F.CostToString(data.CostsShare);

            var costsLines = new List<CO2CostsLine>();
            var emissionLines = new List<CO2EmissionLine>();
            for (int i = 0; i < data.Intervals.Length; i++)
            {
                {
                    var l = new CO2CostsLine();
                    l.From = F.DateToString(data.Intervals[i].From);
                    l.To = F.DateToString(data.Intervals[i].To);
                    l.Emission = F.NumberToString(data.EmissionsPerInterval[i], 1);
                    l.Key = F.RelShareToString(data.KeyPerYear[i]);
                    l.Total = F.CostToString(data.CostsTotalPerInterval[i]);
                    l.Share = F.CostToString(data.CostsSharePerInterval[i]);
                    l.Dash = HasDashAfterLine(i, data.Intervals.Length);
                    costsLines.Add(l);
                }
                {
                    var l = new CO2EmissionLine();
                    l.Year = data.Intervals[i].From.Year.ToString();
                    l.Emission = F.NumberToString(data.EmissionsPerYear[i], 1);
                    l.EmissionPerSqm = F.NumberToString(data.EmissionsPerYearSqm[i], 1);
                    l.Key = F.RelShareToString(data.KeyPerYear[i]);
                    l.Dash = HasDashAfterLine(i, data.Intervals.Length);
                    emissionLines.Add(l);
                }
            }
            strings.CostsLines = costsLines;
            strings.EmissionLines = emissionLines;

            template.Add("co2", strings);
        }

        private void AddEntriesForLivingArea(Template template, Bill bill)
        {

            var unit = F.AllocationKeyToString(AllocationKey.SQM);
            var totalSum = F.RoomSizeToString(bill.RoomSizeSum);
            var intervals = bill.Shares.Select(s => String.Format("{0} -- {1}",
                F.DateToString(s.Interval.From),
                F.DateToString(s.Interval.To))).ToList();
            var absShareSums = bill.AbsRoomSharePerInterval.Select(s => F.RoomSizeToString(s)).ToList();
            var shareSums = bill.RelRoomSharePerInterval.Select(s => F.RelShareToString(s)).ToList();

            var shareCount = bill.Shares.Length;
            var tableCount = (int)Math.Ceiling(shareCount / (decimal)MAX_NUMBER_OF_INTERVALS_PER_LIVING_AREA_TABLE);

            if (tableCount == 0)
            {
                // Prevent "attribute not defined" string template error
                template.Add("living_space_tables", null);
                return;
            }

            var shareCountPerTable = (int)Math.Ceiling(shareCount / (decimal)tableCount);
            Debug.Assert(shareCountPerTable <= MAX_NUMBER_OF_INTERVALS_PER_LIVING_AREA_TABLE);


            for (var tableIdx = 0; tableIdx < tableCount; tableIdx++)
            {
                var shareStartForTable = tableIdx * shareCountPerTable;
                var shareCountForTable = Math.Min(shareCount - shareStartForTable, shareCountPerTable);
                var table = new LivingSpaceTable();
                table.Unit = unit;
                table.TotalSum = totalSum;
                table.Intervals = intervals.GetRange(shareStartForTable, shareCountForTable);
                table.AbsShareSums = absShareSums.GetRange(shareStartForTable, shareCountForTable);
                table.ShareSums = shareSums.GetRange(shareStartForTable, shareCountForTable);

                var lines = new List<LivingSpaceTableLine>();
                for (var roomIdx = 0; roomIdx < bill.Rooms.Length; roomIdx++)
                {
                    var line = new LivingSpaceTableLine();
                    line.Room = TexCompiler.EscapeSpecialCharacters(bill.Rooms[roomIdx].Name);
                    line.Size = F.RoomSizeToString(bill.Rooms[roomIdx].SquareMeter);
                    line.Shares = Enumerable.Range(shareStartForTable, shareCountForTable)
                                            .Select(i => F.RelShareToString(bill.SharePerRoomAndInterval[roomIdx, i]))
                                            .ToList();

                    line.Dash = HasDashAfterLine(roomIdx, bill.Rooms.Length);
                    lines.Add(line);
                }
                table.Lines = lines;

                template.Add("living_space_tables", table);
            }
        }

        private void AddEntryForPrepaymentUpdate(Template template, Bill bill)
        {
            if (!bill.AdjustedPrepayment.HasValue)
            {
                template.Add("adjust", null);
                return;
            }
            var adjustment = bill.AdjustedPrepayment.Value;
            var adjust = new Adjust();
            adjust.Prepayment = F.CostToString(adjustment.Value, true);
            adjust.Date = F.DateToString(adjustment.BeginningAt);
            adjust.Total = F.CostToString(adjustment.NewRent, true);
            template.Add("adjust", adjust);
        }

        private bool HasDashAfterLine(int lineIdx, int lineCount)
        {
            return (lineIdx % 3) == 2 && lineIdx < lineCount - 1;
        }
    }
}
