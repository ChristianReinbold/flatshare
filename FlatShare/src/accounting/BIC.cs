using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class BIC
    {
        private static HashSet<string> KNOWN_COUNTRY_CODES = new HashSet<string>(new string[]{
            "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR", "AS", "AT", "AU", "AW", "AX", "AZ",
            "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI", "BJ", "BL", "BM", "BN", "BO", "BQ", "BQ", "BR",
            "BS", "BT", "BV", "BW", "BY", "BZ", "CA", "CC", "CD", "CF", "CG", "CH", "CI", "CK", "CL", "CM",
            "CN", "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM", "DO", "DZ", "EC",
            "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK", "FO", "FR", "GA", "GB", "GD", "GE", "GF",
            "GG", "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM",
            "HN", "HR", "HT", "HU", "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR", "IS", "IT", "JE", "JM",
            "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN", "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC",
            "LI", "LK", "LR", "LS", "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MF", "MG", "MH", "MK",
            "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW", "MX", "MY", "MZ", "NA",
            "NC", "NE", "NF", "NG", "NI", "NL", "NP", "NR", "NU", "NZ", "OM", "PA", "PE", "PF", "PG", "PH",
            "PK", "PL", "PM", "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW", "SA",
            "SB", "SC", "SD", "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM", "SN", "SO", "SR", "SS", "ST",
            "SV", "SX", "SY", "SZ", "TC", "TD", "TF", "TG", "TH", "TJ", "TK", "TL", "TM", "TN", "TO", "TR",
            "TT", "TV", "TW", "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI", "VN",
            "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW"
        });

        public enum ValidationResult
        {
            IsValid = 0,
            InvalidBankCode = 1,
            InvalidCountryCode = 2,
            InvalidLocationCode = 3,
            InvalidBranchCode = 4,
            InvalidLength = 5,
            CountryCodeNotKnown = 6,
        }

        private readonly string _BIC;

        public BIC(string s)
        {
            _BIC = s;
        }

        public override string ToString()
        {
            return _BIC;
        }

        public ValidationResult Validate()
        {
            if (_BIC.Length != 8 && _BIC.Length != 11) return ValidationResult.InvalidLength;
            var bankCode = _BIC.Substring(0, 4);
            var countryCode = _BIC.Substring(4, 2);
            var locationCode = _BIC.Substring(6, 2);
            var branchCode = _BIC.Length > 8 ? _BIC.Substring(8, 3) : "";

            if (!bankCode.All(Char.IsLetter)) return ValidationResult.InvalidBankCode;
            if (!countryCode.All(Char.IsLetter)) return ValidationResult.InvalidCountryCode;
            if (!locationCode.All(Char.IsLetterOrDigit)) return ValidationResult.InvalidLocationCode;
            if (!branchCode.All(Char.IsLetterOrDigit)) return ValidationResult.InvalidBranchCode;

            if (!KNOWN_COUNTRY_CODES.Contains(countryCode)) return ValidationResult.InvalidCountryCode;

            return ValidationResult.IsValid;

        }

        public override bool Equals(object obj)
        {
            var other = obj as BIC;
            if (other == null) return false;
            return _BIC == other._BIC;
        }

        public override int GetHashCode()
        {
            return _BIC.GetHashCode();
        }

        public static implicit operator BIC(string s)
        {
            return new BIC(s);
        }

        public static implicit operator string(BIC bic)
        {
            return bic?.ToString();
        }
    }
}
