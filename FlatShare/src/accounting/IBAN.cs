using IbanValidation;
using System;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class IBAN
    {
        private readonly string _IBAN;
        private readonly string _OldSuffix;

        public IBAN(string s)
        {
            s = s.Replace(" ", "").Replace("\t", "").ToUpper();
            var idx = s.IndexOf("OLD");
            if (idx >= 0)
            {
                _IBAN = s.Substring(0, idx);
                _OldSuffix = s.Substring(idx);
            }
            else
            {
                _IBAN = s;
                _OldSuffix = "";
            }
        }

        public override string ToString()
        {
            var splitted = Enumerable.Range(0, (_IBAN.Length + 3) / 4)
                                         .Select(i => _IBAN.Substring(4 * i, Math.Min(4, _IBAN.Length - 4 * i)));
            var s = String.Join(" ", splitted);
            if (!String.IsNullOrEmpty(_OldSuffix)) s += " " + _OldSuffix;
            return s;
        }

        public IbanValidationResult Validate()
        {
            return new IbanValidator().Validate(_IBAN);
        }

        public override bool Equals(object obj)
        {
            var other = obj as IBAN;
            if (other == null) return false;
            return _IBAN == other._IBAN && _OldSuffix == other._OldSuffix;
        }

        public override int GetHashCode()
        {
            return _IBAN.GetHashCode() ^ _OldSuffix.GetHashCode();
        }

        public static implicit operator IBAN(string s)
        {
            return new IBAN(s);
        }

        public static implicit operator string(IBAN iban)
        {
            return iban.ToString();
        }
    }
}
