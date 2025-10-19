using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace de.creinbold.FlatShare
{
    public static class Extensions
    {
        public static string CapitalizeFirstLetter(this string s)
        {
            if (String.IsNullOrEmpty(s)) return s;
            return s.Substring(0, 1).ToUpper() + s.Substring(1);
        }

        public static string SplitCamelCase(this string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }
        public static string RemoveCharacters(this string s, params char[] unwantedCharacters)
        {
            if (s == null) return null;
            return string.Join("", s.Split(unwantedCharacters));
        }

        public static bool CountLowerEqual<T>(this IEnumerable<T> i, int value)
        {
            return i.Skip(value).IsEmpty();
        }

        public static IEnumerable<T> ToEnumerable<T>(this T obj)
        {
            yield return obj;
        }

        public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
        {
            return !enumerable.GetEnumerator().MoveNext();
        }

        public static void ElementwiseInvoke<T>(this IEnumerable<T> enumerable, Action<T> fn)
        {
            foreach (var elem in enumerable) fn(elem);
        }

        public static TValue GetValueOrDefault<TKey, TValue>
            (this IDictionary<TKey, TValue> dictionary,
             TKey key,
             TValue defaultValue = default(TValue))
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }

        public static TValue DefaultCreate<TKey, TValue>
            (this IDictionary<TKey, TValue> dictionary,
             TKey key, Func<TValue> factory)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = factory();
            }
            return dictionary[key];
        }

        public static bool SkipWhile<T>(this IEnumerator<T> iter, Func<T, bool> pred)
        {
            while (pred(iter.Current))
            {
                if (!iter.MoveNext()) return false;
            }
            return true;
        }

        public static IEnumerable<T> TakeWhile<T>(this IEnumerator<T> iter, Func<T, bool> pred)
        {
            while (pred(iter.Current))
            {
                yield return iter.Current;
                if (!iter.MoveNext()) break;
            }
        }

        public static IEnumerable<T> TakeAllButLast<T>(this IEnumerable<T> source)
        {
            var it = source.GetEnumerator();
            bool hasRemainingItems = false;
            bool isFirst = true;
            T item = default(T);

            do
            {
                hasRemainingItems = it.MoveNext();
                if (hasRemainingItems)
                {
                    if (!isFirst) yield return item;
                    item = it.Current;
                    isFirst = false;
                }
            } while (hasRemainingItems);
        }
    }
}
