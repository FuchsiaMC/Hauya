using System;
using System.Collections.Generic;
using System.Linq;

namespace Hauya.Utilities
{
    public static class ListUtils
    {
        public static IEnumerable<List<T>> SplitByChunkSize<T>(this List<T> bigList, int nSize)
        {
            for (int i = 0; i < bigList.Count; i += nSize)
            {
                yield return bigList.GetRange(i, Math.Min(nSize, bigList.Count - i));
            }
        }
        
        public static IEnumerable<IEnumerable<T>> SplitByChunks<T>(this IEnumerable<T> list, int parts)
        {
            int i = 0;
            IEnumerable<IEnumerable<T>> splits = from item in list
                group item by i++ % parts into part
                select part.AsEnumerable();
            return splits;
        }
        
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.Shuffle(new Random());
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            return source.ShuffleIterator(rng);
        }

        private static IEnumerable<T> ShuffleIterator<T>(
            this IEnumerable<T> source, Random rng)
        {
            List<T> buffer = source.ToList();
            for (int i = 0; i < buffer.Count; i++)
            {
                int j = rng.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }
    }
}