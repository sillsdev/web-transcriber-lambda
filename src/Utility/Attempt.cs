namespace SIL.Transcriber.Utility
{
    public static class Attempt
    {
        public static Attempt<T?> Success<T>(T? result)
        {
            return new Attempt<T?>(true, result);
        }

        public static Attempt<T?> Failure<T>(T? result)
        {
            return new Attempt<T?>(false, result);
        }

        public static Attempt<T?> Failure<T>(T? result, string err)
        {
            return new Attempt<T?>(false, result, err);
        }
    }

    public struct Attempt<T>(bool success, T? result = default, string err = "")
    {
        public static Attempt<T> Failure { get; } = new Attempt<T>();

        public Attempt(T result)
            : this(true, result)
        {
        }

        public T? Result { get; } = result;
        public bool Success { get; } = success;
        public string Err { get; } = err;

        public bool TryResult(out T? result)
        {
            result = Result;
            return Success;
        }
    }
}
