using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TootTallyCore.APIServices;
using TootTallyCore.Utils.TootTallyNotifs;
using UnityEngine;
using static TootTallyTwitchIntegration.Plugin;

namespace TootTallyTwitchIntegration
{
    public class RequestController : MonoBehaviour
    {
        public List<string> RequesterBlacklist { get; set; }
        private ConcurrentQueue<Notif> NotifQueue;
        private ConcurrentQueue<UnprocessedRequest> RequestQueue; // Unfinished request stack, only song ids here

        public void Awake()
        {
            NotifQueue = new ConcurrentQueue<Notif>();
            RequestQueue = new ConcurrentQueue<UnprocessedRequest>();
            RequesterBlacklist = new List<string>();
        }

        public void Update()
        {
            if (RequestPanelManager.isPlaying) return;

            if (RequestQueue.TryDequeue(out UnprocessedRequest request))
            {
                LogInfo($"Attempting to get song data for ID {request.song_id}");
                Instance.StartCoroutine(TootTallyAPIService.GetSongDataFromDB(request.song_id, (songdata) =>
                {
                    LogInfo($"Obtained request by {request.requester} for song {songdata.author} - {songdata.name}");
                    TootTallyNotifManager.DisplayNotif($"Requested song by {request.requester}: {songdata.author} - {songdata.name}");
                    var processed_request = new Request
                    {
                        requester = request.requester,
                        songData = songdata,
                        song_id = request.song_id,
                        date = DateTime.Now.ToString()
                    };
                    RequestPanelManager.AddRow(processed_request);
                }));
            }

            if (NotifQueue.TryDequeue(out Notif notif))
            {
                LogInfo("Attempting to generate notification...");
                TootTallyNotifManager.DisplayNotif(notif.message);
            }
        }

        public void RequestSong(int song_id, string requester, bool isSubscriber = false)
        {

            if (!RequesterBlacklist.Contains(requester))
            {
                if (RequestPanelManager.IsBlocked(song_id))
                {
                    Instance.Bot.client.SendMessage(Instance.Bot.CHANNEL, $"! Song #{song_id} is blocked.");
                    return;
                }
                else if (RequestPanelManager.IsDuplicate(song_id) && !RequestQueue.Any(x => x.song_id == song_id))
                {
                    Instance.Bot.client.SendMessage(Instance.Bot.CHANNEL, $"! Song #{song_id} already requested.");
                    return;
                }
                else if (RequestPanelManager.RequestCount >= Instance.MaxRequestCount.Value)
                {
                    Instance.Bot.client.SendMessage(Instance.Bot.CHANNEL, $"! Request cap reached.");
                    return;
                }
                else if (Instance.SubOnlyMode.Value && !isSubscriber)
                {
                    return; // Silently ignore non-subscriber requests if in Sub Only mode
                }
                UnprocessedRequest request = new UnprocessedRequest();
                request.song_id = song_id;
                request.requester = requester;
                LogInfo($"Accepted request {song_id} by {requester}.");
                Instance.Bot.client.SendMessage(Instance.Bot.CHANNEL, $"! Song #{song_id} successfully requested.");
                RequestQueue.Enqueue(request);
            }
        }

        public void Dispose()
        {
            NotifQueue?.Clear();
            NotifQueue = null;
            RequestQueue?.Clear();
            RequestQueue = null;
            RequesterBlacklist?.Clear();
            RequesterBlacklist = null;
        }

    }
}
