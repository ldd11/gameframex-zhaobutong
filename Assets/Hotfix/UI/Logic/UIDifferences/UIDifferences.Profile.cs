#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        public Sprite profileTabSelectedSprite, profileTabUnselectedSprite;
        public Sprite profileFrameTabSelectedSprite, profileFrameTabUnselectedSprite;
        public Sprite profileFramePlaceholder;
        public Sprite[] profileFrameSprites;
        public UnityEngine.UI.ScrollRect profileChoicesScroll;
        int ProfileFrameCount => profileFrameSprites != null && profileFrameSprites.Length > 0 ? profileFrameSprites.Length : FrameColors.Length;

        void BindProfileDesign()
        {
            if (stage.Find("FigmaHome/Avatar")) Bind("FigmaHome/Avatar", ShowProfile);
            if (profile.transform.Find("Card/User/ConfirmName"))
                Bind("Profile/Card/User/ConfirmName", BeginNicknameEdit);
            nicknameInput.onEndEdit.AddListener(SaveNickname);
        }

        void BeginNicknameEdit()
        {
            if (AdInputBlocked) return;
            nicknameInput.Select();
            nicknameInput.ActivateInputField();
        }

        void SaveNickname(string text)
        {
            if (AdInputBlocked || nicknameInput.wasCanceled) return;
            if (!TryReadNickname(out var value))
            { nicknameInput.SetTextWithoutNotify(nickname); return; }
            nicknameInput.SetTextWithoutNotify(value);
            if (nickname == value) return;
            nickname = value; Save();
        }

        bool TryReadNickname(out string value)
        {
            value = nicknameInput.text.Trim();
            var length = Mathf.Min(value.Length, 16);
            if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            value = value.Substring(0, length);
            if (value.Length == 0) { Notice("Your nickname cannot be empty.", 2); return false; }
            return true;
        }

        public void ShowProfile()
        {
            if (AdInputBlocked) return;
            draftAvatar = avatar; draftFrame = frame; frameTab = false;
            RefreshWallet();
            nicknameInput.text = nickname; profile.SetActive(true); RefreshProfile(); ResetProfileChoicesScroll(); UpdateAdsVisibility();
        }

        void ResetProfileChoicesScroll()
        {
            if (!profileChoicesScroll) return;
            profileChoicesScroll.StopMovement(); profileChoicesScroll.verticalNormalizedPosition = 1;
        }

        void SelectProfileChoice(int index)
        {
            if (AdInputBlocked) return;
            if (index < 0 || index >= (frameTab ? ProfileFrameCount : avatarSprites.Length))
            { Notice("This item is unavailable.", 2); return; }
            if (frameTab) draftFrame = index; else draftAvatar = index;
            CommitProfile(false);
            draftAvatar = avatar; draftFrame = frame;
            RefreshProfile();
        }

        int FrameCoinCost(int index) => index switch { 4 => 288, 5 => 888, 6 => 1588, 7 => 2588, _ => 0 };
        int FrameRequiredLevels(int index) => index switch { 8 => 30, 9 => 20, 10 => 50, 11 => 100, _ => 0 };
        bool FrameRequiresJourney(int index) => index == 9 || index == 10;
        string FrameLevelLabel(int index) => (FrameRequiresJourney(index) ? "Journey " : "Level ") + FrameRequiredLevels(index);
        bool ProfileFrameUnlocked(int index) => index >= 0 && index < ProfileFrameCount &&
            (index < 4 || (index < 32 && (ownedFrames & (1 << index)) != 0) ||
             (FrameRequiredLevels(index) > 0 && (FrameRequiresJourney(index) ? journeyCompleted : completed) >= FrameRequiredLevels(index)));

        void RefreshProfile()
        {
            profileAvatar.sprite = avatarSprites[draftAvatar];
            ApplyProfileFrame(profileFrame, draftFrame);
            var choices = profile.transform.Find("Card/ChoicesScroll/Viewport/Choices");
            for (var i = 0; i < choices.childCount; i++)
            {
                var choice = choices.Find("Choice" + i);
                var valid = i < (frameTab ? ProfileFrameCount : avatarSprites.Length);
                choice.gameObject.SetActive(valid);
                if (!valid) continue;
                var owned = !frameTab || ProfileFrameUnlocked(i);
                var frameImage = choice.Find("Frame");
                var image = choice.GetComponent<UnityEngine.UI.Image>();
                ApplyProfileFrame(frameImage ? frameImage.GetComponent<UnityEngine.UI.Image>() : image, frameTab ? i : 0);
                choice.Find("AvatarMask/Avatar").GetComponent<UnityEngine.UI.Image>().sprite = frameTab && profileFramePlaceholder ? profileFramePlaceholder : avatarSprites[frameTab ? draftAvatar : i];
                SetText("Profile/Card/ChoicesScroll/Viewport/Choices/Choice" + i + "/Price", owned ? "" :
                    FrameRequiredLevels(i) > 0 ? FrameLevelLabel(i) : FrameCoinCost(i).ToString());
                var price = choice.Find("Price");
                LayoutProfilePrice((RectTransform)price, i);
                price.gameObject.SetActive(frameTab && !owned);
                var badge = choice.Find("PriceBadge");
                if (badge) badge.gameObject.SetActive(frameTab && !owned);
                var coin = price.Find("Coin");
                if (coin) coin.gameObject.SetActive(frameTab && !owned && FrameCoinCost(i) > 0 && FrameRequiredLevels(i) == 0);
                choice.Find("Selected").gameObject.SetActive(i == (frameTab ? draftFrame : draftAvatar));
                var locked = choice.Find("Lock");
                if (locked) locked.gameObject.SetActive(!owned);
            }
            var cost = ProfileCost();
            SetText("Profile/Card/Save/Label", cost > 0 ? "Unlock & Save  " + cost : "Save");
            At<UnityEngine.UI.Button>("Profile/Card/Save").interactable = coins >= cost;
            if (profileChoicesScroll)
            {
                var content = profileChoicesScroll.content;
                var grid = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                grid.spacing = new Vector2(49, frameTab ? 58 : 34);
                var padding = grid.padding; padding.bottom = frameTab ? 29 : 0; grid.padding = padding;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
            RefreshProfileDesign();
        }

        public void LayoutProfilePrice(RectTransform price, int index)
        {
            var coinUnlock = FrameCoinCost(index) > 0 && FrameRequiredLevels(index) == 0;
            price.anchorMin = price.anchorMax = Vector2.one * .5f;
            price.pivot = new Vector2(.5f, coinUnlock ? .5f : 1);
            price.anchoredPosition3D = coinUnlock ? new Vector3(11.3f, -83.69f, 0) : new Vector3(.5f, -66, 0);
            price.sizeDelta = new Vector2(coinUnlock ? 59.33f : 108.2f, 39.8f);
            price.localRotation = Quaternion.identity; price.localScale = Vector3.one;
            var coin = price.Find("Coin") as RectTransform;
            if (!coin) return;
            coin.anchorMin = coin.anchorMax = new Vector2(0, .5f);
            coin.pivot = new Vector2(1, .5f);
            coin.anchoredPosition3D = new Vector3(0, -1.900024f, 0);
            coin.sizeDelta = new Vector2(32, 32);
            coin.localRotation = Quaternion.identity; coin.localScale = Vector3.one;
        }

        void RefreshProfileDesign()
        {
            profile.transform.SetAsLastSibling();
            if (modal.activeSelf) modal.transform.SetAsLastSibling();
            SetText("Profile/Card/Title", "Edit Profile");
            SetText("Profile/Card/AvatarTab/Label", "Avatar");
            SetText("Profile/Card/FrameTab/Label", "Avatar Frame");
            foreach (var tab in new[] { "AvatarTab", "FrameTab" })
            {
                var image = At<UnityEngine.UI.Image>("Profile/Card/" + tab);
                var selected = (tab == "FrameTab") == frameTab;
                var sprite = tab == "AvatarTab"
                    ? (selected ? profileTabSelectedSprite : profileTabUnselectedSprite)
                    : (selected ? profileFrameTabSelectedSprite : profileFrameTabUnselectedSprite);
                if (sprite) { image.sprite = sprite; image.color = Color.white; }
                At<UnityEngine.UI.Text>("Profile/Card/" + tab + "/Label").color = new Color(1, 1, 1, selected ? 1 : .51f);
            }
        }

        void ApplyProfileFrame(UnityEngine.UI.Image image, int index)
        {
            if (profileFrameSprites != null && profileFrameSprites.Length > 0)
            {
                image.sprite = profileFrameSprites[index];
                image.color = Color.white;
                var parent = (RectTransform)image.transform.parent;
                var size = parent.rect.width * (index < 4 ? 1 : 215f / 157);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(.5f, .5f);
                image.rectTransform.anchoredPosition = Vector2.zero;
                image.rectTransform.sizeDelta = new Vector2(size, size);
                parent.GetComponent<UnityEngine.UI.Image>().color = index == 0 ? Color.white : Color.clear;
            }
            else image.color = FrameColors[index];
        }

        void RefreshHomeProfileDesign() { ApplyProfileFrame(homeFrame, frame); }

        int ProfileCost() => ProfileFrameUnlocked(draftFrame) ? 0 : FrameCoinCost(draftFrame);
        public void SaveProfile() { CommitProfile(true); }

        void CommitProfile(bool close)
        {
            if (AdInputBlocked) return;
            if (!TryReadNickname(out var value)) return;
            if (draftAvatar < 0 || draftAvatar >= avatarSprites.Length || draftFrame < 0 || draftFrame >= ProfileFrameCount)
            { Notice("This item is unavailable.", 2); return; }
            if (!ProfileFrameUnlocked(draftFrame) && FrameRequiredLevels(draftFrame) > 0)
            { Notice("Complete " + FrameLevelLabel(draftFrame) + " to unlock this frame.", 2.5f); return; }
            var cost = ProfileCost();
            if (coins < cost) { Notice("You need " + cost + " coins to unlock this frame.", 2); return; }
            coins -= cost; avatar = draftAvatar; frame = draftFrame;
            if (frame < 32) ownedFrames |= 1 << frame;
            nickname = value; Save();
            homeAvatar.sprite = avatarSprites[avatar]; RefreshHomeProfileDesign();
            if (close) profile.SetActive(false);
            RefreshWallet();
        }
    }
}
#endif
