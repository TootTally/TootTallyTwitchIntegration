# TootTally Twitch Integration Module

> Version: 1.0.5

Twitch Integration Module for [TootTally](https://toottally.com/).

First head to the TootTally Twitch setting page and follow the instructions.
Press F8 to toggle the request panel in game.

COMMAND LIST:
- `!ttr [id]`: Request a song with an id
- `!ttrhelp`: Display help message on how to request charts
- `!ttrprofile`: Send a your TootTally profile link to the chat (if logged in)
- `!ttrsong`: Sends what song you're currently playing
- `!ttrqueue`: Sends a list of what songs are in your current queue
- `!ttrlast`: Sends what your last played chart is
- `!ttrhistory`: Sends what charts you have played recently

INSTRUCTIONS:
1. Instructions for first time usage/setup:
2. Install the mod and open the game normally
3. Open TootTally Settings (the button to the left of the Play/Collect buttons)
4. Click Twitch
5. Under Twitch Integration, place your Twitch username on the first line and press Enter.
6. Click Authorize TootTally on Twitch. This will open your browser to https://toottally.com/twitch/
7. Follow the steps on the website, and authorize TootTally on Twitch.
8. Copy the Access Token given to you, and place it under your Twitch username on TootTally Twitch settings.
9. Click Connect/Disconnect Bot to start integration (or alternatively, hop straight into the song select screen). Your game may freeze for a bit when starting up the bot.
10. And you're done! Test the bot by typing any of the commands above!

Instructions for usage after:
1. Open the game
2. Bot should automatically connect. If it doesn't, move to step 3.
3. Head to TootTally Settings and the twitch page
4. Scroll down and click "Connect Bot"

### FAQ

> To access the Requests panel, press **F8** in the song list screen. If you have the chart requested, pressing the check mark will close the panel and lead you directly to the chart (if it's within search parameters). If you do not have the chart requested, you can choose to download the chart in-game or open the website link to download it directly (if it can't be downloaded in-game). If the chart does not show up, please double-check your filters and search (if you're using SongOrganizer). If it still isn't showing up, you can force-refresh the song list by doing `Ctrl + R`.

> Some people asked to display the song name when requesting but this is very dangerous. If someone maliciously uploaded a chart with unwanted words in it, using the names in chat could result in a permanent Twitch ban. For this reason, only the song IDs and leaderboard links will be sent in chat.

### Further Help and Support

For more help, join the [Trombone Champ Modding Discord](https://discord.gg/KVzKRsbetJ) or the [TootTally Discord](https://discord.gg/9jQmVEDVTp)

### TootTally Installation Instructions

Charts being tracked can be searched in https://toottally.com/search/ or accessed programmatically in https://toottally.com/api/songs/
Charts can be manually uploaded in https://toottally.com/upload/

A guide for creating custom themes can be found [here](https://bit.ly/toottallythemeguide)

