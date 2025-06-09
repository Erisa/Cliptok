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
        
        public static DateTime ToDateTime(long unixTime, bool milliseconds = false)
        {
            /* This function creates a DateTime from a long thats a UNIX timestamp.
             It will parse both second and millisecond timestamps depending on the second argument (true = milliseconds, false = seconds).
             Works by creating a datetime object in 1970-1-1 00:00:00, and then just adding the timestamp, lol.
             This is then returned !IN LOCAL TIME!, **NOT** utc.
            */
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (milliseconds)
            {
                return epoch.AddMilliseconds(unixTime).ToLocalTime();
            }
            else
            {
                return epoch.AddSeconds(unixTime).ToLocalTime();
            }
        }

        public static DateTime ParseAnyDateFormat(string inputString)
        {
            /*
             This function parses any date format from a simple string.
             It can parse: UNIX timestamps using the functiona above (TimeHelpers.ToDateTime),
             or simple DateTimes (like 2025-06-10 02:00:00) or relative timestamps
             with HumanDateParser such as 5d, 2y, etc.
             Returns a datetime. Made for the remindme command but can of course be used elsewhere too.
             */
            DateTime t;

            // Define REGEX patterns for checking if its a UNIX millisecond or second timestamp
            string patternSec = @"^\d{10}$";
            string patternMillisec = @"^\d{13}$";

            // check if its a second timestamp
            if (Regex.IsMatch(inputString, patternSec))
            {
                // parse the string into a long
                long ts = long.Parse(inputString);
                // use helper func to turn it into a datetime
                t = TimeHelpers.ToDateTime(ts);
            }
            else if (Regex.IsMatch(inputString, patternMillisec))
            {
                // parse the string into a long
                long ts = long.Parse(inputString);
                // use helper func to turn it into a datetime
                t = TimeHelpers.ToDateTime(ts, true);
            }
            // else try inside the condition of this elseif to parse it as a simple datetime (example: 2025-06-09 14:25:36)
            else if (!DateTime.TryParse(inputString, out t))
            {
                // if it couldnt parse it as that, finally try humandateparser
                t = HumanDateParser.HumanDateParser.Parse(inputString);
            }

            return t;
        }
    }
}
