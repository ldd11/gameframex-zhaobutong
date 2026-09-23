#if ENABLE_UI_UGUI
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        // Loaded and retained with the UI prefab by GameFrameX/YooAsset.
        public Sprite[] designSprites;
        GameObject homeDesign, settingsDesign, loadingDesign, loadingMagnifier, victoryDesign, failDesign, hintDesign;
        Image loadingFill, victoryPreview, musicToggle, soundToggle, vibrationToggle;
        Text designCoins, failProgress;
        Sprite designRound, toggleOn, toggleOff;
        bool figmaResultActive, figmaResultWon, localLoading;
        Coroutine localLoadRoutine;
        float loadingStarted;
        Vector2 loadingMagnifierCenter;
        static readonly Color DesignInk = new Color(8 / 255f, 31 / 255f, 69 / 255f);
        static readonly Color DesignCream = new Color(1, 250 / 255f, 235 / 255f);

        // Figma UI定稿 (125:220 etc.): use its 1440 x 3200 coordinates at half scale.
        void InstallFigmaDesign()
        {
            designRound = DesignSprite("RoundedPanel");
            toggleOn = DesignSprite("ToggleOn"); toggleOff = DesignSprite("ToggleOff");
            navigation.SetActive(false);
            shopButton.gameObject.SetActive(false); trophyButton.gameObject.SetActive(false);
            foreach (Transform child in home.transform) child.gameObject.SetActive(false);
            var oldSettings = settings.AddComponent<CanvasGroup>();
            oldSettings.alpha = 0; oldSettings.blocksRaycasts = oldSettings.interactable = false;

            homeDesign = DesignLayer("FigmaHome", Color.clear);
            DesignImage(homeDesign.transform, "Artwork", "HomeArtwork", 0, 0, 1440, 3200);
            var logo = DesignRect(homeDesign.transform, "Logo", 165, 467, 1186, 471);
            logo.gameObject.AddComponent<RectMask2D>();
            DesignImage(logo, "Image", "HomeLogo", -212, -41, 1573, 524);
            DesignImage(homeDesign.transform, "Wallet", "Wallet", 68, 147, 346, 180);
            DesignImage(homeDesign.transform, "Coin", "Coin", 113, 182, 85, 88);
            designCoins = DesignText(homeDesign.transform, "Coins", "", 205, 166, 180, 115, 80, new Color(.08f, .29f, .55f));
            DesignButton(homeDesign.transform, "Settings", "SettingsIcon", 1130, 114, 224, 224, "", ShowSettings);
            DesignButton(homeDesign.transform, "Start", "GreenButton", 279, 2493, 906, 236, "Start", StartLevel, 150);

            loadingDesign = DesignLayer("FigmaLoading", Color.white);
            DesignImage(loadingDesign.transform, "Background", "LoadingBackground", 0, 0, 1440, 3200);
            var magnifier = DesignImage(loadingDesign.transform, "Magnifier", "LoadingMagnifier", 286, 1105, 868, 868);
            loadingMagnifier = magnifier.gameObject; loadingMagnifier.name = "FigmaLoadingMagnifier";
            magnifier.rectTransform.pivot = new Vector2(.5f, .5f);
            magnifier.rectTransform.anchoredPosition += new Vector2(217, -217);
            loadingMagnifierCenter = magnifier.rectTransform.anchoredPosition;
            DesignImage(loadingDesign.transform, "Track", "LoadingTrack", 197, 2497, 1046, 142);
            loadingFill = DesignImage(loadingDesign.transform, "Progress", "LoadingFill", 213, 2513, 1014, 110);
            DesignText(loadingDesign.transform, "Caption", "Loading...", 420, 2690, 600, 120, 82, DesignInk);

            settingsDesign = DesignPopup("FigmaSettings", 1495, CloseSettings);
            var card = settingsDesign.transform.Find("Card");
            DesignText(card, "Title", "Settings", 90, 112, 1000, 140, 120, DesignInk);
            musicToggle = DesignSetting(card, "Music", 352, () => { musicEnabled = !musicEnabled; Save(); PlayMusic(); RefreshSettings(); });
            soundToggle = DesignSetting(card, "Sound", 567, () => { sound = !sound; Save(); RefreshSettings(); });
            vibrationToggle = DesignSetting(card, "Vibration", 782, ToggleVibration);
            var contact = DesignButton(card, "Contact", null, 117, 1032, 930, 190, "", null);
            StyleSettingRow(contact, 190, DesignCream);
            DesignImage(contact.transform, "Icon", "ContactIcon", 176, 32, 189, 126);
            DesignText(contact.transform, "Label", "Contact Us", 365, 0, 400, 190, 70, DesignInk);
            contact.interactable = false;
            DesignText(card, "Policies", "Terms of Service     |     Privacy Policy", 115, 1282, 950, 70, 45, DesignInk);
            settingsDesign.AddComponent<DifferencePopupMotion>();

            failDesign = DesignPopup("FigmaFail", 1740, ShowHome);
            card = failDesign.transform.Find("Card");
            DesignText(card, "Title", "Level Failed", 90, 112, 1000, 140, 120, DesignInk);
            DesignImage(card, "Heart", "BrokenHeart", 352, 373, 459, 459);
            failProgress = DesignText(card, "Progress", "", 132, 832, 900, 100, 70, new Color(.32f, .37f, .45f));
            // Preserve the existing coin revival rule; this project has no rewarded-ad provider.
            DesignButton(card, "Continue", "GreenButton", 125, 1025, 911, 243, "Continue  30", OnFigmaResultPrimary, 96);
            DesignButton(card, "Retry", "RetryButton", 122, 1352, 920, 231, "Retry", OnFigmaResultSecondary, 100, DesignInk);
            failDesign.AddComponent<DifferencePopupMotion>();

            hintDesign = DesignPopup("FigmaHint", 1740, CloseDesignHint);
            card = hintDesign.transform.Find("Card");
            DesignText(card, "Title", "Need a hint?", 90, 112, 1000, 140, 120, DesignInk);
            DesignImage(card, "Magnifier", "LoadingMagnifier", 339, 294, 486, 486);
            DesignText(card, "Description", "Use hints to find\ndifferences.", 132, 822, 900, 170, 70, new Color(.32f, .37f, .45f));
            DesignButton(card, "Free", "GreenButton", 137, 1026, 911, 243, "Free", () => AcquireDesignHint(true), 100);
            DesignButton(card, "Buy", "RetryButton", 139, 1349, 920, 231, "100", () => AcquireDesignHint(false), 100, DesignInk);
            DesignImage(card, "Coin", "Coin", 390, 1401, 105, 109);
            hintDesign.AddComponent<DifferencePopupMotion>();

            victoryDesign = DesignLayer("FigmaVictory", new Color(2 / 255f, 20 / 255f, 51 / 255f));
            DesignImage(victoryDesign.transform, "Glow", "VictoryGlow", 35, 288, 1349, 978);
            DesignImage(victoryDesign.transform, "Title", "VictoryTitle", 74, 480, 1271, 446);
            DesignText(victoryDesign.transform, "Caption", "Level Complete!", 210, 1003, 1000, 120, 90, new Color(.42f, .50f, .63f));
            var rim = DesignImage(victoryDesign.transform, "PreviewFrame", null, 82, 1200, 1275, 865, Color.white);
            var clip = DesignImage(rim.transform, "Clip", null, 12, 12, 1251, 841, Color.white);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            victoryPreview = DesignImage(clip.transform, "Preview", null, 0, 0, 1251, 841, Color.white);
            victoryPreview.type = Image.Type.Simple;
            DesignButton(victoryDesign.transform, "Next", "GreenButton", 267, 2316, 906, 236, "Next Level", OnFigmaResultPrimary, 120);
            foreach (var piece in confetti) { piece.SetParent(victoryDesign.transform, false); piece.GetComponent<Image>().raycastTarget = false; }

            UpdateFigmaDesign(currentPage);
        }

        Sprite DesignSprite(string name)
        {
            var sprite = Array.Find(designSprites, item => item && item.name == name);
            if (!sprite) throw new InvalidOperationException("Missing Figma UI sprite: " + name);
            return sprite;
        }

        RectTransform DesignRect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = stage.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y) * .5f; rect.sizeDelta = new Vector2(width, height) * .5f;
            return rect;
        }

        Image DesignImage(Transform parent, string name, string asset, float x, float y, float width, float height, Color? tint = null)
        {
            var image = DesignRect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = asset == null ? designRound : DesignSprite(asset);
            image.type = asset == null ? Image.Type.Sliced : Image.Type.Simple;
            if (asset == null) image.pixelsPerUnitMultiplier = 2;
            image.color = tint ?? Color.white; image.raycastTarget = false;
            return image;
        }

        Text DesignText(Transform parent, string name, string text, float x, float y, float width, float height, int size, Color color)
        {
            var label = DesignRect(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = startLabel.font; label.text = text; label.fontSize = size / 2; label.fontStyle = FontStyle.Bold;
            label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = size / 3; label.resizeTextMaxSize = size / 2;
            return label;
        }

        Button DesignButton(Transform parent, string name, string asset, float x, float y, float width, float height,
            string caption, Action action, int fontSize = 100, Color? textColor = null)
        {
            var image = DesignImage(parent, name, asset, x, y, width, height); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());
            if (!string.IsNullOrEmpty(caption)) DesignText(button.transform, "Label", caption, 25, 0, width - 50, height, fontSize, textColor ?? Color.white);
            return button;
        }

        GameObject DesignLayer(string name, Color color)
        {
            var rect = DesignRect(stage, name, 0, 0, 1440, 3200);
            var scale = Mathf.Min(stage.rect.width / 720, stage.rect.height / 1600);
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = new Vector2((stage.rect.width - 720 * scale) / 2,
                -(stage.rect.height - 1600 * scale) / 2);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = true;
            rect.gameObject.SetActive(false); return rect.gameObject;
        }

        GameObject DesignPopup(string name, float height, Action close)
        {
            var layer = DesignLayer(name, Color.clear);
            var scrim = DesignImage(layer.transform, "Scrim", null, 0, 0, 1440, 3200, new Color(.02f, .09f, .17f, .68f));
            scrim.sprite = null; scrim.raycastTarget = true;
            var card = DesignImage(layer.transform, "Card", null, 130, 720, 1180, height, Color.white);
            card.raycastTarget = true;
            var clip = DesignImage(card.transform, "Surface", null, 8, 8, 1164, height - 16, Color.white);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            DesignImage(clip.transform, "Fill", "ModalBackground", 0, 0, 1164, height - 16);
            DesignButton(layer.transform, "Close", "Close", 1159, 643, 227, 227, "", close);
            return layer;
        }

        Image DesignSetting(Transform card, string name, float y, Action toggle)
        {
            var row = DesignButton(card, name, null, 117, y, 930, 180, "", toggle);
            StyleSettingRow(row, 180, new Color(1, 254 / 255f, 247 / 255f));
            DesignImage(row.transform, "Icon", name, 36, 40, 110, 100);
            var label = DesignText(row.transform, "Label", name, 160, 10, 480, 160, 70, DesignInk); label.alignment = TextAnchor.MiddleLeft;
            return DesignImage(row.transform, "Switch", "ToggleOn", 680, 35, 220, 110);
        }

        void StyleSettingRow(Button row, float height, Color fill)
        {
            var border = row.GetComponent<Image>();
            border.color = new Color(199 / 255f, 181 / 255f, 158 / 255f);
            border.pixelsPerUnitMultiplier = 180f / 55;
            var surface = DesignImage(row.transform, "Surface", null, 4, 4, 922, height - 8, fill);
            surface.pixelsPerUnitMultiplier = 180f / 51;
        }

        void UpdateFigmaDesign(GameObject page)
        {
            if (!homeDesign) return;
            var loading = IsLoadingLevel;
            homeDesign.SetActive(page == home && !loading && !figmaResultActive);
            loadingDesign.SetActive(loading);
            victoryDesign.SetActive(figmaResultActive && figmaResultWon && !loading);
            failDesign.SetActive(figmaResultActive && !figmaResultWon && !loading);
            settingsDesign.SetActive(settings.activeSelf && !loading && !figmaResultActive);
            if (designCoins) designCoins.text = coins.ToString();
            foreach (var layer in new[] { homeDesign, loadingDesign, victoryDesign, failDesign, hintDesign, settingsDesign })
                if (layer.activeSelf) layer.transform.SetAsLastSibling();
            if (modal.activeSelf) modal.transform.SetAsLastSibling();
        }

        void AdvanceDesignLoading()
        {
            if (!loadingDesign || !loadingDesign.activeSelf) return;
            var elapsed = Time.unscaledTime - loadingStarted;
            var angle = elapsed * Mathf.PI;
            var magnifier = (RectTransform)loadingMagnifier.transform;
            magnifier.anchoredPosition = loadingMagnifierCenter + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 24;
            magnifier.localRotation = Quaternion.identity;
            loadingFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                507 * (.18f + .67f * (1 - Mathf.Exp(-elapsed))));
        }

        IEnumerator LoadLocalLevel(int index, bool resume)
        {
            yield return new WaitForSecondsRealtime(.55f);
            localLoading = false; localLoadRoutine = null;
            OpenLevel(index, resume);
        }

        void ShowFigmaResult(bool won)
        {
            figmaResultWon = won; figmaResultActive = true; modal.SetActive(false); CloseDesignHint();
            victoryPreview.sprite = upperImage.sprite;
            failProgress.text = "You completed " + Mathf.RoundToInt(100f * Round.Count / Mathf.Max(1, Round.Total)) + "%.";
            UpdateFigmaDesign(currentPage);
        }

        void CloseFigmaResult() { figmaResultActive = false; UpdateFigmaDesign(currentPage); }
        void OnFigmaResultPrimary()
        {
            if (figmaResultWon) { CloseFigmaResult(); if (level + 1 < LevelLimit) BeginLevel(level + 1, false); else ShowHome(); }
            else Revive();
        }
        void OnFigmaResultSecondary() { CloseFigmaResult(); BeginLevel(level, false); }

        void ToggleVibration()
        {
            vibration = !vibration; Save(); RefreshSettings();
            if (vibration && !Application.isEditor) Handheld.Vibrate();
        }
        void CloseDesignHint() { if (hintDesign) hintDesign.SetActive(false); }
        void AcquireDesignHint(bool free)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (free && GiftClaimed(today)) { Notice("今日免费提示已领取", 2); return; }
            if (!free && coins < 100) { Notice("金币不足，需要 100 金币", 2); return; }
            if (free) giftDate = today; else coins -= 100;
            hints++; Save(); CloseDesignHint(); UpdateHUD(); UseHint();
        }
    }
}
#endif
