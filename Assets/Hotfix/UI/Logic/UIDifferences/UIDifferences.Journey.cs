#if ENABLE_UI_UGUI
using System;
using Hotfix.Manager;
using UnityEngine;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        public const int JourneyUnlockLevel = 20;
        // shortcut: 200 levels until the service supplies the journey catalogue size.
        public const int JourneyLevelCount = 200;
        const string JourneyLevelApi = "https://diffapi.joystar.pro/api/christmas/levels/";
        [SerializeField] GameObject journeyDesign, journeyLockOverlay;
        [SerializeField] UnityEngine.UI.Button journeyEntryButton, journeyPlayButton, journeyCloseButton;
        [SerializeField] UnityEngine.UI.Text journeyUnlockProgress, journeyPlayLabel;
        [SerializeField] UnityEngine.UI.Text journeyUnlockedLabel;
        [SerializeField] GameObject[] journeyLockedObjects = Array.Empty<GameObject>();
        [SerializeField] UnityEngine.UI.ScrollRect journeyScroll;
        [SerializeField] UnityEngine.UI.Scrollbar journeyUnlockBar;
        [SerializeField] UnityEngine.UI.Button[] journeyLevelButtons = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] Sprite journeyCurrentNodeSprite, journeyDefaultNodeSprite;
        UnityEngine.UI.Text[] journeyLevelLabels;
        int journeyCompleted, journeySelected;
        bool journeyPlaying, journeyVisible;
        public bool JourneyPlaying => journeyPlaying;
        public bool JourneyVisible => journeyVisible;
        public int JourneyCompleted => journeyCompleted;
        public bool JourneyUnlocked => completed >= JourneyUnlockLevel;
        int ActiveCompleted => journeyPlaying ? journeyCompleted : completed;

        string RoundSettingKey(string name) => Key + (journeyPlaying ? "Journey." : "") + name;

        void BindJourney()
        {
            if (!journeyDesign) return;
            journeyEntryButton.onClick.AddListener(ShowJourney);
            journeyPlayButton.onClick.AddListener(PlayJourneyLevel);
            journeyCloseButton.onClick.AddListener(ShowHome);
            journeyLevelLabels = new UnityEngine.UI.Text[journeyLevelButtons.Length];
            for (var i = 0; i < journeyLevelButtons.Length; i++)
            {
                var index = i;
                journeyLevelLabels[i] = journeyLevelButtons[i].GetComponentInChildren<UnityEngine.UI.Text>(true);
                journeyLevelButtons[i].onClick.AddListener(() => SelectJourneyLevel(index));
            }
            journeyDesign.SetActive(false);
        }

        void InitializeJourney()
        {
            journeyPlaying = journeyVisible = false;
            journeyCompleted = Mathf.Clamp(GetInt("JourneyCompleted", 0), 0, JourneyLevelCount);
            journeySelected = Mathf.Min(journeyCompleted, JourneyLevelCount - 1);
            if (journeyDesign) journeyDesign.SetActive(false);
        }

        void RefreshJourneyEntry()
        {
            if (journeyLockOverlay) journeyLockOverlay.SetActive(!JourneyUnlocked);
            foreach (var item in journeyLockedObjects) if (item) item.SetActive(!JourneyUnlocked);
            if (journeyUnlockedLabel) journeyUnlockedLabel.gameObject.SetActive(JourneyUnlocked);
            if (journeyUnlockProgress) journeyUnlockProgress.text = Mathf.Clamp(completed, 0, JourneyUnlockLevel) + "/" + JourneyUnlockLevel;
            if (journeyUnlockBar)
            {
                journeyUnlockBar.size = Mathf.Clamp01((float)completed / JourneyUnlockLevel);
                journeyUnlockBar.SetValueWithoutNotify(0);
            }
            if (journeyEntryButton) journeyEntryButton.interactable = JourneyUnlocked && !IsLoadingLevel;
        }

        void UpdateJourneyDesign()
        {
            if (!journeyDesign) return;
            var visible = journeyVisible && !IsLoadingLevel && !figmaResultActive;
            journeyDesign.SetActive(visible);
            if (visible)
            {
                if (homeDesign) homeDesign.SetActive(false);
                navigation.SetActive(false);
                journeyDesign.transform.SetAsLastSibling();
            }
            RefreshJourneyEntry();
        }

        public void ShowJourney()
        {
            if (AdInputBlocked || IsLoadingLevel || !JourneyUnlocked || !journeyDesign) return;
            ShowHomeInternal();
            journeyVisible = true;
            PlayMusic();
            journeySelected = Mathf.Min(journeyCompleted, JourneyLevelCount - 1);
            RefreshJourneyMap();
            UpdateFigmaDesign(currentPage);
            CenterJourneySelection();
        }

        void SelectJourneyLevel(int index)
        {
            if (AdInputBlocked || !journeyVisible || index < 0 || index >= JourneyLevelCount || index > journeyCompleted) return;
            journeySelected = index;
            RefreshJourneyMap();
        }

        void RefreshJourneyMap()
        {
            for (var i = 0; i < journeyLevelButtons.Length; i++)
            {
                var button = journeyLevelButtons[i];
                button.gameObject.SetActive(i < JourneyLevelCount);
                if (i >= JourneyLevelCount) continue;
                button.interactable = i <= journeyCompleted;
                if (journeyLevelLabels[i]) journeyLevelLabels[i].text = (i + 1).ToString();
                var sprite = i == journeySelected ? journeyCurrentNodeSprite : journeyDefaultNodeSprite;
                if (sprite) button.image.sprite = sprite;
            }
            if (journeyPlayLabel) journeyPlayLabel.text = "Play Level " + (journeySelected + 1);
            journeyPlayButton.interactable = journeySelected <= journeyCompleted;
        }

        void CenterJourneySelection()
        {
            if (!journeyScroll || journeySelected >= journeyLevelButtons.Length) return;
            Canvas.ForceUpdateCanvases();
            journeyScroll.StopMovement();
            var content = journeyScroll.content;
            var viewport = journeyScroll.viewport;
            var range = content.rect.height - viewport.rect.height;
            if (range <= 0) return;
            var button = (RectTransform)journeyLevelButtons[journeySelected].transform;
            var y = content.InverseTransformPoint(button.TransformPoint(button.rect.center)).y;
            journeyScroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((content.rect.yMax - y - viewport.rect.height * .5f) / range);
        }

        void PlayJourneyLevel()
        {
            if (AdInputBlocked || !journeyVisible || !JourneyUnlocked || journeySelected < 0 ||
                journeySelected >= JourneyLevelCount || journeySelected > journeyCompleted || IsLoadingLevel) return;
            SaveRound();
            Round = null;
            journeyVisible = false;
            journeyPlaying = true;
            BeginLevel(journeySelected, true);
        }

        void ExitJourneyMode()
        {
            journeyVisible = false;
            if (journeyDesign) journeyDesign.SetActive(false);
            if (!journeyPlaying) return;
            SaveRound();
            Round = null;
            settled = false;
            journeyPlaying = false;
        }

        void RecordLevelCompletion()
        {
            if (journeyPlaying) journeyCompleted = Mathf.Min(JourneyLevelCount, Mathf.Max(journeyCompleted, level + 1));
            else completed = Mathf.Max(completed, level + 1);
        }

        public void JourneySelfCheck()
        {
            var previousMode = journeyPlaying;
            var previousCompleted = completed;
            var previousJourneyCompleted = journeyCompleted;
            var previousLevel = level;
            try
            {
                journeyPlaying = false; completed = 19; journeyCompleted = 0;
                if (JourneyUnlocked) throw new InvalidOperationException("Journey must remain locked before 20 completed main levels.");
                completed = 20;
                if (!JourneyUnlocked || ActiveCompleted != 20 || RoundSettingKey("RoundLevel") != Key + "RoundLevel")
                    throw new InvalidOperationException("Main progression and existing save keys must remain unchanged.");
                journeyPlaying = true;
                if (ActiveCompleted != 0 || LevelLimit != JourneyLevelCount ||
                    LevelUrl(0) != JourneyLevelApi + "1" || LevelUrl(199) != JourneyLevelApi + "200" ||
                    RoundSettingKey("RoundLevel") == Key + "RoundLevel")
                    throw new InvalidOperationException("Journey must start at 1 and isolate its endpoint, limit and saves.");
                level = 0; RecordLevelCompletion();
                if (UnlimitedHints) throw new InvalidOperationException("Journey must not inherit the main tutorial's unlimited hints.");
                if (completed != 20 || journeyCompleted != 1 || ActiveCompleted <= level)
                    throw new InvalidOperationException("Journey completion must advance only journey progress and prevent repeat rewards.");
                level = 199; RecordLevelCompletion();
                if (journeyCompleted != JourneyLevelCount || completed != 20)
                    throw new InvalidOperationException("Journey must end at 200 without changing main progress.");
            }
            finally
            {
                journeyPlaying = previousMode; completed = previousCompleted;
                journeyCompleted = previousJourneyCompleted; level = previousLevel;
            }
        }
    }
}
#endif
