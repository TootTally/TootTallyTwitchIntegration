using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using static TootTallyTwitchIntegration.Plugin;
using TootTallyCore.Graphics;
using TootTallyCore.Graphics.Animations;
using TootTallyCore.Utils.TootTallyNotifs;

namespace TootTallyTwitchIntegration
{
    public static class RequestPanelManager
    {
        private const float MIN_POS_Y = -40;
        public static GameObject requestRowPrefab;
        public static LevelSelectController songSelectInstance;
        public static int songIndex;
        public static bool isPlaying;
        private static List<RequestPanelRow> _requestRowList;
        private static List<Request> _requestList;
        private static List<BlockedRequests> _blockedList;
        private static List<int> _songIDHistory;
        public static int currentSongID;
        public static int RequestCount => _requestList.Count;

        private static ScrollableSliderHandler _scrollableHandler;
        private static Slider _slider;

        private static RectTransform _containerRect;
        private static TootTallyAnimation _panelAnimationFG, _panelAnimationBG;

        private static GameObject _overlayPanel;
        private static GameObject _overlayCanvas;
        private static GameObject _overlayPanelContainer;
        private static bool _isPanelActive;
        private static bool _isInitialized;
        private static bool _isAnimating;
        public static void Initialize()
        {
            if (_isInitialized) return;

            _overlayCanvas = new GameObject("TwitchOverlayCanvas");
            Canvas canvas = _overlayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1;
            CanvasScaler scaler = _overlayCanvas.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _requestRowList = new List<RequestPanelRow>();
            _requestList = new List<Request>();
            _blockedList = new List<BlockedRequests>();
            _songIDHistory = new List<int>();

            GameObject.DontDestroyOnLoad(_overlayCanvas);

            _overlayPanel = GameObjectFactory.CreateOverlayPanel(_overlayCanvas.transform, Vector2.zero, new Vector2(1700, 900), 20f, "TwitchOverlayPanel");
            _overlayPanelContainer = _overlayPanel.transform.Find("FSLatencyPanel/LatencyFG/MainPage").gameObject;
            
            _slider = new GameObject("TwitchPanelSlider", typeof(Slider)).GetComponent<Slider>();
            _slider.transform.SetParent(_overlayPanel.transform);
            _slider.onValueChanged.AddListener(OnScrolling);
            _scrollableHandler = _slider.gameObject.AddComponent<ScrollableSliderHandler>();


            _overlayPanel.transform.Find("FSLatencyPanel/LatencyFG").localScale = Vector2.zero;
            _overlayPanel.transform.Find("FSLatencyPanel/LatencyBG").localScale = Vector2.zero;
            _overlayPanel.transform.Find("FSLatencyPanel/LatencyFG").GetComponent<Image>().color = new Color(.1f, .1f, .1f);
            _containerRect = _overlayPanelContainer.GetComponent<RectTransform>();
            _containerRect.anchoredPosition = new Vector2(0,-40);
            _containerRect.sizeDelta = new Vector2(1700, 900);


            var verticalLayout = _overlayPanelContainer.GetComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(20, 20, 20, 20);
            verticalLayout.spacing = 120f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlHeight = verticalLayout.childControlWidth = true;
            _overlayPanelContainer.transform.parent.gameObject.AddComponent<Mask>();
            GameObjectFactory.DestroyFromParent(_overlayPanelContainer.transform.parent.gameObject, "subtitle");
            GameObjectFactory.DestroyFromParent(_overlayPanelContainer.transform.parent.gameObject, "title");
            var text = GameObjectFactory.CreateSingleText(_overlayPanelContainer.transform, "title", "Twitch Requests", Color.white);
            text.fontSize = 60f;
            _overlayPanel.SetActive(false);
            SetRequestRowPrefab();

            _requestList = FileManager.GetRequestsFromFile();
            _requestList.ForEach(AddRowFromFile);
            _blockedList = FileManager.GetBlockedRequestsFromFile();

            _isPanelActive = false;
            _isInitialized = true;
            isPlaying = false;
        }

        public static void Update()
        {
            if (!_isInitialized) return; //just in case

            if (Input.GetKeyDown(KeyCode.F8))
                TogglePanel();

            if (Input.GetKeyDown(KeyCode.Escape) && _isPanelActive)
                TogglePanel();
        }

        private static void OnScrolling(float value)
        {
            _containerRect.anchoredPosition = new Vector2(_containerRect.anchoredPosition.x, value * (65f * _requestList.Count) - 40f);
        }
        
        public static void TogglePanel()
        {
            _isPanelActive = !_isPanelActive;
            _scrollableHandler.enabled = _isPanelActive && _requestRowList.Count > 6;
            _isAnimating = true;
            if (_overlayPanel != null)
            {
                _panelAnimationBG?.Dispose();
                _panelAnimationFG?.Dispose();
                var targetVector = _isPanelActive ? Vector2.one : Vector2.zero;
                var animationTime = _isPanelActive ? 1f : 0.45f;
                var secondDegreeAnimationFG = _isPanelActive ? new SecondDegreeDynamicsAnimation(1.75f, 1f, 0f) : new SecondDegreeDynamicsAnimation(3.2f, 1f, 0.25f);
                var secondDegreeAnimationBG = _isPanelActive ? new SecondDegreeDynamicsAnimation(1.75f, 1f, 0f) : new SecondDegreeDynamicsAnimation(3.2f, 1f, 0.25f);
                _panelAnimationFG = TootTallyAnimationManager.AddNewScaleAnimation(_overlayPanel.transform.Find("FSLatencyPanel/LatencyFG").gameObject, targetVector, animationTime, secondDegreeAnimationFG);
                _panelAnimationBG = TootTallyAnimationManager.AddNewScaleAnimation(_overlayPanel.transform.Find("FSLatencyPanel/LatencyBG").gameObject, targetVector, animationTime, secondDegreeAnimationBG, (sender) =>
                {
                    _isAnimating = false;
                    if (!_isPanelActive)
                        _overlayPanel.SetActive(_isPanelActive);
                });
                if (_isPanelActive)
                    _overlayPanel.SetActive(_isPanelActive);
            }
        }

        public static void AddRow(Request request)
        {
            _requestList.Add(request);
            UpdateSaveRequestFile();
            _requestRowList.Add(new RequestPanelRow(_overlayPanelContainer.transform, request));
            _scrollableHandler.accelerationMult = 6f / _requestRowList.Count;
            _scrollableHandler.enabled = _requestRowList.Count > 6;
        }

        public static void AddToBlockList(int id)
        {
            _blockedList.Add(new BlockedRequests() { song_id = id });
            TootTallyNotifManager.DisplayNotif($"Song #{id} blocked.");
            FileManager.SaveBlockedRequestsToFile(_blockedList);
        }

        public static void AddRowFromFile(Request request) =>
            _requestRowList.Add(new RequestPanelRow(_overlayPanelContainer.transform, request));

        public static void Dispose()
        {
            if (!_isInitialized) return; //just in case too

            GameObject.DestroyImmediate(_overlayCanvas);
            GameObject.DestroyImmediate(_overlayPanel);
            _isInitialized = false;
        }

        public static void Remove(RequestPanelRow row)
        {
            _requestList.Remove(row.request);
            UpdateSaveRequestFile();
            _requestRowList.Remove(row);
            _slider.value = 0;
        }

        public static void Remove(string trackref)
        {
            var request = _requestRowList.Find(r => r.request.songData.track_ref == trackref);
            if (request == null) return;

            request.RemoveFromPanel();
            RequestPanelManager.AddSongIDToHistory(request.request.song_id);
            TootTallyNotifManager.DisplayNotif($"Fulfilled request from {request.request.requester}", Color.white);
        }

        public static void SetRequestRowPrefab()
        {

            var tempRow = GameObjectFactory.CreateOverlayPanel(_overlayCanvas.transform, Vector2.zero, new Vector2(1200, 84), 5f, $"TwitchRequestRowTemp").transform.Find("FSLatencyPanel").gameObject;
            requestRowPrefab = GameObject.Instantiate(tempRow);
            GameObject.DestroyImmediate(tempRow.gameObject);

            requestRowPrefab.name = "RequestRowPrefab";
            requestRowPrefab.transform.localScale = Vector3.one;

            requestRowPrefab.GetComponent<Image>().maskable = true;
            var container = requestRowPrefab.transform.Find("LatencyFG/MainPage").gameObject;
            container.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 100);
            GameObject.DestroyImmediate(container.transform.parent.Find("subtitle").gameObject);
            GameObject.DestroyImmediate(container.transform.parent.Find("title").gameObject);
            GameObject.DestroyImmediate(container.GetComponent<VerticalLayoutGroup>());
            var horizontalLayoutGroup = container.AddComponent<HorizontalLayoutGroup>();
            horizontalLayoutGroup.padding = new RectOffset(20, 20, 20, 20);
            horizontalLayoutGroup.spacing = 20f;
            horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayoutGroup.childControlHeight = horizontalLayoutGroup.childControlWidth = false;
            horizontalLayoutGroup.childForceExpandHeight = horizontalLayoutGroup.childForceExpandWidth = false;
            requestRowPrefab.transform.Find("LatencyFG").GetComponent<Image>().maskable = true;
            requestRowPrefab.transform.Find("LatencyBG").GetComponent<Image>().maskable = true;

            GameObject.DontDestroyOnLoad(requestRowPrefab);
            requestRowPrefab.SetActive(false);
        }

        public static void SetTrackToTrackref(string trackref)
        {
            if (songSelectInstance == null) return;
            for (int i = 0; i < songSelectInstance.alltrackslist.Count; i++)
            {
                if (songSelectInstance.alltrackslist[i].trackref == trackref)
                {
                    var attempts = 0;
                    while (i - songIndex != 0 && songSelectInstance.songindex != i && attempts <= 3)
                    {
                        // Only advance songs if we're not on the same song already
                        songSelectInstance.advanceSongs(i - songIndex, true);
                        attempts++;
                    }
                    return;
                }
            }
        }

        public static void AddSongIDToHistory(int id) => _songIDHistory.Add(id);
        public static string GetSongIDHistoryString() => _songIDHistory.Count > 0 ? string.Join(", ", _songIDHistory) : "No songs history recorded";

        public static string GetSongQueueIDString() => _requestList.Count > 0 ? string.Join(", ", _requestList.Select(x => x.song_id)) : "No songs requested";
        public static string GetLastSongPlayed() => _songIDHistory.Count > 0 ? $"https://toottally.com/song/{_songIDHistory.Last()}" : "No song played";

        public static bool IsDuplicate(int song_id) => _requestRowList.Any(x => x.request.song_id == song_id);

        public static bool IsBlocked(int song_id) => _blockedList.Any(x => x.song_id == song_id);

        public static bool ShouldScrollSongs() => !_isPanelActive && !_isAnimating;

        public static void UpdateSaveRequestFile()
        {
            FileManager.SaveRequestsQueueToFile(_requestList);
        }
    }
}