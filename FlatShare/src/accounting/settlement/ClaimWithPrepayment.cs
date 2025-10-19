using System;

namespace de.creinbold.FlatShare
{
    public abstract class ClaimWithPrepayment : Claim
    {

        public DateTime PrepaymentDate { get; private set; }
        public decimal Prepayment { get; private set; }
        public decimal CoveredPrepayment
        {
            get
            {
                var tmp = Prepayment;
                Prepayment = 0M;
                var coveredPart = -GetAssignablePart(-tmp);
                Prepayment = tmp;
                return coveredPart;
            }
        }

        public decimal WithdrawnPrepayment { get; private set; }

        public bool PrepaymentIsResolved { get { return Prepayment == 0M; } }

        protected override decimal SignedAmount
        {
            get
            {
                return base.SignedAmount + Prepayment;
            }
        }


        protected ClaimWithPrepayment(DateTime announceDate, DateTime dueDate, decimal fixedAmount, decimal prepayment, DateTime prepaymentDate) : base(announceDate, dueDate, fixedAmount)
        {
            Prepayment = prepayment;
            PrepaymentDate = prepaymentDate;
            WithdrawnPrepayment = 0M;
        }

        public decimal WithdrawPrepayment(object target, DateTime date)
        {
            var withdrawnAmount = this.CoveredPrepayment;
            Prepayment = 0M;
            Assign(-withdrawnAmount, target, date);
            WithdrawnPrepayment += withdrawnAmount;
            return withdrawnAmount;
        }
    }
}
