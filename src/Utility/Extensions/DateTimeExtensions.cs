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
            if (dateTime.HasValue)
            {
                return dateTime.Value.SetKindUtc();
            }
            else
            {
                return null;
            }
        }
        public static DateTime SetKindUtc(this DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            { return dateTime; }
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
    }
}

