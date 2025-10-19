using System;

namespace de.creinbold.FlatShare
{
    public class AccountStatement
    {
        public BankAccount Account { get; private set; }
        public Transaction Transaction { get; private set; }
        public string Use { get; private set; }
        public Currency Currency { get; private set; }

        public AccountStatement(string owner, Transaction transaction, string use, Currency currency)
        {
            Account = new BankAccount(owner, transaction.IBAN);
            Transaction = transaction;
            Use = use;
            Currency = currency;
        }

        public override string ToString()
        {
            var template = "{0:N2}{1} at {2} {3} {4}. Usage: {5}";
            var absAmount = Math.Abs(Transaction.Amount);
            var direction = Transaction.Amount > 0 ? "from" : "to";
            return String.Format(template, absAmount, Currency.Symbol, Transaction.DateAsString, direction, Account, Use);
        }
    }
}
