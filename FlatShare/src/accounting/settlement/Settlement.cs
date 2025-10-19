using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class Settlement
    {
        private class DueDateComparer : IComparer<Claim>
        {
            private int _AscendingFactor;
            public DueDateComparer(bool ascending = true)
            {
                _AscendingFactor = ascending ? 1 : -1;
            }

            public int Compare(Claim c1, Claim c2)
            {
                return _AscendingFactor * c1.DueDate.CompareTo(c2.DueDate);
            }
        }

        private enum EventType { NONE, CREDIT, NEW_CLAIM, OVERDUE_CLAIM };

        private IEnumerator<Credit> _UnprocessedCredits;
        private IEnumerator<Claim> _UnprocessedClaims;

        private List<Claim> _UncoveredClaimsByDescendingDueDate = new List<Claim>();
        private List<Claim> _FutureClaimsByAscendingDueDate = new List<Claim>();
        private List<Claim> _ElapsedClaims = new List<Claim>();

        public IEnumerable<Claim> ElapsedClaims { get { return _ElapsedClaims; } }
        public IEnumerable<Claim> FutureClaims { get { return _FutureClaimsByAscendingDueDate; } }
        public IEnumerable<Claim> UncoveredClaims { get { return _UncoveredClaimsByDescendingDueDate.Concat(_FutureClaimsByAscendingDueDate.Where(c => !c.IsCovered)); } }
        public IEnumerable<Claim> Claims { get { return _ElapsedClaims.Concat(_FutureClaimsByAscendingDueDate); } }
        public IEnumerable<Credit> Credits { get { return _Credits; } }

        private List<Credit> _Credits = new List<Credit>();
        private List<Credit> _NonDepletedCredits = new List<Credit>();

        private DateTime? _lastEvent = null;


        public Settlement(IEnumerable<Claim> claims, IEnumerable<Credit> credits)
        {

            _UnprocessedClaims = claims.GetEnumerator();
            _UnprocessedCredits = credits.GetEnumerator();

            if (!_UnprocessedClaims.MoveNext()) _UnprocessedClaims = null;
            if (!_UnprocessedCredits.MoveNext()) _UnprocessedCredits = null;
        }

        public void SettleUpTo(DateTime dateExclusive)
        {
            for (DateTime? currentEvent = _ProcessNextEvent(dateExclusive); currentEvent.HasValue; currentEvent = _ProcessNextEvent(dateExclusive))
            {
                if (_lastEvent.HasValue && currentEvent.Value < _lastEvent.Value)
                    throw new InvalidOperationException("Events did not trigger in ascending order. Check if the injected claims and credits enumerables are sorted.");
                _lastEvent = currentEvent;
            }
        }

        public decimal GetOverdueAmountFor(Role debtor)
        {
            return _UncoveredClaimsByDescendingDueDate
                       .Where(c => c.CurrentDebtor == debtor)
                       .Sum(c => c.OpenAmount);
        }

        public decimal GetUnassignableCreditFor(Role owner)
        {
            var claimedSince = new Dictionary<DateTime, decimal>();
            var credits = _NonDepletedCredits.Where(c => c.Owner == owner).ToList();
            var unassignable = 0M;
            if (credits.IsEmpty()) return unassignable;
            credits.ElementwiseInvoke(c => claimedSince[c.Transaction.Date] = 0M);
            var keys = claimedSince.Keys.ToList();
            foreach (var claim in _FutureClaimsByAscendingDueDate.Where(c => c.CurrentDebtor == owner))
            {
                foreach (var key in keys.Where(d => claim.AnnounceDate <= d))
                {
                    claimedSince[key] += claim.OpenAmount;
                }
            }
            foreach (var credit in credits)
            {
                var assigned = Math.Min(credit.Remaining, claimedSince[credit.Transaction.Date]);
                unassignable += credit.Remaining - assigned;
                foreach (var key in keys.Where(d => credit.Transaction.Date <= d))
                {
                    claimedSince[key] -= assigned;
                    Debug.Assert(claimedSince[key] >= 0);
                }
            }
            return unassignable;
        }

        private DateTime? _ProcessNextEvent(DateTime latestEventExclusive)
        {
            DateTime nextEventTiming = latestEventExclusive;
            EventType eventType = EventType.NONE;
            if (_UnprocessedClaims != null)
            {
                var date = _UnprocessedClaims.Current.AnnounceDate;
                if (date < nextEventTiming)
                {
                    nextEventTiming = date;
                    eventType = EventType.NEW_CLAIM;
                }
            }
            if (_UnprocessedCredits != null)
            {
                var date = _UnprocessedCredits.Current.Transaction.Date;
                if (date < nextEventTiming)
                {
                    nextEventTiming = date;
                    eventType = EventType.CREDIT;
                }
            }
            if (_FutureClaimsByAscendingDueDate.Count > 0)
            {
                var date = _FutureClaimsByAscendingDueDate.First().DueDate;
                if (date < nextEventTiming)
                {
                    nextEventTiming = date;
                    eventType = EventType.OVERDUE_CLAIM;
                }
            }
            switch (eventType)
            {
                case EventType.CREDIT:
                    var credit = _UnprocessedCredits.Current;
                    if (!_UnprocessedCredits.MoveNext()) _UnprocessedCredits = null;
                    ProcessCredit(credit, nextEventTiming);
                    break;
                case EventType.NEW_CLAIM:
                    var newClaim = _UnprocessedClaims.Current;
                    if (!_UnprocessedClaims.MoveNext()) _UnprocessedClaims = null;
                    ProcessClaim(newClaim, nextEventTiming);
                    break;
                case EventType.OVERDUE_CLAIM:
                    var overdueClaim = _FutureClaimsByAscendingDueDate.First();
                    _FutureClaimsByAscendingDueDate.Remove(overdueClaim);
                    ProcessOverdueClaim(overdueClaim, nextEventTiming);
                    break;
            }
            if (eventType != EventType.NONE) BalanceCreditsWithoutAssignableClaims(nextEventTiming);
            return eventType == EventType.NONE ? null : (DateTime?)nextEventTiming;
        }

        private void ProcessOverdueClaim(Claim claim, DateTime timing)
        {

            _ElapsedClaims.Add(claim);

            foreach (var otherClaim in _UncoveredClaimsByDescendingDueDate)
            {
                claim.BalanceWith(otherClaim, timing);
                if (claim.IsCovered) break;
            }
            _UncoveredClaimsByDescendingDueDate.RemoveAll(c => c.IsCovered);

            foreach (var credit in _NonDepletedCredits.Where(c => c.Transaction.Date >= claim.AnnounceDate))
            {
                claim.BalanceWith(credit, timing);
                if (claim.IsCovered) break;
            }
            _NonDepletedCredits.RemoveAll(c => c.IsDepleted);

            foreach (var otherClaim in _FutureClaimsByAscendingDueDate)
            {
                claim.BalanceWith(otherClaim, timing);
                if (claim.IsCovered) break;
            }

            if (!claim.IsCovered)
            {
                _UncoveredClaimsByDescendingDueDate.Insert(0, claim);
            }

        }

        private void ProcessClaim(Claim claim, DateTime timing)
        {
            // Sorted Insert, see List<T>.BinarySearch as reference.
            var idx = _FutureClaimsByAscendingDueDate.BinarySearch(claim, new DueDateComparer());
            if (idx < 0) idx = ~idx;
            else idx++;
            _FutureClaimsByAscendingDueDate.Insert(idx, claim);

            (claim as AncillaryBillClaim)?.CollectPrepayments(Claims.OfType<ClaimWithPrepayment>(), timing);

            foreach (var otherClaim in _UncoveredClaimsByDescendingDueDate)
            {
                claim.BalanceWith(otherClaim, timing);
                if (claim.IsCovered) break;
            }
            _UncoveredClaimsByDescendingDueDate.RemoveAll(c => c.IsCovered);
        }

        private void ProcessCredit(Credit credit, DateTime timing)
        {
            foreach (var claim in _UncoveredClaimsByDescendingDueDate)
            {
                claim.BalanceWith(credit, timing);
                if (credit.IsDepleted) break;
            }
            _UncoveredClaimsByDescendingDueDate.RemoveAll(c => c.IsCovered);

            _Credits.Add(credit);
            if (!credit.IsDepleted) _NonDepletedCredits.Add(credit);
        }

        private void BalanceCreditsWithoutAssignableClaims(DateTime timing)
        {
            if (_NonDepletedCredits.IsEmpty()) return;

            var announceDates = new Dictionary<Role, DateTime>();
            foreach (Role role in Enum.GetValues(typeof(Role)))
            {
                try
                {
                    announceDates[role] = UncoveredClaims.Where(c => c.CurrentDebtor == role).Select(c => c.AnnounceDate).Min();
                }
                catch (InvalidOperationException)
                {
                    // No uncovered claims
                }
            }
            bool changed = false;
            foreach (var credit in _NonDepletedCredits)
            {
                if (credit.IsDepleted) continue;
                var debtor = credit.Owner.Flip();
                if (!announceDates.ContainsKey(debtor) || credit.Transaction.Date < announceDates[debtor])
                {
                    changed = true;
                    foreach (var otherCredit in _NonDepletedCredits)
                    {
                        credit.BalanceWith(otherCredit, timing);
                        if (credit.IsDepleted) break;
                    }
                }
            }
            if (changed)
                _NonDepletedCredits.RemoveAll(c => c.IsDepleted);
        }
    }
}
