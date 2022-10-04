namespace Cliptok.Helpers
{
    public class TimeHelpers
    {
        public static string TimeToPrettyFormat(TimeSpan span, bool ago = true)
        {

            if (span == TimeSpan.Zero) return "0 seconds";

            if (span.Days > 3649)
                return "a long time";

            var sb = new StringBuilder();
            if (span.Days > 365)
            {
                int years = span.Days / 365;
                sb.AppendFormat("{0} year{1}", years, years > 1 ? "s" : String.Empty);
                int remDays = span.Days - (365 * years);
                int months = remDays / 30;
                if (months > 0)
                    sb.AppendFormat(", {0} month{1}", months, months > 1 ? "s" : String.Empty);
            }
            else if (span.Days > 0)
                sb.AppendFormat("{0} day{1}", span.Days, span.Days > 1 ? "s" : String.Empty);
            else if (span.Hours > 0)
                sb.AppendFormat("{0} hour{1}", span.Hours, span.Hours > 1 ? "s" : String.Empty);
            else if (span.Minutes > 0)
                sb.AppendFormat("{0} minute{1}", span.Minutes, span.Minutes > 1 ? "s" : String.Empty);
            else
                sb.AppendFormat("{0} second{1}", span.Seconds, (span.Seconds > 1 || span.Seconds == 0) ? "s" : String.Empty);

            string output = sb.ToString();
            if (ago)
                output += " ago";
            return output;
        }
        public static long ToUnixTimestamp(DateTime? dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }
    }
}
