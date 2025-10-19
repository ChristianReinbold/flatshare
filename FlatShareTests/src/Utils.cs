using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace de.creinbold.FlatShare.Tests
{
    static class Utils
    {
        public static void AssertNumericAlmostEqual(decimal n1, decimal n2)
        {
            decimal eps = 1e-06m;
            Assert.IsTrue((n1 - n2) < eps, "Numeric values not almost equal ({0} and {1})", n1, n2);
            Assert.IsTrue((n2 - n1) < eps, "Numeric values not almost equal ({0} and {1})", n1, n2);
        }

        public static void AssertDate(string expected, DateTime actual)
        {
            Assert.AreEqual(DateUtils.DateFromString(expected).Date, actual.Date);
        }
    }
}
