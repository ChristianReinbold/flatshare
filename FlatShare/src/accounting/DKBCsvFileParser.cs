using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace de.creinbold.FlatShare
{
    public class DKBCsvFileParser : ITransactionFileParser
    {
        private static NumberStyles AMOUNT_STYLE = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        private static IFormatProvider AMOUNT_FORMAT = new CultureInfo("de-DE");

        public IEnumerable<AccountStatement> Parse(Stream stream)
        {

            var parser = new TextFieldParser(stream, Encoding.Default);
            parser.SetDelimiters(";");
            int[] positions = null;
            try
            {
                positions = searchHead(parser,
                                       "Wertstellung",
                                       "Zahlungspflichtige*r",
                                       "Zahlungsempfänger*in",
                                       "Verwendungszweck",
                                       "Umsatztyp",
                                       "IBAN",
                                       "Betrag (€)"
                                       );
            }
            catch (Exception) { }
            if (positions == null) throw new InvalidDataException("No appropriate header found");

            var dateIdx = positions[0];
            var senderIdx = positions[1];
            var receiverIdx = positions[2];
            var useIdx = positions[3];
            var directionIdx = positions[4];
            var ibanIdx = positions[5];
            var valueIdx = positions[6];

            var statements = new List<AccountStatement>();
            for (var parts = parser.ReadFields(); parts != null; parts = parser.ReadFields())
            {
                decimal amount;
                if (!decimal.TryParse(parts[valueIdx], AMOUNT_STYLE, AMOUNT_FORMAT, out amount))
                    throw new InvalidDataException("Unexpected format of number: " + parts[valueIdx]);
                DateTime date;
                if (!DateTime.TryParse(parts[dateIdx], out date))
                    throw new InvalidDataException("Unexpected format of date: " + parts[dateIdx]);
                var transaction = new Transaction(parts[ibanIdx], date, amount);
                var owner = parts[directionIdx].Equals("Ausgang") ? parts[receiverIdx] : parts[senderIdx];
                statements.Add(new AccountStatement(owner, transaction, parts[useIdx], Currency.EURO));
            }
            return statements;
        }

        private int[] searchHead(TextFieldParser parser, params string[] attributes)
        {
            var positions = new int[attributes.Length];
            for (var parts = parser.ReadFields(); parts != null; parts = parser.ReadFields())
            {
                if (AttributePositionsFromHead(parts, attributes, ref positions))
                    return positions;
            }
            return null;
        }

        private bool AttributePositionsFromHead(string[] head, string[] attributes, ref int[] positions)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                var idx = Array.IndexOf(head, attributes[i]);
                if (idx < 0) return false;
                positions[i] = idx;
            }
            return true;
        }
    }
}
