#if ENABLE_UI_UGUI
using System;
using System.Collections;
using GameFrameX.UI.Runtime;
using GameFrameX.UI.UGUI.Runtime;
using Hotfix.Manager;
using UnityEngine;

namespace Hotfix.UI
{
    [Serializable]
    public sealed class DifferenceLevel
    {
        public string title;
        public string analyticsPicId, analyticsPicName, analyticsDifficulty;
        public string[] analyticsParts;
        public Sprite original, changed;
        public Vector4[] regions;
        [NonSerialized] public string contentKey;
        [NonSerialized] public Sprite[] croppedPatches;
        [NonSerialized] public DifferenceSpot[] hitSpots;
        [NonSerialized] public Vector4[] markerRegions;
        [NonSerialized] public bool changedOnTop;
    }

    public sealed partial class UIDifferences : UGUI
    {
        public RectTransform stage;
        public GameObject home, play, modal, shop, ranking, settings, profile, musicPanel, achievements, album, navigation;
        public UnityEngine.UI.Text coinsLabel, startLabel, levelLabel, heartsLabel, progressLabel, hintLabel, noticeLabel;
        public UnityEngine.UI.Text modalTitle, modalBody, modalActionLabel;
        public UnityEngine.UI.Button startButton, settingsButton, shopButton, trophyButton, backButton, hintButton, modalAction, modalClose;
        public UnityEngine.UI.Button modalSecondary, modalTertiary;
        public Spine.Unity.SkeletonGraphic[] progressDots;
        public UnityEngine.UI.Image[] topRings, bottomRings, patchImages, avatars;
        public GameObject[] differencePatches;
        public DifferenceSpot[] spots;
        public DifferenceLevel[] levels;
        public Sprite[] avatarSprites;
        public Sprite hintHandSprite;
        public Material foundFlightMaterial, hintSpotlightMaterial;
        public GameObject foundFlightPrefab, foundArrivalPrefab;
        public GameObject foundRingEffectPrefab;
        public Spine.Unity.SkeletonDataAsset foundRingSpine;
        public Material foundRingMaterial;
        [SerializeField] bool useSpineFoundRing;
        [Header("命中圆圈：统一调整双图所有圆圈")]
        public DifferenceFoundRing.Style foundRingStyle = new DifferenceFoundRing.Style();
        public UnityEngine.UI.Image upperImage, lowerImage, profileAvatar, homeAvatar, profileFrame, homeFrame, resultIcon;
        public UnityEngine.UI.InputField nicknameInput;
        public UnityEngine.UI.Graphic crossTop, crossBottom;
        public AudioSource audioSource, musicSource;
        public AudioClip foundSound, missSound, winSound;
        public AudioClip coinCreateSound, progressCompleteSound;
        public AudioClip buttonSound, failSound, tipsSound, homeMusic, gameMusic, journeyMusic;
        public AudioClip[] musicTracks;
        //public RectTransform[] confetti;
        public DifferenceRound Round { get; private set; }
        public int SelectedLevel => level;
        public int Hints => hints;
        bool UnlimitedHints => gmUnlimitedHints || (!JourneyPlaying && level == 0);
        public int Coins => coins;
        public int Completed => completed;
        public const float ImageAspect = 1.5f;
        public const float BackgroundMusicVolume = .3f;
        public const string Key = "FindDifferences.";
        public static readonly Color[] FrameColors = { new Color(.95f,.37f,.65f), new Color(.15f,.68f,1), new Color(.71f,.31f,.95f), new Color(1,.70f,.14f), new Color(.35f,.9f,.5f), new Color(.95f,.35f,.27f) };
        int level, completed, coins, hints, avatar, frame, music, totalFound, perfect, ownedAvatars, ownedFrames;
        int draftAvatar, draftFrame, roundMistakes;
        bool sound, musicEnabled, settled, frameTab, testing, shopReturnsToPlay, interactionsBound;
        bool vibration = true;
        string nickname, giftDate, roundContent;
        Action dialogAction, secondaryAction, tertiaryAction;
        Coroutine endRoutine;
        CommonToast toast;
        const int LevelCoinReward = 10;
        const float TabSlideDuration = .18f;
        RectTransform[] tabPages, tabButtons;
        RectTransform tabHighlight;
        readonly RectTransform[] tabBackgrounds = new RectTransform[3];
        CanvasGroup[] tabPageInput;
        GameObject currentPage;
        float tabPosition = 1, tabStartPosition, tabElapsed, tabDuration;
        float highlightX = 206, highlightStartX;
        int selectedTab = 1;
        bool tabsSliding, tabsDragging;
        readonly bool[] pendingProgress = new bool[DifferenceRound.MaxSpots];
        DifferenceHintSpotlight hintSpotlight;
        DifferenceHintHand hintHand;
        Spine.Unity.SkeletonGraphic lifeAnimation;
        float lifeLossDuration;
        UnityEngine.UI.Button resetZoomButton;
        [Range(0, 1), Tooltip("屏幕宽于 1179 时，返回、生命值、缩回按钮向两侧移动的比例。0 不移动，1 跟随原锚点。")]
        public float wideScreenControlMovement = .5f;
        RectTransform[] widthControls;
        Vector2[] widthControlPositions;
        DifferencePlayLayout playLayout;
        UnityEngine.UI.Image[] screenBackgrounds;
        float[] screenBackgroundAspects;
        RectTransform[] safeStretchPages, safeFixedPages;
        RectTransform safeToast;
        float hintIdle;
        int pendingHint = -1;
        public bool HintActive => hintSpotlight && hintSpotlight.isActiveAndEnabled;

        public override void OnAwake()
        {
            UIGroup = GameApp.UI.GetUIGroup(UIGroupConstants.Normal.Name);
            base.OnAwake();
        }

        public override void OnInit()
        {
            base.OnInit();
            if (interactionsBound) return;
            if (levels == null || levels.Length == 0 || topRings.Length != 15 || bottomRings.Length != 15)
                throw new InvalidOperationException("Run Tools/Find Differences/Build Game Assets first.");
            tabPages = new[] { (RectTransform)shop.transform, (RectTransform)home.transform, (RectTransform)ranking.transform };
            tabButtons = new[] { (RectTransform)shopButton.transform, At<RectTransform>("Navigation/HomeTab"), (RectTransform)trophyButton.transform };
            var footer = At<RectTransform>("Navigation/Footer");
            footer.anchoredPosition = Vector2.zero;
            footer.sizeDelta = ((RectTransform)navigation.transform).sizeDelta;
            tabPageInput = new CanvasGroup[3];
            for (var i = 0; i < 3; i++)
            {
                var input = tabPages[i].GetComponent<CanvasGroup>();
                tabPageInput[i] = input ? input : tabPages[i].gameObject.AddComponent<CanvasGroup>();
                tabPages[i].gameObject.AddComponent<DifferencePageSwipe>().owner = this;
                tabPages[i].Find(i == 1 ? "Beach" : "Background").GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
                foreach (var scroll in tabPages[i].GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
                {
                    var swipe = scroll.viewport.gameObject.AddComponent<DifferencePageSwipe>();
                    swipe.owner = this; swipe.scroll = scroll;
                }
                tabBackgrounds[i] = CreateTabPlate("TabBackground" + i, new Color(.18f,.35f,.76f));
                tabBackgrounds[i].SetSiblingIndex(i + 1);
                tabButtons[i].GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                var tabButton = tabButtons[i].GetComponent<UnityEngine.UI.Button>();
                tabButton.targetGraphic = tabButtons[i].Find("Icon").GetComponent<UnityEngine.UI.Image>();
                var colors = tabButton.colors; colors.fadeDuration = 0; tabButton.colors = colors;
                tabButtons[i].gameObject.AddComponent<DifferenceTabPress>();
            }
            tabHighlight = CreateTabPlate("Selection", new Color(.30f,.55f,1));
            tabHighlight.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier = 1.5f;
            tabHighlight.sizeDelta = new Vector2(308, 120);
            tabHighlight.SetSiblingIndex(4);
            var tabShadow = tabHighlight.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            tabShadow.effectColor = new Color(.04f,.1f,.2f,.45f); tabShadow.effectDistance = new Vector2(0, -1);
            /*if (!stage.GetComponent<UnityEngine.UI.RectMask2D>()) 
                stage.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();*/
            DifferenceButtonShine.Create(startButton);
            InitializeHintButtonLook();
            hintHand = DifferenceHintHand.Create(hintButton, hintHandSprite);
            hintSpotlight = DifferenceHintSpotlight.Create((RectTransform)play.transform, hintSpotlightMaterial);
            foreach (var popup in new[] { modal, profile, musicPanel, achievements, album })
                popup.AddComponent<DifferencePopupMotion>();
            foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
                button.onClick.AddListener(PlayButtonSound);
            startButton.onClick.AddListener(StartLevel);
            settingsButton.onClick.AddListener(ShowSettings);
            shopButton.onClick.AddListener(ShowShop);
            trophyButton.onClick.AddListener(() => { if (!AdInputBlocked) ShowPage(ranking); });
            backButton.onClick.AddListener(ShowPause);
            hintButton.onClick.AddListener(UseHint);
            modalAction.onClick.AddListener(() => { if (AdInputBlocked) return; modal.SetActive(false); dialogAction?.Invoke(); UpdateAdsVisibility(); });
            modalSecondary.onClick.AddListener(() => { if (AdInputBlocked) return; modal.SetActive(false); secondaryAction?.Invoke(); UpdateAdsVisibility(); });
            modalTertiary.onClick.AddListener(() => { if (AdInputBlocked) return; modal.SetActive(false); tertiaryAction?.Invoke(); UpdateAdsVisibility(); });
            modalClose.onClick.AddListener(() => { if (AdInputBlocked) return; modal.SetActive(false); UpdateAdsVisibility(); });
            Bind("Navigation/HomeTab", ShowHome);
            Bind("Home/Avatar", ShowProfile);
            Bind("Home/WalletButton", ShowShop);
            Bind("Home/NoAds", ShowNoAds);
            Bind("Shop/Close", CloseShop);
            Bind("Shop/Scroll/Viewport/Content/Free/Buy", ClaimDailyHint);
            Bind("Shop/Scroll/Viewport/Content/HintOffer/Buy", BuyHint);
            Bind("Settings/Close", CloseSettings);
            Bind("Settings/Scroll/Viewport/Content/Sound", () => { sound = !sound; Save(); RefreshSettings(); });
            Bind("Settings/Scroll/Viewport/Content/Music", () => { musicEnabled = !musicEnabled; Save(); RefreshSettings(); PlayMusic(); });
            Bind("Settings/Scroll/Viewport/Content/SelectMusic", () => { musicPanel.SetActive(true); RefreshMusic(); });
            Bind("Settings/Scroll/Viewport/Content/Achievements", ShowAchievements);
            Bind("Settings/Scroll/Viewport/Content/Album", ShowAlbum);
            Bind("Settings/Scroll/Viewport/Content/SaveInfo", () => ShowDialog("Game Data", "Your progress, coins, hints and profile are saved automatically.\nYou can leave a level and continue later.", "OK", null));
            Bind("Settings/Scroll/Viewport/Content/NoAds", ShowNoAds);
            Bind("MusicPanel/Close", () => musicPanel.SetActive(false));
            for (var i = 0; i < musicTracks.Length; i++)
            {
                var index = i;
                Bind("MusicPanel/Card/Track" + i, () => { music = index; musicEnabled = true; Save(); PlayMusic(); RefreshMusic(); RefreshSettings(); });
            }
            Bind("Profile/Close", () => profile.SetActive(false));
            Bind("Profile/Card/Save", SaveProfile);
            Bind("Profile/Card/AvatarTab", () => { frameTab = false; RefreshProfile(); ResetProfileChoicesScroll(); });
            Bind("Profile/Card/FrameTab", () => { frameTab = true; RefreshProfile(); ResetProfileChoicesScroll(); });
            for (var i = 0; i < Mathf.Max(avatarSprites.Length, ProfileFrameCount); i++)
            {
                var index = i;
                Bind("Profile/Card/ChoicesScroll/Viewport/Choices/Choice" + i, () => SelectProfileChoice(index));
            }
            Bind("Achievements/Close", () => achievements.SetActive(false));
            Bind("Album/Close", () => album.SetActive(false));
            for (var i = 0; i < levels.Length; i++)
            {
                var index = i;
                Bind("Album/Card/Level" + i, () => SelectLevel((OnlineLevels ? albumPage * levels.Length : 0) + index));
            }
            albumPrevious = AlbumPageButton("PreviousPage", "Previous", 24, -1);
            albumNext = AlbumPageButton("NextPage", "Next", 428, 1);
            resetZoomButton = At<UnityEngine.UI.Button>("Play/Zoom");
            widthControls = new[] { (RectTransform)backButton.transform, At<RectTransform>("Play/LifeBadge"), (RectTransform)resetZoomButton.transform };
            widthControlPositions = Array.ConvertAll(widthControls, rect => rect.anchoredPosition);
            playLayout = play.GetComponent<DifferencePlayLayout>();
            Bind("Play/Zoom", () => upperImage.GetComponent<DifferenceBoard>().ResetViewAnimated());
            resetZoomButton.gameObject.SetActive(false);
            toast = noticeLabel.transform.parent.GetComponent<CommonToast>();
            if (!toast) toast = noticeLabel.transform.parent.gameObject.AddComponent<CommonToast>();
            BindFigmaDesign();
            BindJourney();
            BindProfileDesign();
            BindPauseDesign();
            var lifeBadge = play.transform.Find("LifeBadge");
            lifeAnimation = lifeBadge ? lifeBadge.GetComponentInChildren<Spine.Unity.SkeletonGraphic>(true) : null;
            if (lifeAnimation)
            {
                lifeAnimation.Initialize(false);
                var data = lifeAnimation.SkeletonDataAsset.GetSkeletonData(false);
                if (data.FindAnimation("xin") == null || data.FindAnimation("xin2") == null)
                    throw new InvalidOperationException("The life Spine asset is missing the xin or xin2 animation.");
                lifeLossDuration = data.FindAnimation("xin2").Duration;
                lifeAnimation.UnscaledTime = true;
                PlayLifeAnimation(false);
            }
            interactionsBound = true;
        }

        public override void OnOpen(object userData)
        {
            if (!interactionsBound) return;
            CancelStartupData();
            CloseAds();
            ClosePauseOverlays();
            CancelRemoteLoad();
            CancelEndAnimation();
            tabsSliding = tabsDragging = false; currentPage = null;
            base.OnOpen(userData);
            InitializeJourney();
            completed = Mathf.Clamp(GetInt("Completed", 0), 0, LevelLimit);
            coins = Mathf.Clamp(GetInt("Coins", 10), 0, 1000000);
            hints = Mathf.Clamp(GetInt("Hints", 5), 0, 10000);
            avatar = Mathf.Clamp(GetInt("Avatar", 0), 0, avatarSprites.Length - 1);
            frame = Mathf.Clamp(GetInt("Frame", 0), 0, ProfileFrameCount - 1);
            music = Mathf.Clamp(GetInt("MusicTrack", 0), 0, musicTracks.Length - 1);
            ownedAvatars = GetInt("OwnedAvatars", 249);
            ownedFrames = GetInt("OwnedFrames", 15) | 15;
            if (!ProfileFrameUnlocked(frame)) frame = 0;
            sound = GameApp.Setting.GetBool(Key + "Sound", true);
            musicEnabled = GameApp.Setting.GetBool(Key + "Music", true);
            gmUnlimitedHints = GameApp.Setting.GetBool(Key + "GmUnlimitedHints", false);
            gmGesture.Reset(); CloseGmPanel();
            vibration = GameApp.Setting.GetBool(Key + "Vibration", true);
            nickname = GameApp.Setting.GetString(Key + "Nickname", "Player_1001");
            giftDate = GameApp.Setting.GetString(Key + "GiftDate", "");
            totalFound = Mathf.Max(0, GetInt("TotalFound", 0)); perfect = Mathf.Max(0, GetInt("Perfect", 0));
            settled = false; Round = null; pendingHint = -1;
            ShowHomeInternal(); PlayMusic(); OpenAds();
            if (ReferenceEquals(userData, StartupDataMarker)) StopTutorial();
            else InitializeTutorial();
        }

        void AlignWideScreenControls()
        {
            for (var i = 0; i < widthControls.Length; i++)
            {
                var rect = widthControls[i];
                var extraWidth = Mathf.Max(0, ((RectTransform)rect.parent).rect.width - 1179);
                var anchor = Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x);
                var position = widthControlPositions[i];
                position.x -= (anchor - .5f) * extraWidth * (1 - wideScreenControlMovement);
                rect.anchoredPosition = position;
            }
        }

        void FitSafeArea(Rect area)
        {
            stage.anchorMin = Vector2.zero;
            stage.anchorMax = Vector2.one;
            stage.pivot = Vector2.one * .5f;
            stage.localScale = Vector3.one;
            stage.localRotation = Quaternion.identity;
            stage.sizeDelta = stage.anchoredPosition = Vector2.zero;
            if (safeStretchPages == null)
            {
                var stretch = new[] { "Home", "Profile", "FigmaHome", "FigmaLoading", "FigmaJourney", "FigmaVictory",
                    "FigmaSettings", "FigmaFail", "FigmaHint", "FigmaPause", "FigmaLeave" };
                var fixedPages = new[] { "Shop", "Ranking", "Settings", "Achievements", "Album", "Modal", "MusicPanel" };
                safeStretchPages = new RectTransform[stretch.Length];
                safeFixedPages = new RectTransform[fixedPages.Length];
                for (var i = 0; i < stretch.Length; i++) safeStretchPages[i] = stage.Find(stretch[i]) as RectTransform;
                for (var i = 0; i < fixedPages.Length; i++) safeFixedPages[i] = stage.Find(fixedPages[i]) as RectTransform;
                safeToast = stage.Find("Toast") as RectTransform;
            }
            foreach (var page in safeStretchPages)
            {
                if (!page) continue;
                page.anchorMin = area.min; page.anchorMax = area.max;
                page.sizeDelta = Vector2.zero;
                page.anchoredPosition = new Vector2(page.gameObject == home ? page.anchoredPosition.x : 0, 0);
            }
            foreach (var page in safeFixedPages)
            {
                if (!page) continue;
                // Preserve design-space pivots and sizes used by popup animations and legacy controls.
                page.anchorMin = page.anchorMax = area.center + Vector2.Scale(page.pivot - Vector2.one * .5f,
                    new Vector2(page.rect.width / stage.rect.width, page.rect.height / stage.rect.height));
                var sliding = page.gameObject == shop || page.gameObject == ranking;
                page.anchoredPosition = new Vector2(sliding ? page.anchoredPosition.x : 0, 0);
            }
            var nav = (RectTransform)navigation.transform;
            nav.anchorMin = nav.anchorMax = new Vector2(area.center.x, area.yMin) +
                new Vector2((nav.pivot.x - .5f) * nav.rect.width / stage.rect.width, nav.pivot.y * nav.rect.height / stage.rect.height);
            nav.anchoredPosition = Vector2.zero;
            if (safeToast) safeToast.anchorMin = safeToast.anchorMax = new Vector2(area.center.x, area.yMax);
        }

        void FitScreenBackgrounds(RectTransform root)
        {
            if (screenBackgrounds == null)
            {
                var paths = new[]
                {
                    "Play/Background", "FigmaHome/Artwork", "FigmaLoading/Background", "FigmaJourney/Background", "Home/Beach",
                    "Shop/Background", "Ranking/Background", "Settings/Background", "FigmaVictory/Bg",
                    "Profile/Scrim", "MusicPanel/Scrim", "Achievements/Scrim", "Album/Scrim", "Modal/Scrim",
                    "FigmaSettings/Scrim", "FigmaFail/Scrim", "FigmaHint/Scrim", "FigmaPause/Scrim", "FigmaLeave/Scrim"
                };
                screenBackgrounds = new UnityEngine.UI.Image[paths.Length];
                screenBackgroundAspects = new float[paths.Length];
                for (var i = 0; i < paths.Length; i++)
                {
                    var image = stage.Find(paths[i])?.GetComponent<UnityEngine.UI.Image>();
                    screenBackgrounds[i] = image;
                    if (!image) continue;
                    var fitter = image.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                    if (fitter && image.sprite) screenBackgroundAspects[i] = fitter.aspectRatio;
                    if (fitter) fitter.enabled = false;
                }
            }
            for (var i = 0; i < screenBackgrounds.Length; i++)
            {
                var image = screenBackgrounds[i];
                if (!image) continue;
                var background = image.rectTransform;
                var parent = background.parent;
                var min = parent.InverseTransformPoint(root.TransformPoint(root.rect.min));
                var max = parent.InverseTransformPoint(root.TransformPoint(root.rect.max));
                var width = (max.x - min.x) / Mathf.Max(.001f, Mathf.Abs(background.localScale.x));
                var height = (max.y - min.y) / Mathf.Max(.001f, Mathf.Abs(background.localScale.y));
                var aspect = screenBackgroundAspects[i];
                if (aspect > 0) { width = Mathf.Max(width, height * aspect); height = width / aspect; }
                background.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                background.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                var center = root.TransformPoint(root.rect.center);
                // Backgrounds fill the screen while their page content stays inside the safe area.
                if (parent == home.transform || parent == shop.transform || parent == ranking.transform)
                    center += stage.TransformVector(new Vector3(((RectTransform)parent).anchoredPosition.x, 0, 0));
                background.position = center - background.TransformVector(background.rect.center);
            }
        }

        void LateUpdate()
        {
            if (!interactionsBound) return;
            PollGmGesture();
            RefreshGmWindow();
            SampleAnalytics();
            UpdateRewardEntryAnalytics();
            UpdateAdsVisibility();
            if (AdInputBlocked) return;
            resetZoomButton.gameObject.SetActive(upperImage.transform.localScale.x > 1.0001f);
            resetZoomButton.interactable = !PauseInputBlocked && !HintActive && !upperImage.GetComponent<DifferenceBoard>().IsResetting;
            var root = (RectTransform)transform;
            var area = Screen.safeArea;
            // Canvas layout can be zero-sized while the form is being attached or resized.
            if (root.rect.width <= 0 || root.rect.height <= 0 || stage.rect.width <= 0 || stage.rect.height <= 0 ||
                area.width <= 0 || area.height <= 0) return;
            FitSafeArea(new Rect(area.x / Mathf.Max(1, Screen.width), area.y / Mathf.Max(1, Screen.height),
                area.width / Mathf.Max(1, Screen.width), area.height / Mathf.Max(1, Screen.height)));
            FitScreenBackgrounds(root);
            if (playLayout && playLayout.enabled)
            {
                if (playLayout.ApplyLayout())
                {
                    FitProgressRow(Round == null ? progressDots.Length : Round.Total);
                    upperImage.GetComponent<DifferenceBoard>().RefreshViewport();
                }
            }
            else AlignWideScreenControls();
            AdvanceDesignLoading();
            AdvanceTabTransition(Time.unscaledDeltaTime);
            AdvancePatches(Time.unscaledDeltaTime);
            AdvanceTutorial();
            if (Input.GetMouseButton(0) || Input.touchCount > 0 || Input.mouseScrollDelta.sqrMagnitude > 0) hintIdle = 0;
            if (!testing) AdvanceHintGuide(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (HandlePauseEscape()) return;
                if (hintDesign && hintDesign.activeSelf) CloseDesignHint();
                else if (figmaResultActive) CloseResultToHome();
                else if (modal.activeSelf) { if (modalClose.gameObject.activeSelf) modal.SetActive(false); }
                else if (profile.activeSelf) profile.SetActive(false);
                else if (musicPanel.activeSelf) musicPanel.SetActive(false);
                else if (album.activeSelf) album.SetActive(false);
                else if (achievements.activeSelf) achievements.SetActive(false);
                else if (settings.activeSelf) CloseSettings();
                else if (shop.activeSelf && shopReturnsToPlay) CloseShop();
                else if (JourneyVisible) ShowHome();
                else if (play.activeSelf) ShowPause();
                else ShowHome();
            }
        }

        void ShowPage(GameObject page)
        {
            journeyVisible = false;
            ClosePauseOverlays();
            PlayMusicForPage(page);
            var wasDragging = tabsDragging;
            tabsDragging = false;
            if (play.activeSelf && page != play) { SaveRound(); CancelEndAnimation(); ClearFoundFeedback(); }
            if (page != shop) shopReturnsToPlay = false;
            modal.SetActive(false); settings.SetActive(false); profile.SetActive(false); album.SetActive(false); achievements.SetActive(false); musicPanel.SetActive(false);
            navigation.SetActive(false);
            CloseDesignHint();
            RefreshWallet();
            if (page == currentPage) { UpdateFigmaDesign(page); if (wasDragging) StartTabTransition(); return; }
            var animate = !testing && currentPage && currentPage != play && page != play && isActiveAndEnabled;
            currentPage = page;
            selectedTab = page == shop ? 0 : page == ranking ? 2 : 1;
            play.SetActive(page == play);
            if (!animate) { FinishTabTransition(); UpdateFigmaDesign(page); return; }
            StartTabTransition();
            UpdateFigmaDesign(page);
        }

        void StartTabTransition()
        {
            tabStartPosition = tabPosition; highlightStartX = highlightX; tabElapsed = 0; tabsSliding = true;
            tabDuration = Mathf.Clamp(TabSlideDuration * Mathf.Abs(selectedTab - tabPosition), .08f, TabSlideDuration);
            for (var i = 0; i < 3; i++)
            {
                tabPages[i].gameObject.SetActive(true);
                tabPageInput[i].interactable = tabPageInput[i].blocksRaycasts = false;
            }
            LayoutTabs();
        }

        internal bool CanSwipeTabs => !AdInputBlocked && !JourneyVisible && navigation.activeInHierarchy && !play.activeSelf && !tabsSliding &&
            !modal.activeSelf && !settings.activeSelf && !profile.activeSelf && !album.activeSelf &&
            !achievements.activeSelf && !musicPanel.activeSelf;

        internal bool BeginTabDrag()
        {
            if (!CanSwipeTabs || tabsDragging) return false;
            StartTabTransition();
            tabsSliding = false; tabsDragging = true;
            return true;
        }

        internal void DragTabs(float distance)
        {
            if (AdInputBlocked) { CancelTabDrag(); return; }
            if (!tabsDragging) return;
            if (!CanSwipeTabs) { CancelTabDrag(); return; }
            tabPosition = Mathf.Clamp(selectedTab - distance / stage.rect.width,
                Mathf.Max(0, selectedTab - 1), Mathf.Min(2, selectedTab + 1));
            highlightX = tabPosition * 206;
            MoveTabPages();
        }

        internal void EndTabDrag()
        {
            if (AdInputBlocked) { CancelTabDrag(); return; }
            if (!tabsDragging) return;
            var offset = tabPosition - selectedTab;
            var target = Mathf.Abs(offset) >= .15f ? selectedTab + (offset > 0 ? 1 : -1) : selectedTab;
            if (target == selectedTab) { tabsDragging = false; StartTabTransition(); }
            else if (target == 0) ShowShop();
            else if (target == 1) ShowHome();
            else ShowPage(ranking);
        }

        internal void CancelTabDrag()
        {
            if (tabsDragging) FinishTabTransition();
        }

        void AdvanceTabTransition(float deltaTime)
        {
            if (!tabsSliding) return;
            tabElapsed += deltaTime;
            var t = Mathf.Clamp01(tabElapsed / tabDuration);
            var eased = 1 - Mathf.Pow(1 - t, 3);
            tabPosition = Mathf.Lerp(tabStartPosition, selectedTab, eased);
            highlightX = Mathf.Lerp(highlightStartX, selectedTab * 206, eased);
            MoveTabPages();
            if (t >= 1) FinishTabTransition();
        }

        void FinishTabTransition()
        {
            tabsSliding = tabsDragging = false; tabPosition = selectedTab; highlightX = selectedTab * 206;
            if (tabPages == null) return;
            for (var i = 0; i < 3; i++)
            {
                tabPages[i].gameObject.SetActive(tabPages[i].gameObject == currentPage);
                tabPageInput[i].interactable = tabPageInput[i].blocksRaycasts = true;
            }
            LayoutTabs();
        }

        RectTransform CreateTabPlate(string name, Color color)
        {
            var plate = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            plate.layer = navigation.layer;
            var rect = (RectTransform)plate.transform;
            rect.SetParent(navigation.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            var image = plate.GetComponent<UnityEngine.UI.Image>();
            var source = shopButton.GetComponent<UnityEngine.UI.Image>();
            image.sprite = source.sprite; image.type = source.type;
            image.pixelsPerUnitMultiplier = 4;
            image.color = color; image.raycastTarget = false;
            return rect;
        }

        void MoveTabPages()
        {
            tabHighlight.anchoredPosition = new Vector2(highlightX, 24);
            for (var i = 0; i < 3; i++)
                tabPages[i].anchoredPosition = new Vector2((i - tabPosition) * stage.rect.width, 0);
        }

        void LayoutTabs()
        {
            MoveTabPages();
            var x = 0f;
            for (var i = 0; i < 3; i++)
            {
                var weight = i == selectedTab ? 1 : 0;
                var width = 206 + 102 * weight;
                var tab = tabButtons[i];
                tab.anchoredPosition = new Vector2(x, 24 * weight);
                tab.sizeDelta = new Vector2(width, 96 + 24 * weight);
                tabBackgrounds[i].anchoredPosition = new Vector2(x, 0);
                tabBackgrounds[i].sizeDelta = new Vector2(width, 96);
                var icon = (RectTransform)tab.Find("Icon");
                icon.localScale = Vector3.one * (1 + .1f * weight);
                icon.anchoredPosition = new Vector2((width - icon.sizeDelta.x * icon.localScale.x) / 2, -3);
                var label = tab.Find("Label").GetComponent<UnityEngine.UI.Text>();
                label.rectTransform.sizeDelta = new Vector2(width - 10, 31);
                label.color = new Color(1, 1, 1, weight);
                label.gameObject.SetActive(i == selectedTab);
                x += width;
            }
        }

        public void ShowHome()
        {
            if (AdInputBlocked) return;
            if (tutorialPending && (Round == null || !Round.Finished)) return;
            ShowHomeInternal();
        }

        void ShowHomeInternal()
        {
            ExitJourneyMode();
            var returningFromPlay = currentPage == play && !IsLoadingLevel;
            var returnStarted = Time.realtimeSinceStartupAsDouble;
            CancelRemoteLoad();
            CancelEndAnimation();
            CloseFigmaResult();
            SaveRound();
            Round = null; settled = false;
            var saved = SavedLevel();
            level = saved >= 0 ? saved : Mathf.Min(completed, LevelLimit - 1);
            ShowPage(home);
            startLabel.text = saved == level ? "Continue · Level " + (level + 1) : completed == LevelLimit ? "Level Album" : "Level " + (level + 1);
            designStartButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text = "Level " + (completed == LevelLimit && saved < 0 ? 1 : level + 1);
            homeAvatar.sprite = avatarSprites[avatar]; RefreshHomeProfileDesign();
            if (returningFromPlay && AnalyticsEnabled) StartCoroutine(TrackHomeReturn(returnStarted));
        }

        public void StartLevel()
        {
            if (AdInputBlocked) return;
            if (!journeyPlaying && !testing && completed == 0 && !tutorialPending && !GameApp.Setting.GetBool(Key + "TutorialComplete", false))
            { InitializeTutorial(); return; }
            if (ActiveCompleted == LevelLimit && home.activeSelf && SavedLevel() < 0) { BeginLevel(0, false); return; }
            BeginLevel(level, true);
        }

        public void SelectLevel(int index)
        {
            if (AdInputBlocked) return;
            if (index < 0 || index >= LevelLimit || index > ActiveCompleted) return;
            BeginLevel(index, true);
        }

        void BeginLevel(int index, bool resume, bool isRetry = false)
        {
            CancelRemoteLoad();
            BeginAnalyticsLoad();
            CloseFigmaResult();
            loadingStarted = Time.unscaledTime;
            if (OnlineLevels && (remoteLevel == null || loadedApiUrl != LevelUrl(index)))
            {
                SaveRound(); ClearFoundFeedback(); CancelEndAnimation(); Round = null;
                level = index; ShowPage(home);
                var request = new DifferenceRemoteLevel();
                loadingLevel = request;
                startButton.interactable = false;
                UpdateFigmaDesign(currentPage);
                StartCoroutine(LoadRemoteLevel(request, index, resume, isRetry));
                return;
            }
            if (testing) { OpenLevel(index, resume, isRetry); return; }
            SaveRound(); ClearFoundFeedback(); CancelEndAnimation(); Round = null;
            localLoading = true; UpdateFigmaDesign(currentPage);
            localLoadRoutine = StartCoroutine(LoadLocalLevel(index, resume, isRetry));
        }

        void OpenLevel(int index, bool resume, bool isRetry = false)
        {
            CancelEndAnimation(); level = index;
            ClearFoundFeedback();
            ConfigureLevel();
            roundContent = UsingRemoteLevel ? CurrentLevel.contentKey : "";
            var mask = 0; var lives = 3; roundMistakes = 0; pendingHint = -1;
            var restored = resume && SavedLevel() == level &&
                GameApp.Setting.GetString(RoundSettingKey("RoundContent"), "") == roundContent;
            if (restored)
            { mask = GetInt("RoundMask", 0); lives = GetInt("RoundLives", 3); roundMistakes = Mathf.Max(3 - lives, GetInt("RoundMistakes", 0)); pendingHint = GetInt("RoundHint", -1); }
            Round = new DifferenceRound(spots, ImageAspect, mask, lives);
            roundAdOperation = Guid.NewGuid().ToString("N");
            PlayLifeAnimation(false);
            if (pendingHint < 0 || pendingHint >= Round.Total || Round.IsFound(pendingHint) || Round.Finished) pendingHint = -1;
            settled = Round.Finished;
            OpenAnalyticsRound(restored, isRetry);
            ShowPage(play);
            upperImage.GetComponent<DifferenceBoard>().ResetView();
            crossTop.gameObject.SetActive(false); crossBottom.gameObject.SetActive(false);
            for (var i = 0; i < topRings.Length; i++)
            {
                var found = i < Round.Total && Round.IsFound(i);
                topRings[i].gameObject.SetActive(found); bottomRings[i].gameObject.SetActive(found);
                topRings[i].transform.localScale = bottomRings[i].transform.localScale = Vector3.one;
                if (found) { ShowFoundRing(topRings[i], false); ShowFoundRing(bottomRings[i], false); }
            }
            levelLabel.text = "Level " + (level + 1);
            ResetProgressDots();
            UpdateHUD(); SaveRound();
            OpenTutorial(restored);
            if (Round.Finished) ShowRoundResult(false, 0);
            else Notice(Round.Count > 0 ? "Level progress restored." : "Find " + Round.Total + " differences.", 2.5f);
            RestoreHint();
            UpdateAdsVisibility();
            FinishAnalyticsLoad();
        }

        void ConfigureLevel()
        {
            var data = CurrentLevel;
            EnsureSpotCapacity(data.regions.Length);
            upperImage.sprite = data.changedOnTop ? data.changed : data.original;
            lowerImage.sprite = data.changedOnTop ? data.original : data.changed;
            spots = new DifferenceSpot[data.regions.Length];
            for (var i = 0; i < differencePatches.Length; i++)
            {
                // Both boards show complete images; retain these hidden crops only for feedback setup.
                differencePatches[i].SetActive(false);
                if (i >= data.regions.Length) continue;
                var r = data.regions[i];
                var radius = Mathf.Max(23f / 600, new Vector2(r.z, r.w / ImageAspect).magnitude * .5f);
                spots[i] = data.hitSpots != null ? data.hitSpots[i] : new DifferenceSpot(r.x, r.y, radius);
                var cropped = data.croppedPatches != null;
                var changedBoard = data.changedOnTop ? upperImage : lowerImage;
                var originalBoard = data.changedOnTop ? lowerImage : upperImage;
                var patch = ConfigurePatch(patchImages[i], changedBoard, r,
                    cropped ? data.croppedPatches[i] : data.changed, cropped);
                patch.SetSiblingIndex(i);
                var originalFlash = ConfigurePatch(originalPatchImages[i], changedBoard, r, data.original, false);
                var changedFlash = ConfigurePatch(changedFlashImages[i], originalBoard, r, patchImages[i].sprite, cropped);
                originalFlash.SetSiblingIndex((data.changedOnTop ? topRings : bottomRings)[0].transform.GetSiblingIndex());
                changedFlash.SetSiblingIndex((data.changedOnTop ? bottomRings : topRings)[0].transform.GetSiblingIndex());
                originalFlash.gameObject.SetActive(false); changedFlash.gameObject.SetActive(false);
                patchElapsed[i] = -1;
                foreach (var marker in new[] { topRings[i], bottomRings[i] })
                {
                    SetBoardRegion(marker.rectTransform, data.markerRegions != null ? data.markerRegions[i] :
                        new Vector4(spots[i].x, spots[i].y, spots[i].radius * 2, spots[i].radius * 2 * ImageAspect));
                }
            }
        }

        public void ClickImage(float x, float y, bool fromLower = false)
        {
            if (AdInputBlocked || PauseInputBlocked) return;
            if (!TutorialAllowsClick(x, y)) return;
            if (!play.activeSelf || modal.activeSelf || settings.activeSelf || (hintDesign && hintDesign.activeSelf) || IsLoadingLevel || Round == null || settled) return;
            hintIdle = 0; hintHand.Show(false);
            var source = fromLower ? lowerImage.rectTransform : upperImage.rectTransform;
            var point = new Vector3(source.rect.xMin + x * source.rect.width, source.rect.yMax - y * source.rect.height);
            var origin = source.TransformPoint(point);
            var hinted = HintActive;
            if (hinted)
            {
                if (!hintSpotlight.Ready || float.IsNaN(x) || float.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1) return;
                var spot = spots[hintSpotlight.TargetIndex];
                var distance = (Vector2)play.transform.InverseTransformVector(source.TransformVector(Vector2.Scale(new Vector2(x - spot.x, y - spot.y), source.rect.size)));
                if (distance.sqrMagnitude > DifferenceHintSpotlight.Radius * DifferenceHintSpotlight.Radius) return;
                x = spot.x; y = spot.y;
            }
            SampleAnalytics();
            var result = Round.Click(x, y);
            if (result == DifferenceRound.Miss) ShowCross(x, y);
            if (hinted && result >= 0) { pendingHint = -1; hintSpotlight.Dismiss(); }
            ApplyResult(result, origin);
        }

        public void UseHint()
        {
            if (AdInputBlocked || PauseInputBlocked) return;
            if (TutorialRunning && (tutorialStep != TutorialStep.HintButton || Time.unscaledTime < tutorialShowAt)) return;
            if (!play.activeSelf || modal.activeSelf || settings.activeSelf || (hintDesign && hintDesign.activeSelf) || IsLoadingLevel || Round == null || Round.Finished || settled || HintActive) return;
            if (pendingHint >= 0) { RestoreHint(true); return; }
            if (!UnlimitedHints && hints == 0) { RefreshHintRewardLabel(); hintDesign.SetActive(true); hintDesign.transform.SetAsLastSibling(); DifferencePopupMotion.SortAboveGameParticles(hintDesign); UpdateAdsVisibility(); return; }
            var result = Round.Hint();
            if (result < 0) return;
            pendingHint = result;
            CountAnalyticsHint();
            GameApp.Setting.SetInt(RoundSettingKey("RoundHint"), pendingHint);
            if (!UnlimitedHints) hints--;
            Save();
            RestoreHint(true);
            UpdateHUD();
            TutorialHintUsed(result);
        }

        void RestoreHint(bool playSound = false)
        {
            if (PauseInputBlocked || pendingHint < 0 || !play.activeSelf || Round == null || Round.Finished || HintActive) return;
            hintIdle = 0; hintHand.Show(false);
            var spot = spots[pendingHint];
            hintSpotlight.Show(upperImage.GetComponent<DifferenceBoard>(), upperImage.rectTransform, lowerImage.rectTransform,
                new Vector2(spot.x, spot.y), pendingHint);
            // Keep pause reachable while the hint scrim blocks the rest of the board.
            backButton.transform.SetAsLastSibling();
            if (playSound && HintActive) PlayTone(tipsSound);
        }

        void AdvanceHintGuide(float deltaTime)
        {
            if (tutorialPending) { hintHand.Show(false); return; }
            var available = play.activeSelf && Round != null && !Round.Finished && !settled && (UnlimitedHints || hints > 0) &&
                !PauseInputBlocked && !HintActive && !modal.activeSelf && !settings.activeSelf && !(hintDesign && hintDesign.activeSelf) && !IsLoadingLevel;
            if (!available) hintIdle = 0;
            else hintIdle += Mathf.Max(0, deltaTime);
            hintHand.Show(available && hintIdle >= 8);
        }

        void ApplyResult(int result, Vector3? worldOrigin = null)
        {
            if (settled || result == DifferenceRound.Ignored) return;
            // GM calls this method directly without a real pointer origin.
            if (!worldOrigin.HasValue) analyticsSuppressedRound = true;
            PlayResultVibration(result);
            if (result >= 0)
            {
                TutorialFound(result);
                totalFound++;
                TrackAnalyticsPart(result);
                topRings[result].gameObject.SetActive(true); bottomRings[result].gameObject.SetActive(true);
                ShowFoundRing(topRings[result], true); ShowFoundRing(bottomRings[result], true);
                if (!Round.Complete)
                {
                    patchElapsed[result] = 0;
                    originalPatchImages[result].color = changedFlashImages[result].color = new Color(1, 1, 1, 0);
                    originalPatchImages[result].transform.parent.gameObject.SetActive(true);
                    changedFlashImages[result].transform.parent.gameObject.SetActive(true);
                    MaskPatchFlash(originalPatchImages[result], CurrentLevel.changedOnTop ? topRings[result] : bottomRings[result]);
                    MaskPatchFlash(changedFlashImages[result], CurrentLevel.changedOnTop ? bottomRings[result] : topRings[result]);
                }
                var slot = Round.Count - 1;
                var foundRound = Round;
                pendingProgress[slot] = true;
                DifferenceFoundFlight.Launch((RectTransform)play.transform, worldOrigin ?? topRings[result].transform.position,
                    progressDots[slot].rectTransform, () =>
                    {
                        if (Round != foundRound || !play.activeSelf) return;
                        pendingProgress[slot] = false;
                        UpdateHUD();
                    }, foundFlightPrefab);
                PlayTone(foundSound);
            }
            else { roundMistakes++; PlayLifeAnimation(true); PlayTone(missSound); StartCoroutine(Pulse(heartsLabel.transform)); }
            if (Round.Finished) FreezeAnalyticsRound();
            UpdateHUD(); SaveRound(); Save();
            if (!Round.Finished) return;
            settled = true;
            if (Round.Complete)
            {
                if (ActiveCompleted <= level) { RecordLevelCompletion(); if (roundMistakes == 0) perfect++; }
                coins += LevelCoinReward;
                ClearRound(); Save();
                TrackAnalyticsResult(true);
                endRoutine = StartCoroutine(EndRound(true, LevelCoinReward));
            }
            else { TrackAnalyticsResult(false); endRoutine = StartCoroutine(EndRound(false, 0)); }
        }

        void ShowFoundRing(UnityEngine.UI.Image marker, bool animate)
        {
            marker.enabled = false;
            var outline = marker.GetComponentInChildren<DifferenceFoundRing>(true);
            if (!outline)
            {
                var node = new GameObject("Outline", typeof(RectTransform), typeof(DifferenceFoundRing));
                node.layer = marker.gameObject.layer;
                node.transform.SetParent(marker.transform, false);
                outline = node.GetComponent<DifferenceFoundRing>();
                outline.rectTransform.anchorMin = Vector2.zero;
                outline.rectTransform.anchorMax = Vector2.one;
                outline.rectTransform.offsetMin = outline.rectTransform.offsetMax = Vector2.zero;
                outline.raycastTarget = false;
            }
            var showSpine = useSpineFoundRing && foundRingSpine;
            outline.style = foundRingStyle;
            outline.sourceImageWidth = CurrentLevel.markerRegions != null ? upperImage.sprite.rect.width : 0;
            outline.gameObject.SetActive(!showSpine);
            outline.enabled = !showSpine;
            if (!showSpine) outline.Play(animate);
            foreach (var existing in marker.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>(true))
                existing.gameObject.SetActive(showSpine);
            if (showSpine)
            {
                var ring = marker.GetComponentInChildren<Spine.Unity.SkeletonGraphic>(true);
                if (!ring)
                {
                    ring = Spine.Unity.SkeletonGraphic.NewSkeletonGraphicGameObject(foundRingSpine, marker.transform, foundRingMaterial);
                    ring.name = "FoundSpine";
                    ring.gameObject.layer = marker.gameObject.layer;
                    ring.raycastTarget = false;
                    ring.UnscaledTime = true;
                }
                ring.rectTransform.anchoredPosition = Vector2.zero;
                var data = foundRingSpine.GetSkeletonData(false);
                var diameter = Mathf.Min(marker.rectTransform.rect.width, marker.rectTransform.rect.height);
                ring.transform.localScale = Vector3.one * (diameter / Mathf.Max(.001f, Mathf.Max(data.Width, data.Height)));
                ring.AnimationState.SetAnimation(0, animate ? "a2" : "a1", false);
                if (animate) ring.AnimationState.AddAnimation(0, "a1", false, 0);
            }
            if (!animate || !foundRingEffectPrefab) return;
            var effect = Instantiate(foundRingEffectPrefab, marker.transform, false);
            effect.name = "FoundRingParticles";
            effect.transform.localPosition = Vector3.zero;
            var canvas = marker.GetComponentInParent<Canvas>();
            var duration = 0f;
            foreach (var child in effect.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = marker.gameObject.layer;
            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.loop = false; main.useUnscaledTime = true;
                duration = Mathf.Max(duration, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = canvas.sortingLayerID;
                renderer.sortingOrder = canvas.sortingOrder + 1;
                particle.Play(false);
            }
            StartCoroutine(ClearRingParticles(effect, duration + .1f));
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            foreach (var markers in new[] { topRings, bottomRings })
            {
                if (markers == null) continue;
                foreach (var marker in markers)
                {
                    if (!marker) continue;
                    var ring = marker.GetComponentInChildren<DifferenceFoundRing>(true);
                    if (!ring) continue;
                    ring.style = foundRingStyle;
                    ring.SetVerticesDirty();
                }
            }
        }
#endif

        IEnumerator ClearRingParticles(GameObject effect, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            if (effect) Destroy(effect);
        }

        static void ClearRingParticlesNow(UnityEngine.UI.Image marker)
        {
            var effect = marker.transform.Find("FoundRingParticles");
            if (!effect) return;
            effect.gameObject.SetActive(false);
            Destroy(effect.gameObject);
        }

        void PlayLifeAnimation(bool lostLife)
        {
            if (!lifeAnimation) return;
            var state = lifeAnimation.AnimationState;
            state.SetAnimation(0, lostLife ? "xin2" : "xin", false);
            if (lostLife) state.AddAnimation(0, "xin", false, 0);
        }

        IEnumerator EndRound(bool won, int reward)
        {
            var finishedRound = Round;
            var visibleLevel = level + 1;
            var resultTime = Time.unscaledTime + (won ? DifferenceFoundFlight.Duration + .06f : Mathf.Max(.25f, lifeLossDuration));
            if (won && !testing) yield return ReplayCompletedProgress();
            if (!testing) yield return new WaitForSecondsRealtime(Mathf.Max(0, resultTime - Time.unscaledTime));
            if (!play.activeSelf || Round != finishedRound) { endRoutine = null; yield break; }
            ShowRoundResult(won, reward);
            if (won)
            {
                yield return null;
                CheckWinInterstitial(finishedRound, visibleLevel);
            }
            endRoutine = null;
        }

        void ShowRoundResult(bool won, int reward)
        {
            ShowFigmaResult(won);
            if (won) StartRewardCoins(reward);
        }

        public void Revive()
        {
            if (AdInputBlocked) return;
            if (Round == null || Round.Complete || Round.Lives != 0) return;
            if (GameFrameX.Startup.Application.DifferenceAds.SdkEnabled) RequestRewardRevive();
            else CompleteRevive(true);
        }

        void CompleteRevive(bool spendCoins)
        {
            if (AdInputBlocked || Round == null || Round.Complete || Round.Lives != 0) return;
            if (spendCoins && coins < 30)
            {
                Notice("You need 30 coins to continue, or retry for free.", 2);
                return;
            }
            if (!Round.Revive()) return;
            PlayLifeAnimation(false);
            if (spendCoins) coins -= 30;
            TrackAnalyticsRevive(spendCoins);
            settled = false; modal.SetActive(false); CloseFigmaResult(); SaveRound(); Save(); UpdateHUD();
        }

        void ClearFoundFeedback()
        {
            foreach (var marker in topRings) ClearRingParticlesNow(marker);
            foreach (var marker in bottomRings) ClearRingParticlesNow(marker);
            for (var i = 0; i < patchElapsed.Length; i++)
            {
                patchElapsed[i] = -1;
            }
            foreach (var image in patchImages) image.color = Color.white;
            for (var i = 0; i < originalPatchImages.Length; i++) ResetPatchFlash(i);
            if (hintSpotlight) hintSpotlight.Cancel();
            if (hintHand) hintHand.Show(false);
            hintIdle = 0;
            DifferenceFoundFlight.Clear(play);
            Array.Clear(pendingProgress, 0, pendingProgress.Length);
        }

        void UpdateHUD()
        {
            if (Round == null) return;
            heartsLabel.text = Round.Lives.ToString();
            hintLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            hintLabel.text = UnlimitedHints ? "∞" : hints.ToString();
            var shownCount = 0;
            for (var i = 0; i < progressDots.Length; i++)
            {
                var shown = i < Round.Count && !pendingProgress[i];
                if (shown) shownCount++;
                progressDots[i].gameObject.SetActive(i < Round.Total);
                // The completion sweep owns pending dots while their flights are still arriving.
                if (shown || !Round.Complete) SyncProgressDot(progressDots[i], shown);
            }
            progressLabel.text = shownCount + " / " + Round.Total;
            RefreshWallet();
        }

        public void ShowSettings()
        {
            if (AdInputBlocked) return;
            if (hintSpotlight) hintSpotlight.Cancel();
            hintIdle = 0; if (hintHand) hintHand.Show(false);
            settings.SetActive(true); UpdateFigmaDesign(currentPage); RefreshSettings();
        }
        void CloseSettings() { if (AdInputBlocked) return; settings.SetActive(false); musicPanel.SetActive(false); achievements.SetActive(false); album.SetActive(false); UpdateFigmaDesign(currentPage); RestoreHint(); }
        void RefreshSettings()
        {
            ToggleLook("Settings/Scroll/Viewport/Content/Sound", sound);
            ToggleLook("Settings/Scroll/Viewport/Content/Music", musicEnabled);
            if (musicToggle) musicToggle.sprite = musicEnabled ? toggleOn : toggleOff;
            if (soundToggle) soundToggle.sprite = sound ? toggleOn : toggleOff;
            if (vibrationToggle) vibrationToggle.sprite = vibration ? toggleOn : toggleOff;
        }
        void ToggleLook(string path, bool enabled)
        {
            var toggle = stage.Find(path + "/Switch");
            toggle.GetComponent<UnityEngine.UI.Image>().color = enabled ? new Color(.38f,.82f,.10f) : new Color(.43f,.50f,.62f);
            ((RectTransform)toggle.Find("Knob")).anchoredPosition = new Vector2(enabled ? 37 : 3, -3);
        }
        void RefreshMusic()
        {
            for (var i = 0; i < musicTracks.Length; i++) SetText("MusicPanel/Card/Track" + i + "/Selected", i == music ? "✓" : "");
        }
        void PlayMusic()
        {
            PlayMusicForPage(currentPage);
        }

        void PlayMusicForPage(GameObject page)
        {
            musicSource.volume = BackgroundMusicVolume;
            if (!musicEnabled) { musicSource.Stop(); return; }
            var clip = page == play ? gameMusic : JourneyVisible ? journeyMusic : homeMusic;
            if (!clip) { musicSource.Stop(); return; }
            if (musicSource.clip != clip) { musicSource.Stop(); musicSource.clip = clip; }
            if (!musicSource.isPlaying) musicSource.Play();
        }

        public void ShowShop()
        {
            if (AdInputBlocked) return;
            var fromPlay = play.activeSelf;
            shopReturnsToPlay = fromPlay || (shop.activeSelf && shopReturnsToPlay);
            ShowPage(shop);
            stage.Find("Shop/Close").gameObject.SetActive(shopReturnsToPlay);
            RefreshShop();
        }
        void CloseShop()
        {
            if (AdInputBlocked) return;
            if (!shopReturnsToPlay || Round == null) { ShowHome(); return; }
            ShowPage(play); UpdateHUD();
            if (Round.Finished) ShowRoundResult(Round.Complete, Round.Complete ? LevelCoinReward : 0);
            else RestoreHint();
        }
        void RefreshShop()
        {
            var claimed = GiftClaimed(DateTime.UtcNow.ToString("yyyy-MM-dd"));
            var button = At<UnityEngine.UI.Button>("Shop/Scroll/Viewport/Content/Free/Buy");
            button.interactable = !claimed; button.GetComponentInChildren<UnityEngine.UI.Text>().text = claimed ? "Claimed" : "Daily Free Hint";
            At<UnityEngine.UI.Button>("Shop/Scroll/Viewport/Content/HintOffer/Buy").interactable = coins >= 900;
            SetText("Shop/HintCount", "My Hints  ×" + hints);
        }
        public void ClaimDailyHint()
        {
            if (AdInputBlocked) return;
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (GiftClaimed(today)) { RefreshShop(); return; }
            giftDate = today; hints++; Save(); RefreshShop();
            ShowDialog("Reward Claimed", "You received 1 hint.", "Collect", null);
        }
        public void BuyHint()
        {
            if (AdInputBlocked) return;
            if (coins < 900) return;
            coins -= 900; hints++; Save(); RefreshWallet(); RefreshShop();
            ShowDialog("Purchase Complete", "You received 1 hint.", "Continue", null);
        }
        void ShowNoAds()
        {
            var ads = GameFrameX.Startup.Application.DifferenceAds.SdkEnabled;
            var message = !ads ? "Ads are disabled in this version." :
                GameFrameX.Startup.Application.DifferenceAds.NoAds ? "Your ad-free access is active. Banner and interstitial ads are disabled.\nYou can still choose to watch rewarded videos for hints." :
                "This version includes banner and interstitial ads.\nAd-free purchases are not available yet.\nYou can choose to watch rewarded videos for hints.";
            ShowDialog("About Ads", message, "OK", null);
        }

        void ShowAchievements()
        {
            if (AdInputBlocked) return;
            achievements.SetActive(true);
            UpdateAdsVisibility();
            var values = new[] { completed, totalFound, perfect, completed };
            var goals = new[] { 1, 10, 1, levels.Length };
            SetText("Achievements/Card/Row3/Detail", OnlineLevels ? "Complete 3 levels" : "Complete all available scenes");
            for (var i = 0; i < values.Length; i++)
            {
                SetText("Achievements/Card/Row" + i + "/Progress", Mathf.Min(values[i], goals[i]) + " / " + goals[i]);
                SetText("Achievements/Card/Row" + i + "/State", values[i] >= goals[i] ? "Completed" : "In Progress");
            }
        }
        public void ShowAlbum()
        {
            if (AdInputBlocked) return;
            album.SetActive(true);
            UpdateAdsVisibility();
            albumPage = OnlineLevels ? Mathf.Min(level, ActiveCompleted) / levels.Length : 0;
            RefreshAlbum();
        }

        void RefreshAlbum()
        {
            albumPrevious.gameObject.SetActive(OnlineLevels); albumNext.gameObject.SetActive(OnlineLevels);
            albumPrevious.interactable = albumPage > 0;
            albumNext.interactable = albumPage < ActiveCompleted / levels.Length;
            var more = At<UnityEngine.UI.Text>("Album/Card/More");
            more.text = OnlineLevels ? (albumPage + 1) + " / " + (ActiveCompleted / levels.Length + 1) : "More scenes coming soon";
            more.rectTransform.anchoredPosition = new Vector2(OnlineLevels ? 200 : 40, -794);
            more.rectTransform.sizeDelta = new Vector2(OnlineLevels ? 212 : 532, 49);
            for (var i = 0; i < levels.Length; i++)
            {
                var row = stage.Find("Album/Card/Level" + i);
                var entry = (OnlineLevels ? (long)albumPage * levels.Length : 0) + i;
                row.gameObject.SetActive(entry < LevelLimit);
                if (entry >= LevelLimit) continue;
                var index = (int)entry;
                var data = OnlineLevels ? (index < LevelLimit && loadedApiUrl == LevelUrl(index) ? remoteLevel?.Level : null) : levels[i];
                row.Find("Title").GetComponent<UnityEngine.UI.Text>().text = ((long)index + 1) + "  " + (OnlineLevels ? "Online Level" : data.title);
                row.Find("Count").GetComponent<UnityEngine.UI.Text>().text = data == null ? "Tap to load level" : data.regions.Length + " differences";
                row.Find("Preview").GetComponent<UnityEngine.UI.Image>().sprite = data?.original;
                row.GetComponent<UnityEngine.UI.Button>().interactable = index <= ActiveCompleted && index < LevelLimit;
                row.Find("Status").GetComponent<UnityEngine.UI.Text>().text = index < ActiveCompleted ? "Completed · Replay" : index == ActiveCompleted ? "Start Finding" : "Locked";
            }
        }

        public void ShowDialog(string title, string body, string actionLabel, Action action, string second = null, Action secondAction = null, string third = null, Action thirdAction = null, bool terminal = false)
        {
            if (AdInputBlocked) return;
            modalAction.interactable = modalSecondary.interactable = modalTertiary.interactable = true;
            modalTitle.text = title; modalBody.text = body; modalActionLabel.text = actionLabel;
            dialogAction = action; secondaryAction = secondAction; tertiaryAction = thirdAction;
            modalSecondary.gameObject.SetActive(second != null); modalTertiary.gameObject.SetActive(third != null);
            modalSecondary.GetComponentInChildren<UnityEngine.UI.Text>().text = second;
            modalTertiary.GetComponentInChildren<UnityEngine.UI.Text>().text = third;
            modalClose.gameObject.SetActive(!terminal); resultIcon.gameObject.SetActive(terminal && Round != null && Round.Complete);
            modal.SetActive(true); modal.transform.SetAsLastSibling();
            UpdateAdsVisibility();
        }

        void RefreshWallet()
        {
            coinsLabel.text = coins.ToString(); SetText("Shop/Coins", coins.ToString());
            SetText("Profile/Wallet/Coins", coins.ToString());
            if (designCoins) designCoins.text = coins.ToString();
            SetText("Ranking/PlayerName", nickname ?? "Player_1001");
            RefreshJourneyEntry();
        }
        int GetInt(string name, int value) { return GameApp.Setting.GetInt(name.StartsWith("Round", StringComparison.Ordinal) ? RoundSettingKey(name) : Key + name, value); }
        int SavedLevel()
        {
            var saved = GetInt("RoundLevel", -1);
            if (saved < 0 || saved >= LevelLimit || saved > ActiveCompleted) return -1;
            var count = OnlineLevels
                ? remoteLevel != null && loadedApiUrl == LevelUrl(saved) ? remoteLevel.Level.regions.Length : DifferenceRound.MaxSpots
                : levels[saved].regions.Length;
            var mask = GetInt("RoundMask", 0); var lives = GetInt("RoundLives", 3);
            return count > 0 && count <= DifferenceRound.MaxSpots && mask >= 0 && mask < (1L << count) - 1 && lives >= 0 && lives <= 3 ? saved : -1;
        }
        void SaveRound()
        {
            if (Round == null || Round.Complete) return;
            SaveAnalyticsRound();
            GameApp.Setting.SetInt(RoundSettingKey("RoundLevel"), level); GameApp.Setting.SetInt(RoundSettingKey("RoundMask"), Round.FoundMask); GameApp.Setting.SetInt(RoundSettingKey("RoundLives"), Round.Lives);
            GameApp.Setting.SetInt(RoundSettingKey("RoundMistakes"), roundMistakes);
            GameApp.Setting.SetInt(RoundSettingKey("RoundHint"), pendingHint);
            GameApp.Setting.SetString(RoundSettingKey("RoundContent"), roundContent);
            GameApp.Setting.Save();
        }
        void ClearRound() { GameApp.Setting.SetInt(RoundSettingKey("RoundLevel"), -1); GameApp.Setting.Save(); }
        bool GiftClaimed(string today)
        {
            var stored = GameApp.Setting.GetString(Key + "GiftDate", "");
            if (string.CompareOrdinal(stored, giftDate) > 0) giftDate = stored;
            return string.CompareOrdinal(giftDate, today) >= 0;
        }
        void Save()
        {
            var names = new[] { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect" };
            var values = new[] { completed, coins, hints, avatar, frame, music, ownedAvatars, ownedFrames, totalFound, perfect };
            for (var i = 0; i < names.Length; i++) GameApp.Setting.SetInt(Key + names[i], values[i]);
            GameApp.Setting.SetInt(Key + "JourneyCompleted", journeyCompleted);
            GameApp.Setting.SetBool(Key + "Sound", sound); GameApp.Setting.SetBool(Key + "Music", musicEnabled); GameApp.Setting.SetBool(Key + "Vibration", vibration);
            GiftClaimed(DateTime.UtcNow.ToString("yyyy-MM-dd"));
            GameApp.Setting.SetString(Key + "Nickname", nickname); GameApp.Setting.SetString(Key + "GiftDate", giftDate);
            GameApp.Setting.Save();
        }
        void OnApplicationPause(bool paused)
        {
            if (paused) gmGesture.Reset();
            SampleAnalytics(); analyticsPaused = paused; SampleAnalytics();
            if (paused && Round != null) SaveRound();
            if (paused && AnalyticsEnabled) GameFrameX.Startup.Application.DifferenceAnalytics.Flush();
        }
        void OnApplicationFocus(bool focused) { if (!focused) gmGesture.Reset(); SampleAnalytics(); analyticsUnfocused = !focused; SampleAnalytics(); }
        public override void OnClose(bool isShutdown, object userData)
        { gmGesture.Reset(); CloseGmPanel(); CancelStartupData(); CloseAds(); ClosePauseOverlays(); SaveRound(); CancelRemoteLoad(); CancelEndAnimation(); ClearFoundFeedback(); ReleaseRemoteLevel(); FinishTabTransition(); base.OnClose(isShutdown, userData); }
        void CancelEndAnimation()
        {
            CancelRewardCoins();
            if (endRoutine != null) { StopCoroutine(endRoutine); endRoutine = null; }
        }
        public void SetTesting(bool value) { testing = value; if (value) { StopTutorial(); analyticsSuppressedRound = true; FinishTabTransition(); } }
        float ToneVolume(AudioClip clip)
        {
            return 1f;
            if (clip == buttonSound) return .12f;
            if (clip == failSound) return .09f;
            if (clip == coinCreateSound) return .26f;
            if (clip == winSound) return .28f;
            if (clip == missSound || clip == tipsSound) return .3f;
            return .4f;
        }
        void PlayTone(AudioClip clip) { if (sound && clip) audioSource.PlayOneShot(clip, ToneVolume(clip)); }
        void ShowCross(float x, float y)
        {
            foreach (var label in new[] { crossTop, crossBottom })
            {
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, 1 - y);
                label.rectTransform.pivot = new Vector2(.5f, .5f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.gameObject.SetActive(true);
                var animation = label as Spine.Unity.SkeletonGraphic;
                if (animation)
                {
                    animation.Initialize(false);
                    animation.UnscaledTime = true;
                    animation.raycastTarget = false;
                    // A new track replaces the previous miss; only the completed track hides it.
                    animation.AnimationState.SetAnimation(0, "cha", false).Complete += entry =>
                    {
                        if (animation && animation.AnimationState.GetCurrent(0) == entry)
                            animation.gameObject.SetActive(false);
                    };
                }
                else StartCoroutine(HideCross(label));
            }
        }
        IEnumerator HideCross(UnityEngine.UI.Graphic label) { yield return new WaitForSecondsRealtime(.6f); label.gameObject.SetActive(false); }
        IEnumerator Pulse(Transform target, float amount = .22f)
        {
            for (var elapsed = 0f; elapsed < .32f; elapsed += Time.unscaledDeltaTime)
            { target.localScale = Vector3.one * (1 + amount * Mathf.Sin(elapsed / .32f * Mathf.PI)); yield return null; }
            target.localScale = Vector3.one;
        }
        void Notice(string message, float seconds) { toast.Show(message, seconds); }
        T At<T>(string path) where T : Component
        {
            var found = stage.Find(path); if (!found) throw new InvalidOperationException("Missing UI node: " + path);
            return found.GetComponent<T>();
        }
        void PlayButtonSound() { if (!AdInputBlocked) PlayTone(buttonSound); }
        void Bind(string path, Action action) { At<UnityEngine.UI.Button>(path).onClick.AddListener(() => { if (AdInputBlocked) return; action(); UpdateAdsVisibility(); }); }
        void SetText(string path, string value) { At<UnityEngine.UI.Text>(path).text = value; }
    }
}
#endif
