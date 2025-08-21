﻿# Cliptok - Opinionated moderation Discord bot

## About
This bot is a Discord moderation bot, it was originally designed for the [Microsoft Community](https://msft.chat/) but its use has since expanded to other servers.  

The bot s only designed to run in a single server at a time, and is difficult to adjust. A lot is assumed about the environment and you will encounter problems along the way.
While ome efforts have been made the bot not actively break other instances with different configs, modularity and portability is not the goal of this project. You are on your own if you attempt to host this bot for your own purposes. (Good luck though, I hope it works out!)

GitHub Issues will generally only be accepted if they are reproducible in a standard and supported environment, please do not report issues that arise as a result of misconfiguring the bot.

## Configuration

Copy `.env-example` to `.env` and edit in the token for your Discord bot. If you require a different prefix, that can be done in the same file.

To use the bot outside of it's intended environments, you will need to edit the configuration file more thoroughly, including all of the role IDs and the server ID. It is vital that every config value is present and valid.

## Limitations
Currently the bot will only work with one server. This choice was made because the bot was specifically created for a single server at a time and will never be made publicly available. If you are looking to host a bot for multiple servers, this bot is not for you.

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
2. Copy `.env-example` to `.env` and add the bot token.
3. Uncomment lines 21-23 in `docker-compose.yml` and edit the `config.json` to fit your needs.
4. Run the bot in the background: `docker-compose up -d`

That's it! If you ever need to see the logs, `cd` back into the directory and run `docker-compose logs`.

If you want to make a backup of the bot's data, that will be inside the `data` folder, though may be owned by root due to Docker limitations. It's up to the user hosting the bot to maintain their own backups (Or lack thereof).  
The author(s) of the bot accept(s) no responsibility for lost data due to negligence.

To update the bot in the future, `git pull` the repository and then pull and restart the containers:
- `git pull && docker-compose pull && docker-compose up -d`

### Setup - Standalone
If you want to run the bot as a standalone application rather than a Docker container, you'll need to install the dependencies manually:
- If running on Windows, Windows 10 or higher is required.
- .NET SDK 8.0, instructions can be found on the [Microsoft website](https://dotnet.microsoft.com/download).
- Redis server, on Debian or Ubuntu-based Linux distributions this should be `sudo apt-get install redis-server`
    - It may be in your best interests to configure Redis to use AOF persistence. This will dramatically decrease the risk of losing data due to power failure or similar issues. You can find more information about that on the [Redis website](https://redis.io/topics/persistence).
    - If running on Windows, [tporadowski/redis](https://github.com/tporadowski/redis) is preferred over WSL or other methods of running Redis.
        - Do **not** use `microsoftarchive/redis`.
        - If using WSL, you may need to run `sudo service redis-server start` or `redis-server` manually.

Once you have everything installed:
1. Clone this repository and `cd` into the directory.
2. Set the `CLIPTOK_TOKEN` environment variable to your bots token.
3. Edit the `config.json` to fit your needs.
4. Compile the bot for production: `dotnet build -c Release`.
5. Run the bot: `dotnet run -c Release`

If you go with this method you will have to fork the bot to the background yourself, through the use of a process manager like `pm2` or a systemd service.

### Setup - Development
If you want to develop and make changes to the bot, it's recommended to use the following:
- First read [our contribution guidelines](CONTRIBUTING.md) if you intend to submit changes back to the repository.
- You need Windows 10 or higher.
- You will need .NET SDK 9.0, instructions can be found on the [Microsoft website](https://dotnet.microsoft.com/download).
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/), with `.NET desktop development` selected on the installer. 
    - Visual Studio 2019 is untested and no longer preferred.
    - Make sure you are on the latest version.
- Redis. It is recommended to install [tporadowski/redis](https://github.com/tporadowski/redis).
    - Simply download the latest .msi and run it. Adding the Windows Firewall exception is not required.
- [Git for Windows](https://gitforwindows.org/)
- A Discord server for testing, with all the roles and channels required for bot functionality.

Alternate setups with Linux/macOS and IDEs like Rider/Visual Studio Code are possible but instructions will not be provided here.

Once you have everything installed:
1. Create a new Discord application and bot with all intents enabled, set `CLIPTOK_TOKEN` Windows environment variable to the bots token.
2. Clone the repository (or your fork of it) to a folder.
3. Open the `Cliptok.sln` within, making sure to use Visual Studio 2022.
4. Copy `config.json` to `config.dev.json` and make changes for your testing server.
    - This is the most difficult part by far. Please try to replicate the required roles/channels/etc as closely as possible.
5. Edit, run, debug, etc.

If you have a change to make that follows the contribution guidelines, send a Pull Request.

## Credits

### 🖥️ Developers
- [Erisa](https://github.com/Erisa)
- [FloatingMilkshake](https://github.com/FloatingMilkshake)

### ⚙️ Code contributors
- [TorchGM](https://github.com/TorchGM)

### 💗 Significant sponsors
- [FloatingMilkshake](https://github.com/FloatingMilkshake)
- [TorchGM](https://github.com/TorchGM)

### 🙏Special thanks
- [TorchGM](https://github.com/TorchGM) for initial testing and providing core design feedback. Seriously, thank you Torch.
- [PrincessRavy](https://github.com/PrincessRavy) for providing an API previously used by Cliptok.
- All of my [GitHub Sponsors](https://github.com/sponsors/Erisa) 💝
- The developers of [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus), without their library none of this would  be possible.
- The excellent moderation team over at [Microsoft Community](https://msft.chat/), and all of its wonderful members.
