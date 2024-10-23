using System.Text.RegularExpressions;

namespace SIL.Transcriber.Utility.Extensions
{
    public static class DateTimeExtensions
    {

        public static string ToISO8601(this DateTime value)
        {
            string dt = value.ToString("yyyy-MM-ddTHH:mm:ssK");
            // remove timezone offset if it's UTC
            string result = Regex.Replace(dt, @"00:00$", "");

            return result;
        }
        public static DateTime? SetKindUtc(this DateTime? dateTime)
        {
            return dateTime.HasValue ? dateTime.Value.SetKindUtc() : null;
        }
        public static DateTime SetKindUtc(this DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
    }
}

