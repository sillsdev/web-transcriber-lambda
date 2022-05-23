using System;


namespace SIL.Transcriber.Utility.Extensions
{
    public static class StringExtensions
    {

        public static T ToEnum<T>(this string value, T defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return (T) Enum.Parse(typeof(T), value, true);
        }

        public static DateTime DateTimeFromISO8601(this string value)
        {
            return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        }
    }
}

