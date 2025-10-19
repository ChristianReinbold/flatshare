using System;

namespace de.creinbold.FlatShare
{
    public interface IFormat
    {
        string DateToString(DateTime date);

        string DateToLongString(DateTime date);

        string RelShareToString(decimal? share);

        string RoomSizeToString(decimal size);

        string NumberToString(decimal number, int places);

        string CostToString(decimal? amountOrNUll, bool includeCurrency = false);

        string AllocationKeyToString(AllocationKey key);

        string ReasonToString(object reason, out bool reasonIsOverwriting, out bool reasonIsImportant);
    }
}
