# Microsoft (Discord) Bot

## About
This bot is a simple(?) Discord moderation bot specifically designed for the [Microsoft Community](https://msft.chat/).  
The bot can likely be used outside of that server (Provided the config file is edited), however that is not its main goal and you are on your own if you attempt that. (Good luck though, I hope it works out!)

## Configuration
If you're using the bot on the Microsoft Community Discord server, the configuration should be fairly simple since the default configuration values are mostly filled in for you.  

Simply copy `config.example.json` to `config.json` and edit in the token for your Discord bot. If you require a different prefix, that can be done in the same file.

If you're using the bot elsewhere, you will need to edit the configuration file more thoroughly, including all of the role IDs and the server ID.

## Usage
There are two methods of launching this bot. Through [Docker](https://www.docker.com/) or as a standalone application.

If you are not familiar with deploying .NET Core and Redis applications, it is recommended to use the Docker method as dependencies are automatically handled for you without polluting your main system, and the setup can be handled with a few simple commands.-

### Setup - Docker
First you'll want to install Docker. On a Debian or Ubuntu-based Linux distribution this should be as simple as `sudo apt-get install docker docker-compose`.

Then:
1. Clone this repository and `cd` into the directory.
2. Copy `config.example.json` to `config.json` and edit any needed values (e.g. token).
3. Run the bot in the background: `docker-compose up -d`

That's it! If you ever need to see the logs, `cd` back into the directory and run `docker-compose logs`.

If you want to make a backup of the bot's data, that will be inside the `data` folder, though may be owned by root due to Docker limitations. It's up to the user hosting the bot to maintain their own backups (Or lack thereof).  
The author(s) of the bot accept(s) no responsiblity for lost data due to negligence.

### Setup - Standalone
If you want to run the bot as a standalone application rather than a Docker container, you'll need to install the dependencies manually:
- .NET Core SDK 3.1, instructions can be found on the [Microsoft website](https://dotnet.microsoft.com/download?initial-os=linux).
- Redis server, on Debian or Ubuntu-based Linux distributions this should be `sudo apt-get install redis-server`
    - It may be in your best interests to configure Redis to use AOF persistence. This will dramatically decrease the risk of losing data due to power failure or similar issues. You can find more information about that on the [Redis website](https://redis.io/topics/persistence).

Once you have everything installed:
1. Clone this repository and `cd` into the directory.
2. Copy `config.example.json` to `config.json` and edit any needed values (e.g. token).`
3. Compile the bot for production: `dotnet build -c Release`.
3. Run the bot: `dotnet run -c Release`

If you go this method you will have to fork the bot to the background yourself, through the use of a process manager like `pm2` or a systemd service.

## Limitations
Currently the bot will only work with one server. This choice was made because the bot was specifically created for a single server and will never be made publicly available. If you are looking to host a bot for multiple servers, this bot is not for you.

A lot of the configuration (Role IDs, emoji IDs, etc.) are in the `config.json` file and cannot be edited at runtime. This means the bot will have to be relaunched for changes to those settings to take effect. This may be improved in the future, however it is not a high priority.

## Planned features
- Automatic mutes based on warning thresholds.
- Automatic unmutes after specific time.
- Automatic warning based on filters (e.g. swear words).
- General purpose commands: mute, ban, kick, etc.
- User selfrole assignment (Windows Insider roles?).
- Possibly a starboard? Not high priority.
- ??? (Open an Issue!)

## Credits

### Developers
- [Erisa](https://github.com/Erisa)

### Special thanks
- [TorchGM](https://github.com/TorchGM) for testing and providing design feedback.
- The developers of [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus), without their library none of this would  be possible.
- The excellent Moderation team over at [Microsoft Community](https://msft.chat/), and all of its wonderful members.