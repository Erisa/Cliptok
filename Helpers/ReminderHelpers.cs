namespace Cliptok.Helpers
{
    public class ReminderHelpers
    {
        /* Both of these functions create a DateTime from a long thats a UNIX timestamp.
         The first function is intended to parse second timestamps, and the second one is for millisecond timestamps.
         Works by creating a datetime object in 1970-1-1 00:00:00, and then just adding the timestamp, lol.
         This is then returned !IN LOCAL TIME!, **NOT** utc.
         */
        public static DateTime UnixTimeStampToDateTime(long unixTime)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime).ToLocalTime();
        }
        public static DateTime UnixTimeStampMsToDateTime(long unixTimeMs)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddMilliseconds(unixTimeMs).ToLocalTime();
        }
    }
}