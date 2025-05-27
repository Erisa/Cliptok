namespace Cliptok.Helpers
{
    public static class BaseContextExtensions
    {
        public static async Task PrepareResponseAsync(this CommandContext ctx)
        {
            await ctx.DeferResponseAsync();
        }

        public static async Task RespondAsync(this CommandContext ctx, string text = null, DiscordEmbed embed = null, bool ephemeral = false, bool mentions = true)
        {
            DiscordInteractionResponseBuilder response = new();

            if (text is not null) response.WithContent(text);
            if (embed is not null) response.AddEmbed(embed);

            response.AsEphemeral(ephemeral);
            response.AddMentions(mentions ? Mentions.All : Mentions.None);

            await ctx.RespondAsync(response);
        }

        public static async Task EditAsync(this CommandContext ctx, string text = null, DiscordEmbed embed = null)
        {
            DiscordWebhookBuilder response = new();

            if (text is not null) response.WithContent(text);
            if (embed is not null) response.AddEmbed(embed);

            await ctx.EditResponseAsync(response);
        }

        public static async Task FollowAsync(this CommandContext ctx, string text = null, DiscordEmbed embed = null, bool ephemeral = false)
        {
            DiscordFollowupMessageBuilder response = new();

            response.AddMentions(Mentions.All);

            if (text is not null) response.WithContent(text);
            if (embed is not null) response.AddEmbed(embed);

            response.AsEphemeral(ephemeral);

            await ctx.FollowupAsync(response);
        }
    }
}
