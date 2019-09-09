using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Utility
{
    public static class Attempt
    {
        public static Attempt<T> Success<T>(T result)
        {
            return new Attempt<T>(true, result);
        }

        public static Attempt<T> Failure<T>(T result)
        {
            return new Attempt<T>(false, result);
        }

        public static Attempt<T> Failure<T>(T result, string err)
        {
            return new Attempt<T>(false, result, err);
        }
    }

    public struct Attempt<T>
    {
        public static Attempt<T> Failure { get; } = new Attempt<T>();

        public Attempt(T result)
            : this(true, result)
        {
        }

        public Attempt(bool success, T result = default(T), string err = "")
        {
            Success = success;
            Result = result;
            Err = err;
        }

        public T Result { get; }
        public bool Success { get; }
        public string Err { get; }

        public bool TryResult(out T result)
        {
            result = Result;
            return Success;
        }
    }
}
