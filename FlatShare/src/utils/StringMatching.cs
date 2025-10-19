using System;
using System.Collections.Generic;
using System.Linq;

namespace de.creinbold.FlatShare
{
    public static class StringMatching
    {
        private static int[,] InitMatchingTable(string s, string t)
        {
            int[,] table = new int[s.Length + 1, t.Length + 1];
            for (int i = 0; i < table.GetLength(0); i++) { table[i, 0] = i; }
            for (int j = 0; j < table.GetLength(1); j++) { table[0, j] = j; }
            return table;
        }

        private static void PopulateMatchingTableWithDamerauLevenshtein(string s, string t, int[,] table)
        {
            for (int i = 1; i < table.GetLength(0); i++)
            {
                for (int j = 1; j < table.GetLength(1); j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    int insertion = table[i, j - 1] + 1;
                    int deletion = table[i - 1, j] + 1;
                    int substitution = table[i - 1, j - 1] + cost;

                    int distance = Math.Min(insertion, Math.Min(deletion, substitution));

                    if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                    {
                        distance = Math.Min(distance, table[i - 2, j - 2] + cost);
                    }
                    table[i, j] = distance;
                }
            }
        }

        public static int DamerauLevenshteinDistance(string s, string t)
        {
            var table = InitMatchingTable(s, t);
            PopulateMatchingTableWithDamerauLevenshtein(s, t, table);
            return table[table.GetLength(0) - 1, table.GetLength(1) - 1];
        }

        public static int MinDamerauLevenshteinDistanceToSubstring(string pattern, string s)
        {
            if (pattern == s) return 0;

            var table = InitMatchingTable(pattern, s);

            for (int j = 0; j < table.GetLength(1); j++) { table[0, j] = 0; }
            PopulateMatchingTableWithDamerauLevenshtein(pattern, s, table);
            var min = table[table.GetLength(0) - 1, table.GetLength(1) - 1];
            for (var i = 0; i < table.GetLength(1); i++)
            {
                var val = table[table.GetLength(0) - 1, i];
                if (val < min) min = val;
            }

            // We add a fixed value of 0 in order to retain the property d(pattern, s) == 0 iff pattern == s.
            return min + 1;
        }

        // Extension methods

        public static int DamerauLevenshteinDistanceTo(this string s, string t)
        {
            return DamerauLevenshteinDistance(s, t);
        }

        public static int DamerauLevenshteinToSubstring(this string pattern, string s)
        {
            return MinDamerauLevenshteinDistanceToSubstring(pattern, s);
        }

        public static T GetMostLikelyMatch<T>(Func<string, string, int> distanceFn, Func<T, string> keyFn, string pattern, IEnumerable<T> objects, out int distance)
        {
            if (objects.IsEmpty())
            {
                distance = int.MaxValue;
                return default(T);
            }

            var objectsWithDistance = objects.Select(obj => Tuple.Create(obj, pattern.DamerauLevenshteinToSubstring(keyFn(obj))));
            var bestMatch = objectsWithDistance.OrderBy(t => t.Item2).First();
            distance = bestMatch.Item2;
            return bestMatch.Item1;
        }
    }
}
