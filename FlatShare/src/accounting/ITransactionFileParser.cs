using System.Collections.Generic;
using System.IO;

namespace de.creinbold.FlatShare
{
    public interface ITransactionFileParser
    {
        /// <exception cref="System.IO.InvalidDataException">Thrown when the parser fails to process the given stream.</exception>
        IEnumerable<AccountStatement> Parse(Stream stream);
    }
}
