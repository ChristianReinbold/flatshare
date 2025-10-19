using System.Collections.Generic;

namespace de.creinbold.FlatShare
{
    public interface IBankAccountOwner
    {
        string Name { get; }
        List<BankAccount> BankAccounts { get; }
    }
}
