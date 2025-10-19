using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public class BillRecordData : XmlFileParser
    {
        [XmlElement("Bill")]
        public List<BillRecord> BillRecords { get; set; } = new List<BillRecord>();

        private Tenants _Tenants;
        private Dictionary<Tenant, List<BillRecord>> _TenantToBillRecords = new Dictionary<Tenant, List<BillRecord>>();


        /// <summary>
        /// For deserialization only.
        /// </summary>
        public BillRecordData() : base() { }

        public BillRecordData(IStorage storage, Tenants tenants) : base(storage, "bills.xml", tenants)
        {
            _Tenants = tenants;
            Update();
        }

        protected override void CopyFrom(object deserialized)
        {
            BillRecords.Clear();
            var obj = deserialized as BillRecordData;
            if (obj != null)
            {
                BillRecords.AddRange(obj.BillRecords);
            }
        }

        protected override void OnUpdated()
        {
            UpdateTenantToBillRecords();
            CheckIntegrity();
        }

        private void UpdateTenantToBillRecords()
        {
            _TenantToBillRecords.Clear();
            foreach (var record in BillRecords)
            {
                if (!_Tenants.ContainsKey(record.TenantFile))
                {
                    var altTenantFile = _Tenants.Keys.OrderBy(file => record.TenantFile.DamerauLevenshteinDistanceTo(file)).FirstOrDefault();
                    if (altTenantFile != null)
                    {
                        var template = "Bill references the unknown tenant file \"{0}\". Alternative tenant file: \"{1}\" ({2})?";
                        throw new IntegrityException(String.Format(template, record.TenantFile, altTenantFile, _Tenants[altTenantFile].Name));
                    }
                    else
                    {
                        var template = "Bill references the unknown tenant file \"{0}\".";
                        throw new IntegrityException(String.Format(template, record.TenantFile));
                    }
                }
                else
                {
                    var tenant = _Tenants[record.TenantFile];
                    _TenantToBillRecords.DefaultCreate(tenant, () => new List<BillRecord>()).Add(record);
                }
            }
            _TenantToBillRecords.Values.ElementwiseInvoke(list => list.Sort((l, r) => (l.DueDate - r.DueDate).Days));
        }

        public IEnumerable<BillRecord> GetBillRecordsForTenant(Tenant tenant)
        {
            return _TenantToBillRecords.GetValueOrDefault(tenant, new List<BillRecord>()).AsEnumerable();
        }

        public void CheckIntegrity()
        {
            foreach (var record in BillRecords) record.CheckIntegrity();
            var violatedIndex = DateUtils.FirstNonOrderedDayIndex(BillRecords.Select(r => r.CreationDate));
            if (violatedIndex >= 0)
            {
                var template = "BillRecordData: The {0}. bill is not created after the previous one. Wrong creation date?";
                throw new IntegrityException(String.Format(template, violatedIndex + 1));
            }
        }
    }
}
