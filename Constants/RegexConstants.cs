namespace Cliptok.Constants
{
    public class RegexConstants
    {
        readonly public static Regex emoji_rx = new("((\u203c|\u2049|\u2139|[\u2194-\u2199]|[\u21a9-\u21aa]|[\u231a-\u231b]|\u23cf|[\u23e9-\u23f3]|[\u23f8-\u23fa]|\u24c2|[\u25aa-\u25ab]|\u25b6|\u25c0|[\u25fb-\u25fe]|[\u2600-\u2604]|\u260E|\u2611|[\u2614-\u2615]|\u2618|\u261D|\u2620|[\u2622-\u2623]|\u2626|\u262A|[\u262E-\u262F]|[\u2638-\u263A]|\u2640|\u2642|[\u2648-\u2653]|[\u265F-\u2660]|\u2663|[\u2665-\u2666]|\u2668|\u267B|[\u267E-\u267F]|[\u2692-\u2697]|\u2699|[\u269B-\u269C]|[\u26A0-\u26A1]|\u26A7|[\u26AA-\u26AB]|[\u26B0-\u26B1]|[\u26BD-\u26BE]|[\u26C4-\u26C5]|\u26C8|[\u26CE-\u26CF]|\u26D1|[\u26D3-\u26D4]|[\u26E9-\u26EA]|[\u26F0-\u26F5]|[\u26F7-\u26FA]|\u26FD|\u2702|\u2705|[\u2708-\u270D]|\u270F|\u2712|\u2714|\u2716|\u271D|\u2721|\u2728|[\u2733-\u2734]|\u2744|\u2747|\u274C|\u274E|[\u2753-\u2755]|\u2757|[\u2763-\u2764]|[\u2795-\u2797]|\u27A1|\u27B0|\u27BF|[\u2934-\u2935]|[\u2B05-\u2B07]|[\u2B1B-\u2B1C]|\u2B50|\u2B55|\u3030|\u303D|\u3297|\u3299|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]))|(<(?:a|sound)?:[a-zA-Z0-9_.]{1,32}:[0-9]+>)");
        readonly public static Regex modmaiL_rx = new("User ID: ([0-9]+)");
        readonly public static Regex invite_rx = new("(?:discord|discordapp)\\.(?:gg|com\\/invite)\\/([\\w+-]+)");
        readonly public static Regex domain_rx = new("(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z0-9][a-z0-9-]{0,61}[a-z0-9]");
        readonly public static Regex bold_rx = new("\\*\\*(.*?)\\*\\*");
        readonly public static Regex discord_link_rx = new(@".*discord(?:app)?.com\/channels\/((?:@)?[a-z0-9]*)\/([0-9]*)(?:\/)?([0-9]*)");
        readonly public static Regex channel_rx = new("<#([0-9]+)>");
        readonly public static Regex user_rx = new("<@!?([0-9]+)>");
        readonly public static Regex role_rx = new("<@&([0-9]+)>");
        readonly public static Regex warn_msg_rx = new($"{Program.cfgjson.Emoji.Warning} <@!?[0-9]+> was warned: \\*\\*(.+)\\*\\*");
        readonly public static Regex auto_warn_msg_rx = new($"{Program.cfgjson.Emoji.Denied} <@!?[0-9]+> was automatically warned: \\*\\*(.+)\\*\\*");
        readonly public static Regex mute_msg_rx = new($"{Program.cfgjson.Emoji.Muted} <@!?[0-9]+> has been muted");
        readonly public static Regex unmute_msg_rx = new($"{Program.cfgjson.Emoji.Information} Successfully unmuted");
        readonly public static Regex ban_msg_rx = new($"{Program.cfgjson.Emoji.Banned} <@!?[0-9]+> has been banned");
        readonly public static Regex unban_msg_rx = new($"{Program.cfgjson.Emoji.Unbanned} Successfully unbanned");
        readonly public static Regex url_rx = new("https?:\\/\\/(?:www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b(?:[-a-zA-Z0-9()@:%_\\+.~#?&\\/=]*)");
        readonly public static Regex webhook_rx = new("(?:https?:\\/\\/)?discord(?:app)?.com\\/api\\/(?:v\\d\\/)?webhooks\\/(?<id>\\d+)\\/(?<token>[A-Za-z0-9_\\-]+)", RegexOptions.ECMAScript);
        readonly public static Regex id_rx = new("[0-9]{17,}");
    }
}
