using System;

namespace de.creinbold.FlatShare
{
    public class Currency
    {
        public static Currency EURO = new Currency("EUR", @"\EUR{{{0}}}");

        public static Currency CURRENT = EURO;

        public string Symbol { get; private set; }
        public string TexSymbol { get { return TexStringWithSymbol(""); } }

        private string _TexFormat;

        public Currency(string symbol, string texFormat)
        {
            Symbol = symbol;
            _TexFormat = texFormat;
        }

        public string TexStringWithSymbol(string s)
        {
            return String.Format(_TexFormat, s);
        }
    }
}
