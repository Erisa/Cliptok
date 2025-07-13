namespace Cliptok.Helpers
{
    public class StringHelpers
    {
        // https://stackoverflow.com/a/2776689
        public static string Truncate(string value, int maxLength, bool elipsis = false)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var strOut = value.Length <= maxLength ? value : value[..maxLength];
            if (elipsis && value.Length > maxLength)
                return strOut + '…';
            else
                return strOut;
        }

        public static string Pad(long id)
        {
            if (id < 0)
                return id.ToString("D4");
            else
                return id.ToString("D5");
        }

        public static string WarningContextString(DiscordUser user, string reason, bool automatic = false)
        {
            if (automatic)
                return $"{Program.cfgjson.Emoji.Denied} {user.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
            else
                return $"{Program.cfgjson.Emoji.Warning} {user.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
        }

        public static async Task<string> CodeOrHasteBinAsync(string input, string language = "", int charLimit = 1930, bool plain = false, bool noCode = false, bool messageWrapper = false, string overflowHeader = "")
        {
            bool inputHasCodeBlock = input.Contains("```");
            if (input.Length > charLimit || inputHasCodeBlock)
            {
                if (overflowHeader != "")
                {
                    input = overflowHeader + input;
                }

                HasteBinResult hasteResult = await Program.hasteUploader.PostAsync(input, language);
                if (hasteResult.IsSuccess)
                {
                    var hasteUrl = hasteResult.FullUrl;

                    if (plain)
                        return hasteUrl;                    
                    if (messageWrapper)
                        return $"[`📄 View online`]({hasteResult.RawUrl})";
                    else if (inputHasCodeBlock)
                        return $"{Program.cfgjson.Emoji.Warning} Output contained a code block, so it was uploaded to Hastebin to avoid formatting issues: {hasteUrl}";
                    else
                        return $"{Program.cfgjson.Emoji.Warning} Output exceeded character limit: {hasteUrl}";
                }
                else
                {
                    Program.discord.Logger.LogError("Error ocurred uploading to Hastebin with status code: {code}\nPayload: {output}", hasteResult.StatusCode, input);
                    if (plain)
                        return "Error, check logs.";

                    return $"{Program.cfgjson.Emoji.Error} Unknown error occurred during upload to Hastebin.\nPlease try again or contact the bot owner.";
                }
            }
            else if (noCode)
            {
                return input;
            }
            else {
                return $"```{language}\n{input}\n```";
            }
        }
    }
}
