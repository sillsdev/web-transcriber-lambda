using System.Text.RegularExpressions;

namespace SIL.Transcriber.Utility.Extensions
{
    public static class StringExtensions
    {
        private static readonly Regex _camelRegex = new ("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);
        public static T ToEnum<T>(this string value, T defaultValue)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : (T)Enum.Parse(typeof(T), value, true);
        }

        public static DateTime DateTimeFromISO8601(this string value)
        {
            return DateTime
                .Parse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal)
                .SetKindUtc();
        }
        public static string CameltoSnakeCase(this string value)
        {
            return string.IsNullOrEmpty(value) ? value : _camelRegex.Replace(value, "_$0").ToLowerInvariant();
        }
        public static string CameltoKebab(this string value)
        {
            return string.IsNullOrEmpty(value) ? value : _camelRegex.Replace(value, "-$0").ToLowerInvariant();
        }
    }
}

