namespace Cliptok.Commands
{
    public class PunishmentHelpers
    {
        public static (TimeSpan duration, string reason, bool appealable) UnpackTimeAndReason(string timeAndReason, DateTime anchorTime)
        {
            TimeSpan duration = default;
            bool appealable = false;
            bool timeParsed = false;

            string possibleTime = timeAndReason.Split(' ').First();
            try
            {
                duration = HumanDateParser.HumanDateParser.Parse(possibleTime).ToUniversalTime().Subtract(anchorTime);
                timeParsed = true;
            }
            catch
            {
                // keep default
            }

            string reason = timeAndReason;

            if (timeParsed)
            {
                int i = reason.IndexOf(" ") + 1;
                reason = reason[i..];
            }

            if (timeParsed && possibleTime == reason)
                reason = "No reason specified.";

            if (reason.Length > 6 && reason[..7].ToLower() == "appeal ")
            {
                appealable = true;
                reason = reason[7..^0];
            }

            return (duration, reason, appealable);
        }
    }
}