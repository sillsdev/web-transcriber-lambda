using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace SIL.Transcriber.Utility
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T> list)
        {
            return list ?? Enumerable.Empty<T>();
        }
        [Pure]
        public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? source)
        {
            if (source == null)
            {
                return true;
            }

            return !source.Any();
        }

        public static void AddRange<T>(this ICollection<T> source, IEnumerable<T> itemsToAdd)
        {
            foreach (T item in itemsToAdd)
            {
                source.Add(item);
            }
        }
    }
}
