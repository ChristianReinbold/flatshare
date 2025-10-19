using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace de.creinbold.FlatShare
{
    public sealed class TransactionData : XmlFileParser
    {
        private static ITransactionFileParser[] AVAILABLE_PARSERS = new ITransactionFileParser[] { new DKBCsvFileParser() };

        public List<BankAccount> Blacklist { get; set; } = new List<BankAccount>();
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();

        private Tenants _Tenants;
        private ServiceProviders _ServiceProviders;
        private IEnumerable<IBankAccountOwner> Owners
        {
            get
            {
                if (_ServiceProviders == null) return _Tenants.Values;
                else return _Tenants.Values.Cast<IBankAccountOwner>().Concat(_ServiceProviders.Values);
            }
        }

        private Dictionary<IBankAccountOwner, List<Transaction>> _OwnerToTransactions = new Dictionary<IBankAccountOwner, List<Transaction>>();
        private Dictionary<IBAN, IBankAccountOwner> _IBANToOwner = new Dictionary<IBAN, IBankAccountOwner>();
        private IStorage _Storage;

        private bool _TenantsChanged = false;
        private bool _ServiceProvidersChanged = false;
        private bool _Changed = false;

        /// <summary>
        /// For deserialization only.
        /// </summary>
        public TransactionData() : base() { }

        public TransactionData(IStorage storage, Tenants tenants, ServiceProviders serviceProviders = null) : base(storage, "transaction_data.xml", tenants, serviceProviders)
        {
            _Storage = storage;
            _Tenants = tenants;
            _ServiceProviders = serviceProviders;
            Update();
        }

        public void RegisterCommands(CommandParser cp)
        {
            cp.AddComand("import", Import_CMD, "[-a]",
                 "Imports transactions from a user provided file. " +
                 "By default, only transactions happening after the last known transaction are loaded. " +
                 "Deactivate this behavioud by setting the a(ll) flag."
                 );
        }

        private IEnumerable<AccountStatement> StatementsFromUser(bool importAll)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Import transactions";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() != DialogResult.OK) return Enumerable.Empty<AccountStatement>();

            var statements = dialog.FileNames.SelectMany(ParseFile);
            if (Transactions.Count > 0 && !importAll)
            {
                var lastKnownDate = Transactions.Last().Date;
                statements = statements.Where(s => s.Transaction.Date >= lastKnownDate);
            }
            return statements;
        }

        private string Import_CMD(IEnumerable<string> args)
        {
            var argList = args.ToList();
            if (argList.Count > 1 || (argList.Count == 1 && !String.Equals(argList[0], "-a")))
                return "Invalid argument list. Usage: import [-a].";

            bool importAll = argList.Count == 1;

            var statementList = StatementsFromUser(importAll).ToList();
            statementList.Sort((l, r) => l.Transaction.Date.CompareTo(r.Transaction.Date));

            statementList.RemoveAll(FilterBadStatements);

            var knownTransactions = new HashSet<Transaction>(Transactions);
            var newTransactionCount = 0;
            foreach (var statement in statementList)
            {

                var transaction = statement.Transaction;
                // Filter out transactions we already know about.
                if (knownTransactions.Remove(transaction)) continue;
                // Filter out blacklisted transactions
                if (Blacklist.Contains(statement.Account)) continue;

                if (QueryUserForTransaction(statement))
                {
                    Transactions.Add(transaction);
                    _Changed = true;
                    Debug.Assert(_IBANToOwner.ContainsKey(transaction.IBAN));
                    var owner = _IBANToOwner[transaction.IBAN];
                    _OwnerToTransactions.DefaultCreate(owner, () => new List<Transaction>()).Add(transaction);
                    newTransactionCount++;
                }
            }

            UpdateStorage();

            if (newTransactionCount == 1)
            {
                return String.Format("Imported 1 transaction.");
            }
            else
            {

                return String.Format("Imported {0} transactions.", newTransactionCount);
            }
        }

        private void UpdateStorage()
        {
            var changedStorables = new List<IStorable>();
            if (_Changed) changedStorables.Add(this);
            if (_TenantsChanged) changedStorables.Add(_Tenants);
            if (_ServiceProvidersChanged) changedStorables.Add(_ServiceProviders);

            if (changedStorables.IsEmpty()) return;

            using (var context = _Storage.Open(StorageLocation.VIRTUAL))
            {
                context.NotifyStorablesChanged(changedStorables, FailureMode.PrintAndRollback);
            }

            _Changed = false;
            _TenantsChanged = false;
            _ServiceProvidersChanged = false;
        }

        private bool FilterBadStatements(AccountStatement statement)
        {
            if (statement.Currency != Currency.CURRENT)
            {
                Console.WriteLine("\nWARNING: Ignoring \"" + statement.ToString() + "\" due to unknown currency.");
                return true;
            }
            try
            {
                statement.Account.CheckIntegrity();
            }
            catch (IntegrityException e)
            {
                Console.WriteLine("\nWARNING: Ignoring \"" + statement.ToString() + "\" due to violated integrity constraint. " + e.Message);
                return true;
            }
            return false;
        }

        private IEnumerable<AccountStatement> ParseFile(string file)
        {
            using (var fileStream = File.OpenRead(file))
            {
                foreach (var parser in AVAILABLE_PARSERS)
                {
                    fileStream.Position = 0;
                    try
                    {
                        return parser.Parse(fileStream);
                    }
                    catch (InvalidDataException) { }
                }
            }
            Console.WriteLine(Path.GetFileName(file) + " not supported.");
            return Enumerable.Empty<AccountStatement>();
        }

        private IBankAccountOwner GetMostLikelyBankAccountOwnerFromString(string s)
        {
            int distance;
            return StringMatching.GetMostLikelyMatch(StringMatching.MinDamerauLevenshteinDistanceToSubstring, t => t.Name.ToLower(), s.ToLower(), Owners, out distance);
        }

        private bool QueryUserForTransaction(AccountStatement statement)
        {
            Console.WriteLine();
            Console.WriteLine(statement);
            var ibanKnown = _IBANToOwner.ContainsKey(statement.Transaction.IBAN);

            if (!ibanKnown)
            {
                IBankAccountOwner suggestedOwner = GetMostLikelyBankAccountOwnerFromString(statement.Account.Owner);
                ConsoleKey key;
                while (true)
                {
                    Console.WriteLine(String.Format("[Y]: Add bank account to {0} and import transaction.", suggestedOwner.Name));
                    Console.WriteLine("[C]: Suggest another tenant or service provider.");
                    Console.WriteLine("[I]: Ignore and add account to blacklist.");
                    Console.WriteLine("[N]: Ignore this time.");
                    key = IO.AskForKey(ConsoleKey.Y, ConsoleKey.C, ConsoleKey.I, ConsoleKey.N);
                    if (key == ConsoleKey.C)
                    {
                        Console.Write("Type tenant name: ");
                        var userInput = Console.ReadLine();
                        suggestedOwner = GetMostLikelyBankAccountOwnerFromString(userInput);
                    }
                    else break;
                }
                switch (key)
                {
                    case ConsoleKey.Y:
                        suggestedOwner.BankAccounts.Add(statement.Account);
                        _IBANToOwner[statement.Transaction.IBAN] = suggestedOwner;
                        if (suggestedOwner is Tenant) _TenantsChanged = true;
                        if (suggestedOwner is ServiceProvider) _ServiceProvidersChanged = true;
                        return true;
                    case ConsoleKey.I:
                        Blacklist.Add(statement.Account);
                        Console.WriteLine("Account added to blacklist.");
                        _Changed = true;
                        return false;
                    default:
                        return false;

                }
            }
            else // IBAN is known
            {
                // Service providers are whitelisted. Their transactions are imported without asking the user.
                if (_IBANToOwner[statement.Transaction.IBAN] is ServiceProvider) return true;
                Console.WriteLine("[Y]: Import transaction.");
                Console.WriteLine("[N]: Ignore this time.");
                return IO.AskUser();
            }
        }

        protected override void CopyFrom(object deserialized)
        {
            Transactions.Clear();
            Blacklist.Clear();
            var obj = deserialized as TransactionData;
            if (obj != null)
            {
                Transactions.AddRange(obj.Transactions);
                Blacklist.AddRange(obj.Blacklist);
            }
        }

        public IEnumerable<Transaction> GetTransactionsForOwner(IBankAccountOwner owner)
        {
            return _OwnerToTransactions.GetValueOrDefault(owner, new List<Transaction>()).AsEnumerable();
        }

        public BankAccount GetRecentBankAccountForTenant(IBankAccountOwner owner)
        {
            var transactions = _OwnerToTransactions.GetValueOrDefault(owner, new List<Transaction>());
            if (transactions.Count == 0)
                return null;
            var lastTransaction = transactions.Last();
            return owner.BankAccounts.Find(ba => ba.IBAN.Equals(lastTransaction.IBAN));
        }

        private void UpdateIBANToOwner()
        {
            _IBANToOwner.Clear();
            foreach (var owner in Owners)
            {
                foreach (var iban in owner.BankAccounts.Select(ba => ba.IBAN))
                {
                    if (_IBANToOwner.ContainsKey(iban))
                    {
                        var template = "Duplicate IBAN \"{0}\" assigned to \"{1}\" and \"{2}\"";
                        throw new IntegrityException(String.Format(template, iban, _IBANToOwner[iban].Name, owner.Name));
                    }
                    _IBANToOwner[iban] = owner;
                }
            }
        }

        private void UpdateOwnerToTransactions()
        {
            _OwnerToTransactions.Clear();
            foreach (Transaction transaction in Transactions)
            {
                if (!_IBANToOwner.ContainsKey(transaction.IBAN))
                {
                    var altIBAN = _IBANToOwner.Keys.OrderBy(ib => ((String)transaction.IBAN).DamerauLevenshteinDistanceTo(ib)).FirstOrDefault();
                    if (altIBAN != null)
                    {
                        var template = "Transaction at {0}: IBAN \"{1}\" does not match any tenant or service provider. Alternative IBAN: \"{2}\" ({3})?";
                        throw new IntegrityException(String.Format(template, transaction.DateAsString, transaction.IBAN, altIBAN,
                                                     _IBANToOwner[altIBAN].Name));
                    }
                    else
                    {
                        var template = "Transaction at {0}: IBAN \"{1}\" does not match any tenant or service provider.";
                        throw new IntegrityException(String.Format(template, transaction.DateAsString, transaction.IBAN));
                    }
                }
                else
                {
                    var owner = _IBANToOwner[transaction.IBAN];
                    _OwnerToTransactions.DefaultCreate(owner, () => new List<Transaction>()).Add(transaction);
                }
            }
        }

        protected override void OnUpdated()
        {
            UpdateIBANToOwner();
            UpdateOwnerToTransactions();
            CheckIntegrity();
        }

        public void CheckIntegrity()
        {
            foreach (Transaction transaction in Transactions) transaction.CheckIntegrity();
            foreach (BankAccount account in Blacklist) account.CheckIntegrity("Transaction Data");

            var violatedIndex = DateUtils.FirstNonOrderedDayIndex(Transactions.Select(t => t.Date));
            if (violatedIndex >= 0)
            {
                var template = "TransactionData: The {0}. transaction is not dated after the previous one. Wrong date?";
                throw new IntegrityException(String.Format(template, violatedIndex + 1));
            }
        }
    }
}
