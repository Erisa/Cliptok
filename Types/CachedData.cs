namespace Cliptok.Types
{
    public class RecentMessageInfo
    {
        public string Content { get; set; }
        public List<string> AttachmentNames { get; set; }
        public List<MockDiscordMessage> Messages { get; set; }
        public DateTime LastMessageTime { get; set; }
    }
    public enum Level { Information, Warning, Error, Debug, Verbose }

}
