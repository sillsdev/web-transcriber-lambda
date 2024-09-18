namespace SIL.Transcriber.Utility.Extensions
{
    public static class EnumExtensions
    {

        // https://stackoverflow.com/a/40358579/356849
        // https://stackoverflow.com/a/424380/356849
        public static string? AsString<T>(this T enumValue) where T : IConvertible
        {
            return !typeof(T).IsEnum ? throw new ArgumentException("T must be an enumerated type") : Enum.GetName(typeof(T), enumValue);
        }
    }
}

