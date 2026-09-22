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
        public Sprite original, changed;
        public Vector4[] regions;
        [NonSerialized] public string contentKey;
        [NonSerialized] public Sprite[] croppedPatches;
        [NonSerialized] public DifferenceSpot[] hitSpots;
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
        public UnityEngine.UI.Image[] progressDots, topRings, bottomRings, patchImages, avatars;
        public GameObject[] differencePatches;
        public DifferenceSpot[] spots;
        public DifferenceLevel[] levels;
        public Sprite[] avatarSprites;
        public Sprite progressQuestion, progressCheck, hintHandSprite;
        public Material foundFlightMaterial, hintSpotlightMaterial;
        public UnityEngine.UI.Image upperImage, lowerImage, profileAvatar, homeAvatar, profileFrame, homeFrame, resultIcon;
        public UnityEngine.UI.InputField nicknameInput;
        public UnityEngine.UI.Text crossTop, crossBottom;
        public AudioSource audioSource, musicSource;
        public AudioClip foundSound, missSound, winSound;
        public AudioClip[] musicTracks;
        public RectTransform[] confetti;
        public DifferenceRound Round { get; private set; }
        public int SelectedLevel => level;
        public int Hints => hints;
        public int Coins => coins;
        public int Completed => completed;
        public const float ImageAspect = 1.5f;
        public const string Key = "FindDifferences.";
        public static readonly Color[] FrameColors = { new Color(.95f,.37f,.65f), new Color(.15f,.68f,1), new Color(.71f,.31f,.95f), new Color(1,.70f,.14f), new Color(.35f,.9f,.5f), new Color(.95f,.35f,.27f) };
        int level, completed, coins, hints, avatar, frame, music, totalFound, perfect, ownedAvatars, ownedFrames;
        int draftAvatar, draftFrame, roundMistakes;
        bool sound, musicEnabled, settled, frameTab, testing, shopReturnsToPlay, interactionsBound;
        string nickname, giftDate, roundContent;
        Action dialogAction, secondaryAction, tertiaryAction;
        Coroutine noticeRoutine, endRoutine;
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
            interactionsBound = true;
            if (levels == null || levels.Length == 0 || topRings.Length != 15 || bottomRings.Length != 15)
                throw new InvalidOperationException("请执行 Tools/Find Differences/Build Game Assets");
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
            if (!stage.GetComponent<UnityEngine.UI.RectMask2D>()) stage.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            DifferenceButtonShine.Create(startButton);
            hintHand = DifferenceHintHand.Create(hintButton, hintHandSprite);
            hintSpotlight = DifferenceHintSpotlight.Create((RectTransform)play.transform, hintSpotlightMaterial);
            foreach (var popup in new[] { modal, profile, musicPanel, achievements, album })
                popup.AddComponent<DifferencePopupMotion>();
            startButton.onClick.AddListener(StartLevel);
            settingsButton.onClick.AddListener(ShowSettings);
            shopButton.onClick.AddListener(ShowShop);
            trophyButton.onClick.AddListener(() => ShowPage(ranking));
            backButton.onClick.AddListener(ShowHome);
            hintButton.onClick.AddListener(UseHint);
            modalAction.onClick.AddListener(() => { modal.SetActive(false); dialogAction?.Invoke(); });
            modalSecondary.onClick.AddListener(() => { modal.SetActive(false); secondaryAction?.Invoke(); });
            modalTertiary.onClick.AddListener(() => { modal.SetActive(false); tertiaryAction?.Invoke(); });
            modalClose.onClick.AddListener(() => modal.SetActive(false));
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
            Bind("Settings/Scroll/Viewport/Content/SaveInfo", () => ShowDialog("游戏数据", "关卡、金币、提示与个人资料会自动保存。\n退出关卡后可以继续寻找。", "知道了", null));
            Bind("Settings/Scroll/Viewport/Content/NoAds", ShowNoAds);
            Bind("MusicPanel/Close", () => musicPanel.SetActive(false));
            for (var i = 0; i < musicTracks.Length; i++)
            {
                var index = i;
                Bind("MusicPanel/Card/Track" + i, () => { music = index; musicEnabled = true; Save(); PlayMusic(); RefreshMusic(); RefreshSettings(); });
            }
            Bind("Profile/Close", () => profile.SetActive(false));
            Bind("Profile/Card/Save", SaveProfile);
            Bind("Profile/Card/AvatarTab", () => { frameTab = false; RefreshProfile(); });
            Bind("Profile/Card/FrameTab", () => { frameTab = true; RefreshProfile(); });
            for (var i = 0; i < 8; i++)
            {
                var index = i;
                Bind("Profile/Card/Choices/Choice" + i, () => { if (frameTab) draftFrame = index; else draftAvatar = index; RefreshProfile(); });
            }
            Bind("Achievements/Close", () => achievements.SetActive(false));
            Bind("Album/Close", () => album.SetActive(false));
            for (var i = 0; i < levels.Length; i++)
            {
                var index = i;
                Bind("Album/Card/Level" + i, () => SelectLevel((OnlineLevels ? albumPage * levels.Length : 0) + index));
            }
            albumPrevious = AlbumPageButton("PreviousPage", "上一页", 24, -1);
            albumNext = AlbumPageButton("NextPage", "下一页", 428, 1);
            Bind("Play/Zoom", () => { if (HintActive) return; var board = upperImage.GetComponent<DifferenceBoard>(); board.SetZoom(upperImage.transform.localScale.x > 1.01f ? 1 : 2); });
        }

        public override void OnOpen(object userData)
        {
            CancelRemoteLoad();
            CancelEndAnimation();
            tabsSliding = tabsDragging = false; currentPage = null;
            base.OnOpen(userData);
            completed = Mathf.Clamp(GetInt("Completed", 0), 0, LevelLimit);
            coins = Mathf.Clamp(GetInt("Coins", 10), 0, 1000000);
            hints = Mathf.Clamp(GetInt("Hints", 5), 0, 10000);
            avatar = Mathf.Clamp(GetInt("Avatar", 0), 0, avatarSprites.Length - 1);
            frame = Mathf.Clamp(GetInt("Frame", 0), 0, FrameColors.Length - 1);
            music = Mathf.Clamp(GetInt("MusicTrack", 0), 0, musicTracks.Length - 1);
            ownedAvatars = (GetInt("OwnedAvatars", 249) | 249) & ((1 << avatarSprites.Length) - 1);
            ownedFrames = (GetInt("OwnedFrames", 7) & ((1 << FrameColors.Length) - 1)) | 7;
            if ((ownedAvatars & (1 << avatar)) == 0) avatar = 0;
            if ((ownedFrames & (1 << frame)) == 0) frame = 0;
            sound = GameApp.Setting.GetBool(Key + "Sound", true);
            musicEnabled = GameApp.Setting.GetBool(Key + "Music", true);
            nickname = GameApp.Setting.GetString(Key + "Nickname", "Player_1001");
            giftDate = GameApp.Setting.GetString(Key + "GiftDate", "");
            totalFound = Mathf.Max(0, GetInt("TotalFound", 0)); perfect = Mathf.Max(0, GetInt("Perfect", 0));
            settled = false; Round = null; pendingHint = -1;
            ShowHome(); PlayMusic();
        }

        void LateUpdate()
        {
            var root = (RectTransform)transform;
            var area = Screen.safeArea;
            var scale = Mathf.Min(root.rect.width * area.width / Mathf.Max(1, Screen.width) / 720f,
                root.rect.height * area.height / Mathf.Max(1, Screen.height) / 1280f);
            stage.localScale = Vector3.one * scale;
            stage.anchoredPosition = new Vector2((area.center.x / Mathf.Max(1, Screen.width) - .5f) * root.rect.width,
                (area.center.y / Mathf.Max(1, Screen.height) - .5f) * root.rect.height);
            AdvanceTabTransition(Time.unscaledDeltaTime);
            AdvancePatches(Time.unscaledDeltaTime);
            if (Input.GetMouseButton(0) || Input.touchCount > 0 || Input.mouseScrollDelta.sqrMagnitude > 0) hintIdle = 0;
            if (!testing) AdvanceHintGuide(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modal.activeSelf) { if (modalClose.gameObject.activeSelf) modal.SetActive(false); }
                else if (profile.activeSelf) profile.SetActive(false);
                else if (musicPanel.activeSelf) musicPanel.SetActive(false);
                else if (album.activeSelf) album.SetActive(false);
                else if (achievements.activeSelf) achievements.SetActive(false);
                else if (settings.activeSelf) CloseSettings();
                else if (shop.activeSelf && shopReturnsToPlay) CloseShop();
                else ShowHome();
            }
        }

        void ShowPage(GameObject page)
        {
            var wasDragging = tabsDragging;
            tabsDragging = false;
            if (play.activeSelf && page != play) { SaveRound(); CancelEndAnimation(); ClearFoundFeedback(); }
            if (page != shop) shopReturnsToPlay = false;
            modal.SetActive(false); settings.SetActive(false); profile.SetActive(false); album.SetActive(false); achievements.SetActive(false); musicPanel.SetActive(false);
            navigation.SetActive(page != play);
            RefreshWallet();
            if (page == currentPage) { if (wasDragging) StartTabTransition(); return; }
            var animate = !testing && currentPage && currentPage != play && page != play && isActiveAndEnabled;
            currentPage = page;
            selectedTab = page == shop ? 0 : page == ranking ? 2 : 1;
            play.SetActive(page == play);
            if (!animate) { FinishTabTransition(); return; }
            StartTabTransition();
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

        internal bool CanSwipeTabs => navigation.activeInHierarchy && !play.activeSelf && !tabsSliding &&
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
            if (!tabsDragging) return;
            if (!CanSwipeTabs) { CancelTabDrag(); return; }
            tabPosition = Mathf.Clamp(selectedTab - distance / stage.rect.width,
                Mathf.Max(0, selectedTab - 1), Mathf.Min(2, selectedTab + 1));
            highlightX = tabPosition * 206;
            MoveTabPages();
        }

        internal void EndTabDrag()
        {
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
            CancelRemoteLoad();
            CancelEndAnimation();
            SaveRound();
            Round = null; settled = false;
            var saved = SavedLevel();
            level = saved >= 0 ? saved : Mathf.Min(completed, LevelLimit - 1);
            ShowPage(home);
            startLabel.text = saved == level ? "继续 · 第" + (level + 1) + "关" : completed == LevelLimit ? "关卡相册" : "第" + (level + 1) + "关";
            homeAvatar.sprite = avatarSprites[avatar]; homeFrame.color = FrameColors[frame];
        }

        public void StartLevel()
        {
            if (completed == LevelLimit && home.activeSelf && SavedLevel() < 0) { ShowAlbum(); return; }
            BeginLevel(level, true);
        }

        public void SelectLevel(int index)
        {
            if (index < 0 || index >= LevelLimit || index > completed) return;
            BeginLevel(index, true);
        }

        void BeginLevel(int index, bool resume)
        {
            CancelRemoteLoad();
            if (OnlineLevels && (remoteLevel == null || loadedApiUrl != LevelUrl(index)))
            {
                SaveRound(); ClearFoundFeedback(); CancelEndAnimation(); Round = null;
                level = index; ShowPage(home);
                var request = new DifferenceRemoteLevel();
                loadingLevel = request;
                startButton.interactable = false;
                ShowDialog("正在加载关卡", "正在获取图片和差异位置…", "返回首页", ShowHome, terminal: true);
                StartCoroutine(LoadRemoteLevel(request, index, resume));
                return;
            }
            OpenLevel(index, resume);
        }

        void OpenLevel(int index, bool resume)
        {
            CancelEndAnimation(); level = index;
            ClearFoundFeedback();
            ConfigureLevel();
            roundContent = UsingRemoteLevel ? CurrentLevel.contentKey : "";
            var mask = 0; var lives = 3; roundMistakes = 0; pendingHint = -1;
            if (resume && SavedLevel() == level &&
                GameApp.Setting.GetString(Key + "RoundContent", "") == roundContent)
            { mask = GetInt("RoundMask", 0); lives = GetInt("RoundLives", 3); roundMistakes = Mathf.Max(3 - lives, GetInt("RoundMistakes", 0)); pendingHint = GetInt("RoundHint", -1); }
            Round = new DifferenceRound(spots, ImageAspect, mask, lives);
            if (pendingHint < 0 || pendingHint >= Round.Total || Round.IsFound(pendingHint) || Round.Finished) pendingHint = -1;
            settled = Round.Finished;
            ShowPage(play);
            upperImage.GetComponent<DifferenceBoard>().ResetView();
            crossTop.gameObject.SetActive(false); crossBottom.gameObject.SetActive(false);
            for (var i = 0; i < topRings.Length; i++)
            {
                var found = i < Round.Total && Round.IsFound(i);
                topRings[i].gameObject.SetActive(found); bottomRings[i].gameObject.SetActive(found);
                topRings[i].transform.localScale = bottomRings[i].transform.localScale = Vector3.one;
            }
            levelLabel.text = "第" + (level + 1) + "关";
            UpdateHUD(); SaveRound();
            if (Round.Finished) ShowRoundResult(false, 0);
            else Notice(Round.Count > 0 ? "已恢复本关进度" : "找出 " + Round.Total + " 处不同", 2.5f);
            RestoreHint();
        }

        void ConfigureLevel()
        {
            var data = CurrentLevel;
            EnsureSpotCapacity(data.regions.Length);
            upperImage.sprite = lowerImage.sprite = data.original;
            spots = new DifferenceSpot[data.regions.Length];
            for (var i = 0; i < differencePatches.Length; i++)
            {
                differencePatches[i].SetActive(i < data.regions.Length);
                if (i >= data.regions.Length) continue;
                var r = data.regions[i];
                var radius = Mathf.Max(23f / 600, new Vector2(r.z, r.w / ImageAspect).magnitude * .5f);
                spots[i] = data.hitSpots != null ? data.hitSpots[i] : new DifferenceSpot(r.x, r.y, radius);
                var left = (r.x - r.z / 2) * 600; var top = (r.y - r.w / 2) * 400;
                var patch = (RectTransform)differencePatches[i].transform;
                patch.SetParent(data.changedOnTop ? upperImage.transform : lowerImage.transform, false);
                patch.SetSiblingIndex(i);
                patch.anchoredPosition = new Vector2(left, -top); patch.sizeDelta = new Vector2(r.z * 600, r.w * 400);
                var cropped = data.croppedPatches != null;
                patchImages[i].rectTransform.anchoredPosition = cropped ? Vector2.zero : new Vector2(-left, top);
                patchImages[i].rectTransform.sizeDelta = cropped ? patch.sizeDelta : new Vector2(600, 400);
                patchImages[i].sprite = cropped ? data.croppedPatches[i] : data.changed;
                patchImages[i].color = Color.white;
                ConfigurePatchFlash(originalPatchImages[i], data.changedOnTop ? upperImage : lowerImage,
                    patch, data.original, new Vector2(-left, top), new Vector2(600, 400));
                ConfigurePatchFlash(changedFlashImages[i], data.changedOnTop ? lowerImage : upperImage,
                    patch, patchImages[i].sprite, patchImages[i].rectTransform.anchoredPosition, patchImages[i].rectTransform.sizeDelta);
                patchElapsed[i] = -1;
                var size = spots[i].radius * 1200;
                foreach (var marker in new[] { topRings[i], bottomRings[i] })
                {
                    marker.rectTransform.anchoredPosition = new Vector2(spots[i].x * 600, -spots[i].y * 400);
                    marker.rectTransform.sizeDelta = Vector2.one * size;
                }
            }
        }

        public void ClickImage(float x, float y, bool fromLower = false)
        {
            if (!play.activeSelf || modal.activeSelf || settings.activeSelf || Round == null || settled) return;
            hintIdle = 0; hintHand.Show(false);
            var source = fromLower ? lowerImage.rectTransform : upperImage.rectTransform;
            var point = new Vector3(source.rect.xMin + x * source.rect.width, source.rect.yMax - y * source.rect.height);
            var origin = source.TransformPoint(point);
            var hinted = HintActive;
            if (hinted)
            {
                if (!hintSpotlight.Ready || float.IsNaN(x) || float.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1) return;
                var spot = spots[hintSpotlight.TargetIndex];
                var distance = Vector2.Scale(new Vector2(x - spot.x, y - spot.y), source.rect.size) * source.localScale.x;
                if (distance.sqrMagnitude > DifferenceHintSpotlight.Radius * DifferenceHintSpotlight.Radius) return;
                x = spot.x; y = spot.y;
            }
            var result = Round.Click(x, y);
            if (result == DifferenceRound.Miss) ShowCross(x, y);
            if (hinted && result >= 0) { pendingHint = -1; hintSpotlight.Dismiss(); }
            ApplyResult(result, origin);
        }

        public void UseHint()
        {
            if (!play.activeSelf || modal.activeSelf || settings.activeSelf || Round == null || Round.Finished || settled || HintActive) return;
            if (pendingHint >= 0) { RestoreHint(); return; }
            if (hints == 0) { ShowShop(); return; }
            var result = Round.Hint();
            if (result < 0) return;
            pendingHint = result;
            GameApp.Setting.SetInt(Key + "RoundHint", pendingHint);
            hints--; Save();
            RestoreHint();
            UpdateHUD();
        }

        void RestoreHint()
        {
            if (pendingHint < 0 || !play.activeSelf || Round == null || Round.Finished || HintActive) return;
            hintIdle = 0; hintHand.Show(false);
            noticeLabel.transform.parent.gameObject.SetActive(false);
            var spot = spots[pendingHint];
            hintSpotlight.Show(upperImage.GetComponent<DifferenceBoard>(), upperImage.rectTransform, lowerImage.rectTransform,
                new Vector2(spot.x, spot.y), pendingHint);
        }

        void AdvanceHintGuide(float deltaTime)
        {
            var available = play.activeSelf && Round != null && !Round.Finished && !settled && hints > 0 &&
                !HintActive && !modal.activeSelf && !settings.activeSelf;
            if (!available) hintIdle = 0;
            else hintIdle += Mathf.Max(0, deltaTime);
            hintHand.Show(available && hintIdle >= 8);
        }

        void ApplyResult(int result, Vector3? worldOrigin = null)
        {
            if (result == DifferenceRound.Ignored) return;
            if (result >= 0)
            {
                patchElapsed[result] = 0;
                originalPatchImages[result].color = changedFlashImages[result].color = new Color(1, 1, 1, 0);
                originalPatchImages[result].transform.parent.gameObject.SetActive(true);
                changedFlashImages[result].transform.parent.gameObject.SetActive(true);
                totalFound++;
                topRings[result].gameObject.SetActive(true); bottomRings[result].gameObject.SetActive(true);
                StartCoroutine(Pulse(topRings[result].transform)); StartCoroutine(Pulse(bottomRings[result].transform));
                var slot = Round.Count - 1;
                var foundRound = Round;
                pendingProgress[slot] = true;
                DifferenceFoundFlight.Launch((RectTransform)play.transform, worldOrigin ?? topRings[result].transform.position,
                    progressDots[slot].rectTransform, () =>
                    {
                        if (Round != foundRound || !play.activeSelf) return;
                        pendingProgress[slot] = false;
                        UpdateHUD();
                    }, foundFlightMaterial);
                PlayTone(foundSound); noticeLabel.transform.parent.gameObject.SetActive(false);
            }
            else { roundMistakes++; PlayTone(missSound); StartCoroutine(Pulse(heartsLabel.transform)); }
            UpdateHUD(); SaveRound(); Save();
            if (!Round.Finished) return;
            settled = true;
            if (Round.Complete)
            {
                var reward = completed <= level ? 10 : 0;
                if (reward > 0) { completed = Mathf.Max(completed, level + 1); coins += reward; if (roundMistakes == 0) perfect++; }
                ClearRound(); Save(); PlayTone(winSound);
                endRoutine = StartCoroutine(EndRound(true, reward));
            }
            else endRoutine = StartCoroutine(EndRound(false, 0));
        }

        IEnumerator EndRound(bool won, int reward)
        {
            var finishedRound = Round;
            if (!testing) yield return new WaitForSecondsRealtime(won ? Mathf.Max(DifferenceFoundFlight.Duration + .06f, PatchFlashDuration + .05f) : .25f);
            if (!play.activeSelf || Round != finishedRound) { endRoutine = null; yield break; }
            ShowRoundResult(won, reward);
            endRoutine = null;
        }

        void ShowRoundResult(bool won, int reward)
        {
            if (won)
            {
                ShowDialog("太棒了！", "已找到全部 " + Round.Total + " 处不同\n" + (reward > 0 ? "+ " + reward + " 金币" : "本关已完成"),
                    level + 1 < LevelLimit ? "下一关" : "关卡相册", () => { if (level + 1 < LevelLimit) BeginLevel(level + 1, false); else { ShowHome(); ShowAlbum(); } },
                    "返回首页", ShowHome, null, null, true);
                StartCoroutine(Celebrate());
            }
            else
            {
                ShowDialog("机会用完了", "已找到 " + Round.Count + " / " + Round.Total + "\n补充爱心，保留已找到的位置", "30 金币继续", Revive,
                    "重新挑战", () => BeginLevel(level, false), "返回首页", ShowHome, true);
            }
        }

        public void Revive()
        {
            if (Round == null || Round.Complete || Round.Lives != 0) return;
            if (coins < 30)
            {
                ShowDialog("金币不足", "继续挑战需要 30 金币。\n也可以免费重新挑战。", "重新挑战", () => BeginLevel(level, false), "返回首页", ShowHome, null, null, true);
                return;
            }
            if (!Round.Revive()) return;
            coins -= 30; settled = false; modal.SetActive(false); SaveRound(); Save(); UpdateHUD();
        }

        void ClearFoundFeedback()
        {
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
            hintLabel.text = hints.ToString();
            var shownCount = 0;
            for (var i = 0; i < progressDots.Length; i++)
            {
                var shown = i < Round.Count && !pendingProgress[i];
                if (shown) shownCount++;
                progressDots[i].gameObject.SetActive(i < Round.Total);
                progressDots[i].sprite = shown ? progressCheck : progressQuestion;
                progressDots[i].GetComponentInChildren<UnityEngine.UI.Text>().text = shown ? "✓" : "?";
            }
            progressLabel.text = shownCount + " / " + Round.Total;
            RefreshWallet();
        }

        public void ShowSettings()
        {
            if (hintSpotlight) hintSpotlight.Cancel();
            hintIdle = 0; if (hintHand) hintHand.Show(false);
            settings.SetActive(true); RefreshSettings();
        }
        void CloseSettings() { settings.SetActive(false); musicPanel.SetActive(false); achievements.SetActive(false); album.SetActive(false); RestoreHint(); }
        void RefreshSettings()
        {
            ToggleLook("Settings/Scroll/Viewport/Content/Sound", sound);
            ToggleLook("Settings/Scroll/Viewport/Content/Music", musicEnabled);
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
            if (!musicEnabled) { musicSource.Stop(); return; }
            if (musicSource.clip != musicTracks[music]) { musicSource.Stop(); musicSource.clip = musicTracks[music]; }
            if (!musicSource.isPlaying) musicSource.Play();
        }

        public void ShowShop()
        {
            var fromPlay = play.activeSelf;
            shopReturnsToPlay = fromPlay || (shop.activeSelf && shopReturnsToPlay);
            ShowPage(shop);
            stage.Find("Shop/Close").gameObject.SetActive(shopReturnsToPlay);
            RefreshShop();
        }
        void CloseShop()
        {
            if (!shopReturnsToPlay || Round == null) { ShowHome(); return; }
            ShowPage(play); UpdateHUD();
            if (Round.Finished) ShowRoundResult(Round.Complete, 0);
            else RestoreHint();
        }
        void RefreshShop()
        {
            var claimed = GiftClaimed(DateTime.UtcNow.ToString("yyyy-MM-dd"));
            var button = At<UnityEngine.UI.Button>("Shop/Scroll/Viewport/Content/Free/Buy");
            button.interactable = !claimed; button.GetComponentInChildren<UnityEngine.UI.Text>().text = claimed ? "已领取" : "每日免费";
            At<UnityEngine.UI.Button>("Shop/Scroll/Viewport/Content/HintOffer/Buy").interactable = coins >= 900;
            SetText("Shop/HintCount", "我的提示  ×" + hints);
        }
        public void ClaimDailyHint()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (GiftClaimed(today)) { RefreshShop(); return; }
            giftDate = today; hints++; Save(); RefreshShop();
            ShowDialog("领取成功", "获得 1 次提示", "收下", null);
        }
        public void BuyHint()
        {
            if (coins < 900) return;
            coins -= 900; hints++; Save(); RefreshWallet(); RefreshShop();
            ShowDialog("兑换成功", "获得 1 次提示", "继续", null);
        }
        void ShowNoAds() { ShowDialog("无广告", "当前版本没有插屏与横幅广告。\n可以安心找不同。", "开始游戏", () => { settings.SetActive(false); if (!play.activeSelf) StartLevel(); }); }

        public void ShowProfile()
        {
            draftAvatar = avatar; draftFrame = frame; frameTab = false;
            nicknameInput.text = nickname; profile.SetActive(true); RefreshProfile();
        }
        void RefreshProfile()
        {
            profileAvatar.sprite = avatarSprites[draftAvatar]; profileFrame.color = FrameColors[draftFrame];
            SetText("Profile/Card/AvatarTab/Label", "头像"); SetText("Profile/Card/FrameTab/Label", "头像框");
            for (var i = 0; i < 8; i++)
            {
                var choice = stage.Find("Profile/Card/Choices/Choice" + i);
                var valid = i < (frameTab ? FrameColors.Length : avatarSprites.Length);
                choice.gameObject.SetActive(valid);
                if (!valid) continue;
                choice.GetComponent<UnityEngine.UI.Image>().color = FrameColors[frameTab ? i : draftFrame];
                choice.Find("Avatar").GetComponent<UnityEngine.UI.Image>().sprite = avatarSprites[frameTab ? draftAvatar : i];
                var owned = ((frameTab ? ownedFrames : ownedAvatars) & (1 << i)) != 0;
                SetText("Profile/Card/Choices/Choice" + i + "/Price", owned ? "" : frameTab ? "5000" : "3000");
                choice.Find("Selected").gameObject.SetActive(i == (frameTab ? draftFrame : draftAvatar));
            }
            var cost = ProfileCost();
            SetText("Profile/Card/Save/Label", cost > 0 ? "解锁并保存  " + cost : "保存");
            At<UnityEngine.UI.Button>("Profile/Card/Save").interactable = coins >= cost;
        }
        int ProfileCost() { return ((ownedAvatars & (1 << draftAvatar)) == 0 ? 3000 : 0) + ((ownedFrames & (1 << draftFrame)) == 0 ? 5000 : 0); }
        public void SaveProfile()
        {
            var value = nicknameInput.text.Trim();
            if (value.Length == 0) { ShowDialog("请输入昵称", "昵称不能为空。", "继续编辑", null); return; }
            if (draftAvatar < 0 || draftAvatar >= avatarSprites.Length || draftFrame < 0 || draftFrame >= FrameColors.Length) return;
            var cost = ProfileCost();
            if (coins < cost) { ShowDialog("金币不足", "解锁所选头像与头像框需要 " + cost + " 金币。", "继续选择", null); return; }
            coins -= cost; avatar = draftAvatar; frame = draftFrame;
            ownedAvatars |= 1 << avatar; ownedFrames |= 1 << frame;
            var length = Mathf.Min(value.Length, 16);
            if (length < value.Length && char.IsHighSurrogate(value[length - 1])) length--;
            nickname = value.Substring(0, length); Save();
            homeAvatar.sprite = avatarSprites[avatar]; homeFrame.color = FrameColors[frame];
            profile.SetActive(false); RefreshWallet();
        }

        void ShowAchievements()
        {
            achievements.SetActive(true);
            var values = new[] { completed, totalFound, perfect, completed };
            var goals = new[] { 1, 10, 1, levels.Length };
            SetText("Achievements/Card/Row3/Detail", OnlineLevels ? "完成 3 个关卡" : "完成全部已开放场景");
            for (var i = 0; i < values.Length; i++)
            {
                SetText("Achievements/Card/Row" + i + "/Progress", Mathf.Min(values[i], goals[i]) + " / " + goals[i]);
                SetText("Achievements/Card/Row" + i + "/State", values[i] >= goals[i] ? "已达成" : "进行中");
            }
        }
        public void ShowAlbum()
        {
            album.SetActive(true);
            albumPage = OnlineLevels ? Mathf.Min(level, completed) / levels.Length : 0;
            RefreshAlbum();
        }

        void RefreshAlbum()
        {
            albumPrevious.gameObject.SetActive(OnlineLevels); albumNext.gameObject.SetActive(OnlineLevels);
            albumPrevious.interactable = albumPage > 0;
            albumNext.interactable = albumPage < completed / levels.Length;
            var more = At<UnityEngine.UI.Text>("Album/Card/More");
            more.text = OnlineLevels ? (albumPage + 1) + " / " + (completed / levels.Length + 1) : "更多场景，持续更新";
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
                row.Find("Title").GetComponent<UnityEngine.UI.Text>().text = ((long)index + 1) + "  " + (OnlineLevels ? "在线关卡" : data.title);
                row.Find("Count").GetComponent<UnityEngine.UI.Text>().text = data == null ? "点击加载关卡" : data.regions.Length + " 处不同";
                row.Find("Preview").GetComponent<UnityEngine.UI.Image>().sprite = data?.original;
                row.GetComponent<UnityEngine.UI.Button>().interactable = index <= completed && index < LevelLimit;
                row.Find("Status").GetComponent<UnityEngine.UI.Text>().text = index < completed ? "已完成 · 重玩" : index == completed ? "开始寻找" : "尚未解锁";
            }
        }

        public void ShowDialog(string title, string body, string actionLabel, Action action, string second = null, Action secondAction = null, string third = null, Action thirdAction = null, bool terminal = false)
        {
            modalTitle.text = title; modalBody.text = body; modalActionLabel.text = actionLabel;
            dialogAction = action; secondaryAction = secondAction; tertiaryAction = thirdAction;
            modalSecondary.gameObject.SetActive(second != null); modalTertiary.gameObject.SetActive(third != null);
            modalSecondary.GetComponentInChildren<UnityEngine.UI.Text>().text = second;
            modalTertiary.GetComponentInChildren<UnityEngine.UI.Text>().text = third;
            modalClose.gameObject.SetActive(!terminal); resultIcon.gameObject.SetActive(terminal && Round != null && Round.Complete);
            modal.SetActive(true); modal.transform.SetAsLastSibling();
        }

        void RefreshWallet()
        {
            coinsLabel.text = coins.ToString(); SetText("Shop/Coins", coins.ToString());
            SetText("Ranking/PlayerName", nickname ?? "Player_1001");
        }
        int GetInt(string name, int value) { return GameApp.Setting.GetInt(Key + name, value); }
        int SavedLevel()
        {
            var saved = GetInt("RoundLevel", -1);
            if (saved < 0 || saved >= LevelLimit || saved > completed) return -1;
            var count = OnlineLevels
                ? remoteLevel != null && loadedApiUrl == LevelUrl(saved) ? remoteLevel.Level.regions.Length : DifferenceRound.MaxSpots
                : levels[saved].regions.Length;
            var mask = GetInt("RoundMask", 0); var lives = GetInt("RoundLives", 3);
            return count > 0 && count <= DifferenceRound.MaxSpots && mask >= 0 && mask < (1L << count) - 1 && lives >= 0 && lives <= 3 ? saved : -1;
        }
        void SaveRound()
        {
            if (Round == null || Round.Complete) return;
            GameApp.Setting.SetInt(Key + "RoundLevel", level); GameApp.Setting.SetInt(Key + "RoundMask", Round.FoundMask); GameApp.Setting.SetInt(Key + "RoundLives", Round.Lives);
            GameApp.Setting.SetInt(Key + "RoundMistakes", roundMistakes);
            GameApp.Setting.SetInt(Key + "RoundHint", pendingHint);
            GameApp.Setting.SetString(Key + "RoundContent", roundContent);
            GameApp.Setting.Save();
        }
        void ClearRound() { GameApp.Setting.SetInt(Key + "RoundLevel", -1); GameApp.Setting.Save(); }
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
            GameApp.Setting.SetBool(Key + "Sound", sound); GameApp.Setting.SetBool(Key + "Music", musicEnabled);
            GiftClaimed(DateTime.UtcNow.ToString("yyyy-MM-dd"));
            GameApp.Setting.SetString(Key + "Nickname", nickname); GameApp.Setting.SetString(Key + "GiftDate", giftDate);
            GameApp.Setting.Save();
        }
        void OnApplicationPause(bool paused) { if (paused && Round != null) SaveRound(); }
        public override void OnClose(bool isShutdown, object userData)
        { SaveRound(); CancelRemoteLoad(); CancelEndAnimation(); ClearFoundFeedback(); ReleaseRemoteLevel(); FinishTabTransition(); base.OnClose(isShutdown, userData); }
        void CancelEndAnimation() { if (endRoutine != null) { StopCoroutine(endRoutine); endRoutine = null; } }
        public void SetTesting(bool value) { testing = value; if (value) FinishTabTransition(); }
        void PlayTone(AudioClip clip) { if (sound && clip) audioSource.PlayOneShot(clip, .4f); }
        void ShowCross(float x, float y)
        {
            foreach (var label in new[] { crossTop, crossBottom })
            {
                label.rectTransform.anchoredPosition = new Vector2(x * 600, -y * 400);
                label.gameObject.SetActive(true); StartCoroutine(HideCross(label));
            }
        }
        IEnumerator HideCross(UnityEngine.UI.Text label) { yield return new WaitForSecondsRealtime(.6f); label.gameObject.SetActive(false); }
        IEnumerator Pulse(Transform target, float amount = .22f)
        {
            for (var elapsed = 0f; elapsed < .32f; elapsed += Time.unscaledDeltaTime)
            { target.localScale = Vector3.one * (1 + amount * Mathf.Sin(elapsed / .32f * Mathf.PI)); yield return null; }
            target.localScale = Vector3.one;
        }
        IEnumerator Celebrate()
        {
            for (var i = 0; i < confetti.Length; i++) confetti[i].gameObject.SetActive(true);
            for (var elapsed = 0f; elapsed < 2f && modal.activeSelf; elapsed += Time.unscaledDeltaTime)
            {
                for (var i = 0; i < confetti.Length; i++)
                {
                    confetti[i].anchoredPosition = new Vector2(50 + i * 619f / confetti.Length + Mathf.Sin(elapsed * 4 + i) * 50, -(180 + elapsed * (300 + i % 5 * 40)));
                    confetti[i].localEulerAngles = new Vector3(0, 0, elapsed * 180 + i * 37);
                }
                yield return null;
            }
            foreach (var piece in confetti) piece.gameObject.SetActive(false);
        }
        void Notice(string message, float seconds)
        {
            if (noticeRoutine != null) StopCoroutine(noticeRoutine);
            ((RectTransform)noticeLabel.transform.parent).anchoredPosition = new Vector2(120, play.activeSelf && Round != null && Round.Total > 15 ? -1210 : -144);
            noticeLabel.text = message; noticeLabel.transform.parent.gameObject.SetActive(true); noticeLabel.transform.parent.SetAsLastSibling();
            noticeRoutine = StartCoroutine(ClearNotice(seconds));
        }
        IEnumerator ClearNotice(float seconds) { yield return new WaitForSecondsRealtime(seconds); noticeLabel.transform.parent.gameObject.SetActive(false); noticeRoutine = null; }
        T At<T>(string path) where T : Component
        {
            var found = stage.Find(path); if (!found) throw new InvalidOperationException("缺少 UI 节点：" + path);
            return found.GetComponent<T>();
        }
        void Bind(string path, Action action) { At<UnityEngine.UI.Button>(path).onClick.AddListener(() => { PlayTone(foundSound); action(); }); }
        void SetText(string path, string value) { At<UnityEngine.UI.Text>(path).text = value; }
    }
}
#endif
