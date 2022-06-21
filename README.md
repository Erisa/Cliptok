# Cliptok - A Discord bot for Microsoft Community

## About
This bot is a Discord moderation bot specifically designed for the [Microsoft Community](https://msft.chat/).  

The bot has not been designed for use outside of that server. A lot is assumed about the environment and you **will** encounter problems along the way.
Modularity is not the goal of this project. You are on your own if you attempt to host this bot outside of the intended environment. (Good luck though, I hope it works out!)

If you want my help setting up an instance of this bot and don't know me personally, you are welcome to [Sponsor me](https://github.com/sponsors/Erisa) for at least $5 and then you are welcome to reach out and I will try my best.  
Still though, I recommend you find a better bot.

GitHub Issues will only be accepted if they are reproducible on the production bot (Cliptok in Microsoft Community).

## Note about scam domains
Cliptok no longer includes scam domains in its public source code. If you are using Cliptok or its lists to assist with your moderation, **don't**.  
There are many projects better suited to this, and Cliptok now uses new methods of detecting phishing links that we do not wish to make public.

Scam message parts continue to be available at [Lists/scams.txt](Lists/scams.txt) and you are free to contribute to those. Do not contribute scam domains, we don't need them.

If you have questions about this, reach out to `Moderators' mail` on https://discord.gg/microsoft and ask for Erisa, citing this README as a source. Do not Direct Message me unless we are friends or you are sponsoring me on GitHub.

## Configuration
If you're using the bot on the Microsoft Community Discord server, the configuration should be fairly simple since the default configuration values are filled in for you.  

Simply copy `.env-example` to `.env` and edit in the token for your Discord bot. If you require a different prefix, that can be done in the same file.

If you're using the bot elsewhere, you will need to edit the configuration file more thoroughly, including all of the role IDs and the server ID. It is vital that every config value is present and valid.

## Limitations
Currently the bot will only work with one server. This choice was made because the bot was specifically created for a single server and will never be made publicly available. If you are looking to host a bot for multiple servers, this bot is not for you.

A lot of the configuration (Role IDs, emoji IDs, etc.) are in the `config.json` file and cannot be edited at runtime. This means the bot will have to be relaunched for changes to those settings to take effect. This may be improved in the future, however it is not a high priority.

## Usage
There are three methods of launching this bot:
- (Recommended) Through [Docker](https://www.docker.com/).
- As a standalone application.
- From Visual Studio, aka development/debug mode.

If you are not familiar with deploying .NET (Core) and Redis applications, it is recommended to use the Docker method as dependencies are automatically handled for you without polluting your main system, and the setup can be handled with a few simple commands.

### Setup - Docker
First you'll want to install Docker. On a Debian or Ubuntu-based Linux distribution this should be as simple as `sudo apt-get install docker.io docker-compose`.

Then:
1. Clone this repository and `cd` into the directory.
2. Copy `.env` to `.env-example` and add the bot token.
3. If you're not deploying for Microsoft Community, uncomment lines 21-23 in `docker-compose.yml` and edit the `config.json` to fit your needs.
4. Run the bot in the background: `docker-compose up -d`

That's it! If you ever need to see the logs, `cd` back into the directory and run `docker-compose logs`.

If you want to make a backup of the bot's data, that will be inside the `data` folder, though may be owned by root due to Docker limitations. It's up to the user hosting the bot to maintain their own backups (Or lack thereof).  
The author(s) of the bot accept(s) no responsibility for lost data due to negligence.

To update the bot in the future, `git pull` the repository and then pull and restart the containers:
- `git pull && docker-compose pull && docker-compose up -d`

### Setup - Standalone
If you want to run the bot as a standalone application rather than a Docker container, you'll need to install the dependencies manually:
- If running on Windows, Windows 10 or higher is required.
- .NET SDK 6.0, instructions can be found on the [Microsoft website](https://dotnet.microsoft.com/download).
- Redis server, on Debian or Ubuntu-based Linux distributions this should be `sudo apt-get install redis-server`
    - It may be in your best interests to configure Redis to use AOF persistence. This will dramatically decrease the risk of losing data due to power failure or similar issues. You can find more information about that on the [Redis website](https://redis.io/topics/persistence).
    - If running on Windows, [tporadowski/redis](https://github.com/tporadowski/redis) is preferred over WSL or other methods of running Redis.
        - Do **not** use `microsoftarchive/redis`.
        - If using WSL, you may need to `sudo service redis-server start` or `redis-server` manually.

Once you have everything installed:
1. Clone this repository and `cd` into the directory.
2. Set the `CLIPTOK_TOKEN` environment variable to your bots token.
3. If you're not deploying for Microsoft Community, edit the `config.json` to fit your needs.
4. Compile the bot for production: `dotnet build -c Release`.
5. Run the bot: `dotnet run -c Release`

If you go with this method you will have to fork the bot to the background yourself, through the use of a process manager like `pm2` or a systemd service.

### Setup - Development
If you want to develop and make changes to the bot, you need the following:
- First read [our contribution guidelines](CONTRIBUTING.md) if you intend to submit changes back to the repository.
- You need Windows 10 or higher. Windows 8.1 or lower will not work anymore.
- You will need .NET SDK 6.0, instructions can be found on the [Microsoft website](https://dotnet.microsoft.com/download).
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/), with `.NET desktop development` selected on the installer. 
    - Visual Studio 2019 is untested and no longer preferred.
- Redis. It is recommended to install [tporadowski/redis](https://github.com/tporadowski/redis).
    - Simply download the latest .msi and run it. Adding the Windows Firewall exception is not required.
- [Git for Windows](https://gitforwindows.org/)
- A Discord server for testing, with all the roles and channels required for bot functionality.

Once you have everything installed:
1. Create a new Discord application and bot with all intents enabled, set `CLIPTOK_TOKEN` Windows environment variable to the bots token.
2. Clone the repository (or your fork of it) to a folder.
3. Open the `Cliptok.sln` within, making sure to use Visual Studio 2022.
4. Copy `config.json` to `config.dev.json` and make changes for your testing sever.
    - This is the most difficult part by far. Please try to replicate the required roles/channels/etc as closely as possible.
5. Edit, run, debug, etc.

If you have a change to make that follows the contribution guidelines, send a Pull Request like any other project. I assume you are a developer if you got this far, so no specific instructions for sending Pull Requests will be given.

## Credits

### 🖥️ Developer(s)
- [Erisa](https://github.com/Erisa)

### ⚙️ Code contributor(s)
- [TorchGM](https://github.com/TorchGM)

### 💗 Significant sponsor(s) 
- [FloatingMilkshake](https://github.com/FloatingMilkshake)

### 🙏Special thanks
- [TorchGM](https://github.com/TorchGM) for testing and providing design feedback. Seriously, thank you Torch.
- [PrincessRavy](https://github.com/PrincessRavy) for providing [an API](https://docs.ravy.org/share/5bc92059-64ef-4d6d-816e-144b78e97d89) used by Cliptok.
- All of my [GitHub Sponsors](https://github.com/sponsors/Erisa) 💝
- The developers of [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus), without their library none of this would  be possible.
- The excellent moderation team over at [Microsoft Community](https://msft.chat/), and all of its wonderful members.
