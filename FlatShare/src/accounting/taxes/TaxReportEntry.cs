using System;

namespace de.creinbold.FlatShare
{
    public struct TaxReportEntry
    {
        public object Reason { get; set; }
        public decimal AssignedAmount { get; set; }
        public DateTime? Due { get; set; }
        public int TaxYear { get; set; }
        public decimal? NetTax { get; set; }
        public decimal? AncillaryTax { get; set; }
        public decimal FlowForDeposit { get; set; }
    }
}
