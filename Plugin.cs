﻿using BaboonAPI.Hooks.Initializer;
using BaboonAPI.Hooks.Tracks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using TootTallyAccounts;
using TootTallyCore;
using TootTallyCore.APIServices;
using TootTallyCore.Graphics;
using TootTallyCore.Utils.Assets;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyModules;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallySettings;
using TrombLoader.CustomTracks;
using UnityEngine;

namespace TootTallyTwitchIntegration
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallyCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallySettings", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallyAccounts", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "TwitchIntegration.cfg";
        private const string CONFIG_FIELD = "Twitch";
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "Twitch Integration"; set => Name = value; }

        public static TootTallySettingPage settingPage;
        public TwitchBot Bot = null;
        public RequestController requestController;
        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);
        public static void LogDebug(string msg) => Instance.Logger.LogDebug(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void Update()
        {
            RequestPanelManager.Update();
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "Twitch", true, "Twitch integration with song requests and more.");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            AssetManager.LoadAssets(Path.Combine(Path.GetDirectoryName(Instance.Info.Location), "Assets"));
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            string toottallyTwitchLink = "https://toottally.com/twitch/";
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            ToggleRequestPanel = config.Bind(CONFIG_FIELD, "TogglePanel", KeyCode.F8, "Key to toggle the twitch request panel.");
            EnableRequestsCommand = config.Bind(CONFIG_FIELD, "Enable requests command", true, "Allow people to requests songs using !ttr [songID]");
            EnableCurrentSongCommand = config.Bind(CONFIG_FIELD, "Enable current song command", true, "!ttrsong command that sends a link to the current song into the chat");
            EnableProfileCommand = config.Bind(CONFIG_FIELD, "Enable profile command", true, "!ttrprofile command that links your toottally profile into the chat");
            SubOnlyMode = config.Bind(CONFIG_FIELD, "Sub-only requests", false, "Only allow subscribers to send requests");
            TwitchUsername = config.Bind(CONFIG_FIELD, "Twitch channel to attach to", "", "Paste your twitch username here");
            TwitchAccessToken = config.Bind(CONFIG_FIELD, "Twitch Access Token", "", "Paste the access token from the website here");
            MaxRequestCount = config.Bind(CONFIG_FIELD, "Max Request Count", 50f, "Maximum request count allowed in queue");

            settingPage = TootTallySettingsManager.AddNewPage(CONFIG_FIELD, "Twitch Integration Settings", 40, new Color(.1f, .1f, .1f, .1f));
            if (settingPage != null)
            {
                settingPage.AddLabel("ToggleKeybindLabel", "Keybind Toggle Request Panel", 20);
                settingPage.AddDropdown("Toggle Request Panel", ToggleRequestPanel);
                settingPage.AddToggle("Enable Requests Command", EnableRequestsCommand);
                settingPage.AddToggle("Enable Current Songs Command", EnableCurrentSongCommand);
                settingPage.AddToggle("Enable Profile Command", EnableProfileCommand);
                settingPage.AddToggle("Subs Only Requests", SubOnlyMode);
                settingPage.AddSlider("Max Request Count", 0, 200, MaxRequestCount, true);
                settingPage.AddLabel("TwitchSpecificSettingsLabel", "Twitch Integration", 24); // 20 is the default size for text
                settingPage.AddLabel("TwitchSpecificUsernameLabel", "Username", 16, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.BottomLeft);
                settingPage.AddTextField("Twitch Username", new Vector2(350, 50), 20, TwitchUsername.Value, TwitchUsername.Description.Description, false, SetTwitchUsername);
                settingPage.AddLabel("TwitchSpecificAccessTokenLabel", "AccessToken", 16, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.BottomLeft);
                settingPage.AddButton("AuthorizeTwitchButton", new Vector2(450, 50), "Authorize TootTally on Twitch", "Opens a page to get your auth token", delegate () { Application.OpenURL(toottallyTwitchLink); });
                settingPage.AddTextField("Twitch Access Token", new Vector2(350, 50), 20, TwitchAccessToken.Value, TwitchAccessToken.Description.Description, true, SetTwitchAccessToken);
                settingPage.AddLabel("TwitchBotButtons", "Twitch Bot Settings", 24);
                settingPage.AddButton("ConnectDisconnectBot", new Vector2(350, 50), "Connect/Disconnect Bot", "Rarely useful if the bot fails to connect after setting the auth token", () =>
                {
                    if (Bot == null)
                    {
                        StartBotCoroutine(); // Start and connect the bot if no bot detected yet
                    }
                    else
                    {
                        Bot.Disconnect(); // Disconnect the current bot if it exists
                        Bot = null;
                    }
                });
                settingPage.AddLabel("TwitchBotInstruction", "Twitch bot will also automatically start when you enter the song select menu.", 16);
            }
            requestController = gameObject.AddComponent<RequestController>();

            TootTallySettings.Plugin.TryAddThunderstoreIconToPageButton(Instance.Info.Location, Name, settingPage);
            ThemeManager.OnThemeRefreshEvents += RequestPanelManager.UpdateTheme;

            _harmony.PatchAll(typeof(TwitchPatches));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            ThemeManager.OnThemeRefreshEvents -= RequestPanelManager.UpdateTheme;
            RequestPanelManager.Dispose();
            Bot?.Disconnect();
            Bot = null;
            requestController?.Dispose();
            GameObject.DestroyImmediate(requestController);
            StopAllCoroutines();
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        private void SetTwitchUsername(string text)
        {
            Instance.TwitchUsername.Value = text;
            TootTallyNotifManager.DisplayNotif($"Twitch username is set to '{text}'");
        }

        public void StartBotCoroutine()
        {
            Bot ??= new TwitchBot();
        }

        private void SetTwitchAccessToken(string text)
        {
            Instance.TwitchAccessToken.Value = text;
        }

        public static class TwitchPatches
        {
            private static string _selectedSongTrackRef;

            // Apply your Trombone Champ patches here
            [HarmonyPatch(typeof(GameObjectFactory), nameof(GameObjectFactory.OnHomeControllerInitialize))]
            [HarmonyPostfix]
            public static void InitializeRequestPanel()
            {
                RequestPanelManager.Initialize();
            }

            [HarmonyPatch(typeof(HomeController), nameof(HomeController.Start))]
            [HarmonyPostfix]
            public static void DeInitialize()
            {
                RequestPanelManager.songSelectInstance = null;
            }

            [HarmonyPatch(typeof(TootTallyUser), nameof(TootTallyUser.OnUserLogin))]
            [HarmonyPostfix]
            public static void OnUserLoginInitializeBot()
            {
                Instance.StartBotCoroutine();
            }

            [HarmonyPatch(typeof(HomeController), nameof(HomeController.tryToSaveSettings))]
            [HarmonyPostfix]
            public static void InitializeRequestPanelOnSaveConfig()
            {
                Instance.StartBotCoroutine();
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void SetCurrentSong()
            {
                RequestPanelManager.songSelectInstance = null;
                RequestPanelManager.isPlaying = true;
                var track = TrackLookup.lookup(GlobalVariables.chosen_track_data.trackref);
                var songHash = SongDataHelper.GetSongHash(track);
                Instance.StartCoroutine(TootTallyAPIService.GetHashInDB(songHash, track is CustomTrack, id => RequestPanelManager.currentSongID = id));
            }

            [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
            [HarmonyPostfix]
            public static void ResetCurrentSong()
            {
                RequestPanelManager.isPlaying = false;
                RequestPanelManager.Remove(GlobalVariables.chosen_track_data.trackref);
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Start))]
            [HarmonyPostfix]
            public static void StartBot(LevelSelectController __instance, int ___songindex)
            {
                RequestPanelManager.songSelectInstance = __instance;
                RequestPanelManager.songIndex = ___songindex;
                RequestPanelManager.isPlaying = false;
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.advanceSongs))]
            [HarmonyPostfix]
            public static void UpdateInstance(LevelSelectController __instance, int ___songindex)
            {
                RequestPanelManager.songSelectInstance = __instance;
                RequestPanelManager.songIndex = ___songindex;
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.clickBack))]
            [HarmonyPrefix]
            private static bool OnClickBackSkipIfPanelActive() => ShouldScrollSongs();

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.clickNext))]
            [HarmonyPrefix]
            private static bool OnClickNextSkipIfScrollWheelUsed() => ShouldScrollSongs();

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.clickPrev))]
            [HarmonyPrefix]
            private static bool OnClickBackSkipIfScrollWheelUsed() => ShouldScrollSongs();
            private static bool ShouldScrollSongs() => RequestPanelManager.ShouldScrollSongs();
        }

        public ConfigEntry<KeyCode> ToggleRequestPanel { get; set; }
        public ConfigEntry<bool> EnableRequestsCommand { get; set; }
        public ConfigEntry<bool> EnableProfileCommand { get; set; }
        public ConfigEntry<bool> EnableCurrentSongCommand { get; set; }
        public ConfigEntry<bool> SubOnlyMode { get; set; }
        public ConfigEntry<string> TwitchUsername { get; set; }
        public ConfigEntry<string> TwitchAccessToken { get; set; }
        public ConfigEntry<float> MaxRequestCount { get; set; }

        [Serializable]
        public class Request
        {
            public string requester;
            public SerializableClass.SongDataFromDB songData;
            public int song_id;
            public string date;
        }

        [Serializable]
        public class BlockedRequests
        {
            public int song_id;
        }

        public class UnprocessedRequest
        {
            public string requester;
            public int song_id;
        }

        public class Notif
        {
            public string message;
        }
    }
}