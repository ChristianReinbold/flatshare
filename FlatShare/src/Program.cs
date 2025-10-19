using System;
using System.Collections.Generic;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{
    class Program
    {
        private static string PROMPT = "$>";

        public CommandParser Parser { get; private set; }

        private DateUtils.SystemDateProvider _DateProvider;
        private WorkingFilesystemProvider _WorkingFilesystemProvider;
        private FileStorage _Storage;
        private Meters _Meters;
        private Positions Positions;
        private Tenants _Tenants;
        private Landlord _Landlord;
        private RoomData _RoomData;
        private TransactionData _TransactionData;
        private BillRecordData _BillRecordData;
        private SettlementManager _SettlementManager;
        private BillCreator _BillCreator;
        private TaxReportCreator _TaxReportCreator;
        private ServiceProviders _ServiceProviders;
        private ServiceProviderCommands _ServiceProviderCommands;

        private Program()
        {
            _DateProvider = new DateUtils.SystemDateProvider();
            Init();
            LoadValidStateOrPromptUser();
        }

        private void Init()
        {
            _WorkingFilesystemProvider = new WorkingFilesystemProvider();
            _WorkingFilesystemProvider.Changed += OnWorkingDirectoryChanged;

            var workingFilesystem = _WorkingFilesystemProvider.Get();
            var billsFilesystem = workingFilesystem.GetOrCreateSubFileSystem("/bills");
            var taxesFilesystem = workingFilesystem.GetOrCreateSubFileSystem("/taxes");
            _Storage = new FileStorage(workingFilesystem);
            _Meters = new Meters(_Storage);
            _RoomData = new RoomData(_Storage);
            Positions = new Positions(_Storage, _Meters, _RoomData);
            _ServiceProviders = new ServiceProviders(_Storage, Positions);
            _Tenants = new Tenants(_Storage, new DateUtils.SystemDateProvider());
            _Landlord = new Landlord(_Storage);
            _TransactionData = new TransactionData(_Storage, _Tenants, _ServiceProviders);
            _BillRecordData = new BillRecordData(_Storage, _Tenants);
            _SettlementManager = new SettlementManager(_Storage, _Tenants, _TransactionData, _BillRecordData, _DateProvider);
            _ServiceProviderCommands = new ServiceProviderCommands(_ServiceProviders, _TransactionData, _DateProvider);

            var texDump = workingFilesystem.GetOrCreateSubFileSystem("/tex");
            var billFileWriter = new BillTexWriter(_Tenants, _TransactionData, texDump);
            var taxReportFileWriter = new TaxReportTexWriter(texDump);
            _BillCreator = new BillCreator(billsFilesystem, billFileWriter, _Storage, _RoomData, _Tenants, _Landlord, _Meters, Positions, _BillRecordData, _SettlementManager, _DateProvider);
            _TaxReportCreator = new TaxReportCreator(taxesFilesystem, taxReportFileWriter, _Tenants, _ServiceProviders, _SettlementManager, _TransactionData);

            Parser = new CommandParser();
            _WorkingFilesystemProvider.RegisterCommands(Parser);
            _Storage.RegisterCommands(Parser);
            _Tenants.RegisterCommands(Parser);
            _TransactionData.RegisterCommands(Parser);
            _SettlementManager.RegisterCommands(Parser);
            _ServiceProviderCommands.RegisterCommands(Parser);
            _BillCreator.RegisterCommands(Parser);
            _TaxReportCreator.RegisterCommands(Parser);
        }

        private void OnWorkingDirectoryChanged(object sender, string e)
        {
            Init();
            LoadValidStateOrPromptUser();
        }

        private void PromptUserDueToInvalidState(bool allowManualFix)
        {
            Console.WriteLine("Loading from storage failed...");
            Console.WriteLine("[S]: Load sample scenario.");
            Console.WriteLine("[W]: Change working directory.");
            if (allowManualFix)
                Console.WriteLine("[F]: Fix storage manually.");
            Console.WriteLine("[Q]: Quit the application.");
            while (true)
            {
                var keys = new List<ConsoleKey>(new ConsoleKey[] { ConsoleKey.S, ConsoleKey.W, ConsoleKey.Q });
                if (allowManualFix)
                    keys.Add(ConsoleKey.F);
                var key = IO.AskForKey(keys.ToArray());
                switch (key)
                {
                    case ConsoleKey.S:
                        Console.WriteLine();
                        Console.WriteLine(LoadSampleScenario());
                        Console.WriteLine();
                        return;
                    case ConsoleKey.W:
                        if (_WorkingFilesystemProvider.SetByUser())
                        {
                            Console.WriteLine();
                            return;
                        }
                        break;
                    case ConsoleKey.F:
                        if (_Storage.MaintainByUser())
                        {
                            Console.WriteLine();
                            return;
                        }
                        PromptUserDueToInvalidState(allowManualFix);
                        return;
                    case ConsoleKey.Q:
                        Environment.Exit(0);
                        return;
                }
            }
        }

        private void LoadValidStateOrPromptUser()
        {
            var result = _Storage.TryToLoadAndUpdate();
            switch (result)
            {
                case FileStorage.LoadResult.Success:
                    return;
                case FileStorage.LoadResult.NoAccess:
                    PromptUserDueToInvalidState(false);
                    return;
                case FileStorage.LoadResult.FailedParsing:
                    PromptUserDueToInvalidState(true);
                    return;
                default:
                    throw new ArgumentException("Unknown load result " + result);
            }
        }

        private string LoadSampleScenario()
        {
            _Storage.Backup();
            _Storage.Clear();
            SampleScenario.Inject(_Meters, Positions, _Tenants, _Landlord, _RoomData, _TransactionData, _ServiceProviders, _BillRecordData);
            using (var context = _Storage.Open(StorageLocation.VIRTUAL))
            {
                var changedStorables = new IStorable[] { _Meters, Positions, _Tenants, _Landlord, _RoomData, _TransactionData, _ServiceProviders, _BillRecordData };
                if (context.NotifyStorablesChanged(changedStorables, FailureMode.Print))
                {
                    return "Scenario loaded.";
                }
                else
                {
                    Console.Write("Press key to exit application...");
                    Console.ReadKey();
                    Environment.Exit(-1);
                    return "";
                }
            }
        }

        // Use STAThread since some components want to use dialogs.
        [STAThread]
        static void Main(string[] args)
        {
            var inst = new Program();

            Console.Write(PROMPT);
            for (var cmd = Console.ReadLine(); ; cmd = Console.ReadLine())
            {
                var output = inst.Parser.Execute(cmd);
                if (!String.IsNullOrEmpty(output))
                    Console.WriteLine(output);
                Console.Write(PROMPT);
            }
        }
    }
}
