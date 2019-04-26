using System;
using System.Collections.Generic;
using System.Linq;

namespace SIL.Transcriber.Utility
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T> list)
        {
            return list ?? Enumerable.Empty<T>();
        }
    }
}
