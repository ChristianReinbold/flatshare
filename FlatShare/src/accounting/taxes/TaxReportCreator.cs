using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{
    public class TaxReportCreator
    {
        private IFileSystem _Filesystem;
        private Tenants _Tenants;
        private ServiceProviders _ServiceProviders;
        private SettlementManager _SettlementManager;
        private TransactionData _TransactionData;
        private TaxReportTexWriter _TaxReportWriter;

        public TaxReportCreator(
            IFileSystem filesystem,
            TaxReportTexWriter taxReportWriter,
            Tenants tenants,
            ServiceProviders serviceProviders,
            SettlementManager settlementManager,
            TransactionData transactionData)
        {
            _Filesystem = filesystem;
            _Tenants = tenants;
            _ServiceProviders = serviceProviders;
            _SettlementManager = settlementManager;
            _TransactionData = transactionData;
            _TaxReportWriter = taxReportWriter;
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("taxes", _TaxReportCommand, "[-v] <year>", "Creates a tax report for the given year. " +
                "If the v(erbose) flag is set, log messages while creating the pdf are printed.");
        }

        private string _TaxReportCommand(IEnumerable<string> args)
        {
            string invalid_usage = "Invalid argument list. Usage: taxes <year> [-v]";
            bool verbose = false;
            int year;

            var argList = args.ToList();

            if (argList.Count > 0 && "-v".Equals(argList[0]))
            {
                verbose = true;
                argList.RemoveAt(0);
            }

            if (argList.Count != 1) return invalid_usage;
            if (!int.TryParse(argList[0], out year)) return invalid_usage;

            var report = new TaxReport(_Tenants, _ServiceProviders, _SettlementManager, _TransactionData, year);
            UPath filePath = String.Format("/{0}.{1}", report.FileName, _TaxReportWriter.Extension);

            using (var fs = _Filesystem.OpenFile(filePath, FileMode.Create, FileAccess.Write))
            {
                bool success = _TaxReportWriter.Write(report, fs, verbose);
                if (!success) return String.Format("Cannot create tax report. Run command again with -v flag for detailed information.");
            }
            // Open the report for the user in order to check it.
            var winPath = _Filesystem.ConvertPathToInternal(filePath);
            System.Diagnostics.Process.Start(winPath);
            return String.Format("Created tax report for the year {0}", year);
        }
    }
}
