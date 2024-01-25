using System;
using System.Collections;
using System.Collections.Generic;
using TMPEffects.Tags;
using UnityEngine;

namespace TMPEffects.Extensions
{
    public static class Extensions
    {
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T comp = gameObject.GetComponent<T>();
            if (comp == null) comp = gameObject.AddComponent<T>();
            return comp;
        }

        public static void AddRange<T>(this ICollection<T> dst, IEnumerable<T> src)
        {
            List<T> tags = dst as List<T>;
            if (tags == null)
            {
                foreach (var tag in src)
                {
                    dst.Add(tag);
                }
            }
            else tags.AddRange(src);
        }

        public static int BinarySearchIndexOf<T>(this IList<T> list, T value, IComparer<T> comparer = null)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            comparer = comparer ?? Comparer<T>.Default;

            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = comparer.Compare(value, list[middle]);
                if (comparisonResult == 0)
                    return middle;
                else if (comparisonResult < 0)
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            return ~lower;
        }
    }
}