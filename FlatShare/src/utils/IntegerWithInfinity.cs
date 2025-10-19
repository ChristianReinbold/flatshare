using System;

namespace de.creinbold.FlatShare
{
    public struct IntegerWithInfinity : IComparable<IntegerWithInfinity>
    {
        public static readonly IntegerWithInfinity INFINITY;

        static IntegerWithInfinity()
        {
            INFINITY.SetToInfinity();
        }

        private int _Nominator;
        private int _Denominator;

        public IntegerWithInfinity(int value)
        {
            _Nominator = value;
            _Denominator = 1;
        }

        public int Value
        {
            get { return (int)this; }
            set { _Nominator = value; _Denominator = 1; }
        }

        public bool isFinite { get { return _Denominator != 0; } }

        public int Sign { get { return Math.Sign(_Nominator); } }

        public void SetToInfinity(int sign = 1)
        {
            _Nominator = Math.Sign(sign);
            _Denominator = 0;
        }

        public static explicit operator int(IntegerWithInfinity value)
        {
            try
            {
                return value._Nominator / value._Denominator;
            }
            catch (DivideByZeroException)
            {
                throw new InvalidCastException("Infinity cannot be cast to integer.");
            }
        }

        public static implicit operator IntegerWithInfinity(int value)
        {
            return new IntegerWithInfinity() { _Nominator = value, _Denominator = 1 };
        }

        public int CompareTo(IntegerWithInfinity obj)
        {
            return _Nominator * obj._Denominator - obj._Nominator * _Denominator;
        }

        public static bool operator <(IntegerWithInfinity obj1, IntegerWithInfinity obj2)
        {
            return obj1.CompareTo(obj2) < 0;
        }
        public static bool operator >(IntegerWithInfinity obj1, IntegerWithInfinity obj2)
        {
            return obj1.CompareTo(obj2) > 0;
        }
        public static bool operator ==(IntegerWithInfinity obj1, IntegerWithInfinity obj2)
        {
            return obj1.CompareTo(obj2) == 0;
        }
        public static bool operator !=(IntegerWithInfinity obj1, IntegerWithInfinity obj2)
        {
            return obj1.CompareTo(obj2) != 0;
        }
        public static bool operator <=(IntegerWithInfinity obj1, IntegerWithInfinity obj2)
        {
            return obj1.CompareTo(obj2) <= 0;
        }
        public static bool operator >=(IntegerWithInfinity obj1, IntegerWithInfinity obj2)
        {
            return obj1.CompareTo(obj2) >= 0;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is IntegerWithInfinity)) return false;
            return (IntegerWithInfinity)obj == this;
        }

        public override int GetHashCode()
        {
            return _Nominator ^ _Denominator;
        }

        public static IntegerWithInfinity operator +(IntegerWithInfinity l, IntegerWithInfinity r)
        {
            IntegerWithInfinity result;
            result._Nominator = l._Nominator * r._Denominator + r._Nominator * l._Denominator;
            result._Denominator = l._Denominator * r._Denominator;
            return result;
        }
    }
}
