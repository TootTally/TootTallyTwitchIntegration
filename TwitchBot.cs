using System;
using System.Collections.Generic;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Events;
using TootTallyAccounts;
using TootTallyCore.Utils.TootTallyNotifs;

namespace TootTallyTwitchIntegration
{
    public class TwitchBot
    {
        internal TwitchClient client;
        public string CHANNEL { get; set; }
        public Stack<string> MessageStack { get; set; }

        public TwitchBot()
        {
            if (!Initialize()) return;
            Plugin.LogInfo($"Attempting connection with channel {CHANNEL}...");
            ConnectionCredentials credentials = new ConnectionCredentials(CHANNEL, Plugin.Instance.TwitchAccessToken.Value);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
                ReconnectionPolicy = new ReconnectionPolicy(reconnectInterval: 5, maxAttempts: 3),
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, CHANNEL);

            client.OnLog += Client_OnLog;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnConnected += Client_OnConnected;
            client.OnChatCommandReceived += Client_HandleChatCommand;
            client.OnIncorrectLogin += Client_OnIncorrectLogin;
            client.OnError += Client_OnError;
            client.OnDisconnected += Client_OnDisconnected;

            MessageStack = new Stack<string>();

            client.Connect();
        }

        public void Disconnect()
        {
            if (client != null && client.IsConnected) client.Disconnect();
            MessageStack?.Clear();
            MessageStack = null;
        }

        private bool Initialize()
        {
            if (Plugin.Instance.TwitchAccessToken.Value == null || Plugin.Instance.TwitchAccessToken.Value == "")
            {
                TootTallyNotifManager.DisplayError("Twitch Access Token is empty. Please fill it in.");
                return false;
            }
            if (Plugin.Instance.TwitchUsername.Value == null || Plugin.Instance.TwitchUsername.Value == "")
            {
                TootTallyNotifManager.DisplayError("Twitch Username is empty. Please fill it in.");
                return false;
            }
            CHANNEL = Plugin.Instance.TwitchUsername.Value.ToLower();
            return true;
        }

        private void Client_OnError(object sender, OnErrorEventArgs args)
        {
            Plugin.LogError($"{args.Exception}\n{args.Exception.StackTrace}");
        }

        private void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs args)
        {
            TootTallyNotifManager.DisplayError("Login credentials incorrect. Please re-authorize or refresh your access token, and re-check your Twitch username.");
            client.Disconnect();
        }

        private void Client_HandleChatCommand(object sender, OnChatCommandReceivedArgs args)
        {
            string command = args.Command.CommandText;
            string cmd_args = args.Command.ArgumentsAsString;
            switch (command)
            {
                case "ttr": // Request a song
                    if (Plugin.Instance.EnableRequestsCommand.Value)
                    {
                        if (args.Command.ArgumentsAsList.Count == 1)
                        {
                            if (int.TryParse(cmd_args, out int song_id))
                            {
                                Plugin.LogInfo($"Successfully parsed request for {song_id}, submitting to stack.");
                                Plugin.Instance.requestController.RequestSong(song_id, args.Command.ChatMessage.Username, args.Command.ChatMessage.IsSubscriber);
                            }
                            else
                            {
                                Plugin.LogInfo("Could not parse request input, ignoring.");
                                client.SendMessage(CHANNEL, "! Invalid song ID. Please try again.");
                            }
                        }
                        else
                        {
                            client.SendMessage(CHANNEL, $"! Use !ttr to request a chart use its TootTally Song ID! To get a song ID, search for the song in https://toottally.com/search/ (Example: !ttr 3781)");
                        }
                    }
                    break;
                case "ttrprofile": // Get profile
                    if (Plugin.Instance.EnableProfileCommand.Value && TootTallyUser.userInfo.id > 0)
                        client.SendMessage(CHANNEL, $"! TootTally Profile: https://toottally.com/profile/{TootTallyUser.userInfo.id}");
                    break;
                case "ttrsong": // Get current song
                    if (Plugin.Instance.EnableCurrentSongCommand.Value && RequestPanelManager.currentSongID != 0)
                    {
                        client.SendMessage(CHANNEL, $"! Current Song: https://toottally.com/song/{RequestPanelManager.currentSongID}");
                    }
                    break;
                case "ttrhelp":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        client.SendMessage(CHANNEL, $"! Use !ttr to request a chart use its TootTally Song ID! To get a song ID, search for the song in https://toottally.com/search/ (Example: !ttr 3781)");
                    break;
                case "ttrqueue":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        client.SendMessage(CHANNEL, $"! Song Queue: {RequestPanelManager.GetSongQueueIDString()}");
                    break;
                case "ttrlast":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        client.SendMessage(CHANNEL, $"! Last song played: {RequestPanelManager.GetLastSongPlayed()}");
                    break;
                case "ttrhistory":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        client.SendMessage(CHANNEL, $"! Songs played: {RequestPanelManager.GetSongIDHistoryString()}");
                    break;
                default:
                    break;
            }
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Plugin.LogDebug($"{e.DateTime}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Plugin.LogInfo($"Connected to {e.AutoJoinChannel}");
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            client.SendMessage(e.Channel, "! TootTally Twitch Integration ready!");
            TootTallyNotifManager.DisplayNotif("Twitch Integration successful!");
            Plugin.LogInfo("Twitch integration successfully attached to chat!");
            CHANNEL = e.Channel;
        }

        private void Client_OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            Plugin.LogInfo("TwitchBot successfully disconnected from Twitch!");
            TootTallyNotifManager.DisplayNotif("Twitch bot disconnected!");
        }
    }
}
