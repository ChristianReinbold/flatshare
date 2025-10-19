using System;
using System.Globalization;

namespace de.creinbold.FlatShare
{
    public class FormatDE : IFormat
    {
        private static IFormatProvider format_provider = CultureInfo.CreateSpecificCulture("de-DE");

        public string DateToString(DateTime date)
        {
            return date.ToString("dd.MM.yyyy", format_provider);
        }

        public string DateToLongString(DateTime date)
        {
            return date.ToString("dd. MMMM yyyy", format_provider);
        }

        public string NumberToString(decimal number, int places)
        {
            return number.ToString("N" + places.ToString(), format_provider);
        }

        public string RelShareToString(decimal? share)
        {
            if (!share.HasValue) return " ";
            return (share.Value * 100).ToString("N1", format_provider) + @"\%";
        }

        public string RoomSizeToString(decimal size)
        {
            return size.ToString("N1", format_provider);
        }

        public string CostToString(decimal? amountOrNUll, bool includeCurrency = false)
        {
            if (!amountOrNUll.HasValue) return "---";
            var amount = amountOrNUll.Value;
            var s = amount.ToString("N2", format_provider);
            var integral = Math.Floor(amount) == amount;
            if (includeCurrency)
            {
                if (integral)
                {
                    s = s.Substring(0, s.Length - 2) + "--";
                }
                s = Currency.CURRENT.TexStringWithSymbol(s);
            }
            return s;
        }

        public string AllocationKeyToString(AllocationKey key)
        {
            switch (key)
            {
                case AllocationKey.SQM:
                    return "qm";
                case AllocationKey.PERS:
                    return "pers";
                default:
                    throw new ArgumentException("Unknown allocation key " + key.ToString());
            }
        }

        public string ReasonToString(object reason, out bool reasonIsOverwriting, out bool reasonIsImportant)
        {
            reasonIsOverwriting = false;
            reasonIsImportant = false;
            if (reason == null)
            {
                reasonIsOverwriting = true;
                reasonIsImportant = true;
                return "nicht zugeordnet";
            }
            if (reason is Credit)
            {
                reasonIsOverwriting = true;
                var credit = reason as Credit;
                return "verrechnet mit Transaktion vom " + DateToString(credit.Transaction.Date);
            }
            if (reason is RentClaim) return "Miete";
            if (reason is DepositRateClaim) return "Kaution";
            if (reason is DepositRefundClaim) return "Kaution";
            if (reason is AncillaryBillClaim) return "NK-Abrechnung";
            throw new ArgumentException("Unknown reason type.");
        }
    }
}
