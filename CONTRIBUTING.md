# Contribution Guidelines

First and most importantly, you should note that this project is a Discord Bot designed for use in the [Microsoft Community](https://msft.chat) Discord server, and there alone.

Therefore, the following contribution rules apply to PRs against this repository:
- Do not open a PR that adds major new features to the bot, without consulting first, either via an Issue in the repository or via the community Modmail. This does not apply to small tweaks or fixes, just major new features.
- Do not attempt to make the bot make use of sharding. This is clearly out of scope considering the bot only works on a single server.
- Do not attempt to significantly alter the bot in a way that results in the installation instructions on the README becoming invalid. 
- Do not bump a version of a dependency.
- Do not propose any significant code or building changes without testing them first.
- Do not alter the GitHub Action files (/.github/workflows) in ANY way. Open an Issue if there is a problem with them.
- Do not alter the Docker configuration (`Dockerfile` or `docker-compose.yml`) without consulting first. Open an Issue if there is a problem.
- Do not introduce code changes that alter the bot in the interest of making it work in multiple servers. This includes global slash commands or changing the permission system. At this time, the bot is not intended to support more than one server.

Secondly, this bot is a labour of love by [Erisa](https://github.com/Erisa), who does not expect it to be perfect or optimised in any way.  
Please be respectful when submitting changes that optimise the flow of operation, and do not try to complicate anything for the sake of "best practises".  
If we (developer(s)) cannot understand the concepts behind the code, it will be rejected on principle.  
We are not here to learn how to code perfectly or in the best way, we are here to assist in the moderation of our community.

And finally, this repository is NOT part of Hacktoberfest. Please do not make any contributions expecting points or rewards. We have the upmost respect for Hacktoberfest, but do not wish to take part.
