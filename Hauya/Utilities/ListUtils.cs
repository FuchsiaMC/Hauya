using System;
using System.Collections.Generic;

namespace Hauya.Utilities
{
    public static class ListUtils
    {
        public static IEnumerable<List<T>> Split<T>(this List<T> bigList, int nSize)
        {
            for (int i = 0; i < bigList.Count; i += nSize)
            {
                yield return bigList.GetRange(i, Math.Min(nSize, bigList.Count - i));
            }
        }
    }
}