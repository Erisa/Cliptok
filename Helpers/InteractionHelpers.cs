namespace Cliptok.Helpers
{
    public static class BaseContextExtensions
    {
        public static async Task PrepareResponseAsync(this CommandContext ctx)
        {
            await ctx.DeferResponseAsync();
        }

        public static async Task RespondAsync(this CommandContext ctx, string text = null, DiscordEmbed embed = null, bool ephemeral = false, bool mentions = true, params DiscordComponent[] components)
        {
            DiscordInteractionResponseBuilder response = new();

            if (text is not null) response.WithContent(text);
            if (embed is not null) response.AddEmbed(embed);
            if (components.Length != 0) response.AddComponents(components);

            response.AsEphemeral(ephemeral);
            response.AddMentions(mentions ? Mentions.All : Mentions.None);

            await ctx.RespondAsync(response);
        }

        public static async Task EditAsync(this CommandContext ctx, string text = null, DiscordEmbed embed = null, params DiscordComponent[] components)
        {
            DiscordWebhookBuilder response = new();

            if (text is not null) response.WithContent(text);
            if (embed is not null) response.AddEmbed(embed);
            if (components.Length != 0) response.AddComponents(components);

            await ctx.EditResponseAsync(response);
        }

        public static async Task FollowAsync(this CommandContext ctx, string text = null, DiscordEmbed embed = null, bool ephemeral = false, params DiscordComponent[] components)
        {
            DiscordFollowupMessageBuilder response = new();

            response.AddMentions(Mentions.All);

            if (text is not null) response.WithContent(text);
            if (embed is not null) response.AddEmbed(embed);
            if (components.Length != 0) response.AddComponents(components);

            response.AsEphemeral(ephemeral);

            await ctx.FollowupAsync(response);
        }
    }
}
