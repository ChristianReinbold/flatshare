using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public class EnumUtils
    {
        public static IEnumerable<T> AllValues<T>(T enumEntry)
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("Can only iterate values for Enum types.");
            return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }
}
