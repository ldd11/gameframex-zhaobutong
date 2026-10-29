#if ENABLE_UI_UGUI
using System;
using System.Reflection;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceProfileDesignCheck
{
    [MenuItem("Tools/Find Differences/Check Profile Design Only")]
    public static void Validate()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(string field, object value) => typeof(UIDifferences).GetField(field, flags).SetValue(ui, value);
            object Call(string method, params object[] values) => typeof(UIDifferences).GetMethod(method, flags).Invoke(ui, values);
            void Refresh() => Call("RefreshProfile");
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
            Transform Find(string path) => ui.profile.transform.Find("Card/" + path);
            Transform Choice(int index) => Find("ChoicesScroll/Viewport/Choices/Choice" + index);
            Set("avatar", 0); Set("frame", 0); Set("draftAvatar", 0); Set("draftFrame", 0);
            Set("ownedAvatars", 0); Set("ownedFrames", 15); Set("coins", 10000);
            Set("completed", 0); Set("journeyCompleted", 0); Set("frameTab", false);
            ui.modal.SetActive(false); ui.profile.SetActive(true);
            Check(ui.profileFrameSprites != null && ui.profileFrameSprites.Length == 12, "Profile requires twelve authored frame sprites.");
            Check(ui.avatarSprites.Length == 38 && ui.avatars.Length == 38, "Profile must expose all 38 free avatars.");
            for (var i = 0; i < ui.avatarSprites.Length; i++)
                Check(ui.avatarSprites[i] && AssetDatabase.GetAssetPath(ui.avatarSprites[i]).EndsWith("/ProfileAvatar" + i + ".png"),
                    "Each avatar must use its own Figma asset: " + i);
            Check(ui.profileFrame.transform.name == "Decoration" && ui.homeFrame.transform.name == "Decoration" &&
                Find("User/Frame/AvatarMask/Avatar") == ui.profileAvatar.transform, "Decorative frames must preserve the avatar hierarchy.");
            Check(ui.profileAvatar.transform.parent.GetComponent<UnityEngine.UI.Mask>() &&
                ui.homeAvatar.transform.parent.GetComponent<UnityEngine.UI.Mask>() &&
                ui.profileFrame.transform.parent != ui.profileAvatar.transform.parent && ui.homeFrame.transform.parent != ui.homeAvatar.transform.parent,
                "Rounded avatar masks must not crop decorative frames.");
            var scroll = ui.profileChoicesScroll;
            Check(scroll && scroll.vertical && !scroll.horizontal && scroll.content == Find("ChoicesScroll/Viewport/Choices") &&
                scroll.viewport == Find("ChoicesScroll/Viewport") && scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>(),
                "The avatar grid requires its native scrolling viewport and clipping mask.");
            Check(!Find("Save").gameObject.activeSelf && Find("User/ConfirmName").GetComponent<UnityEngine.UI.Button>(),
                "Profile must use the name confirmation control without a legacy footer Save button.");
            Check(((RectTransform)ui.profile.transform.Find("Card")).sizeDelta == new Vector2(970, 1225), "Profile panel dimensions differ from Figma.");
            Check(((RectTransform)Find("User")).anchoredPosition == new Vector2(101, -240) &&
                ((RectTransform)Find("User")).sizeDelta == new Vector2(778, 201) &&
                ((RectTransform)Find("AvatarTab")).anchoredPosition == new Vector2(128, -476) &&
                ((RectTransform)Find("ChoicesScroll")).anchoredPosition == new Vector2(71, -628), "Profile row, tabs or grid geometry differs from Figma.");
            Check(Vector2.Distance(((RectTransform)ui.profile.transform.Find("Close")).anchoredPosition, new Vector2(365.2f, 597.75f)) < .01f,
                "Profile close button is misaligned with its drawn X.");
            Check(AssetDatabase.GetAssetPath(Find("User").GetComponent<UnityEngine.UI.Image>().sprite).EndsWith("/ProfileUserBg.png") &&
                AssetDatabase.GetAssetPath(Find("User/Nickname").GetComponent<UnityEngine.UI.Image>().sprite).EndsWith("/ProfileNicknameBg.png") &&
                AssetDatabase.GetAssetPath(ui.nicknameInput.textComponent.font).EndsWith("/NotoSansCJKsc-Medium.otf"),
                "Profile must retain its authored backgrounds and the CJK nickname font.");
            Refresh();
            Check(ui.profile.transform.GetSiblingIndex() == ui.stage.childCount - 1 && Find("Title").GetComponent<UnityEngine.UI.Text>().text == "Edit Profile",
                "Profile must display its title above the home page.");
            Check(Find("AvatarTab").GetComponent<UnityEngine.UI.Image>().sprite == ui.profileTabSelectedSprite &&
                Find("FrameTab").GetComponent<UnityEngine.UI.Image>().sprite == ui.profileFrameTabUnselectedSprite, "Avatar tab selection is wrong.");
            for (var i = 0; i < ui.avatarSprites.Length; i++)
                Check(Choice(i).gameObject.activeSelf && !Choice(i).Find("Lock").gameObject.activeSelf &&
                    !Choice(i).Find("Price").gameObject.activeSelf && !Choice(i).Find("PriceBadge").gameObject.activeSelf &&
                    Choice(i).Find("AvatarMask/Avatar").GetComponent<UnityEngine.UI.Image>().sprite == ui.avatarSprites[i],
                    "All avatars must remain free, visible and without price or lock graphics: " + i);
            Check(scroll.content.rect.height > scroll.viewport.rect.height, "All ten avatar rows must be reachable by scrolling.");
            scroll.verticalNormalizedPosition = 0;
            Check(scroll.content.anchoredPosition.y > 0, "Scrolling must reveal the final avatar row.");
            Call("ResetProfileChoicesScroll");
            Check(Mathf.Abs(scroll.content.anchoredPosition.y) < .01f, "Reopening the grid must reset to its first row.");
            Set("draftAvatar", 37); Refresh();
            Check(ui.profileAvatar.sprite == ui.avatarSprites[37] && Choice(37).Find("Selected").gameObject.activeSelf,
                "Avatar index 37 must work independently of the historical 32-bit ownership mask.");

            Set("frameTab", true); Set("draftFrame", 0); Refresh();
            Check(Find("FrameTab").GetComponent<UnityEngine.UI.Image>().sprite == ui.profileFrameTabSelectedSprite &&
                Find("AvatarTab").GetComponent<UnityEngine.UI.Image>().sprite == ui.profileTabUnselectedSprite,
                "Frame tab selection must preserve the left and right background shapes.");
            Check(AssetDatabase.GetAssetPath(ui.profileTabSelectedSprite).EndsWith("/ProfileTabLeftOn.png") &&
                AssetDatabase.GetAssetPath(ui.profileTabUnselectedSprite).EndsWith("/ProfileTabLeftOff.png") &&
                AssetDatabase.GetAssetPath(ui.profileFrameTabSelectedSprite).EndsWith("/ProfileTabRightOn.png") &&
                AssetDatabase.GetAssetPath(ui.profileFrameTabUnselectedSprite).EndsWith("/ProfileTabRightOff.png") &&
                Find("FrameTab/Label").GetComponent<UnityEngine.UI.Text>().color.a == 1 &&
                Mathf.Abs(Find("AvatarTab/Label").GetComponent<UnityEngine.UI.Text>().color.a - .51f) < .001f &&
                ((RectTransform)Find("AvatarTab")).sizeDelta == new Vector2(357, 117) &&
                ((RectTransform)Find("FrameTab")).sizeDelta == new Vector2(357, 117),
                "Each tab must keep its own corner geometry and show the correct selected label state.");
            Check(Choice(0).Find("Selected").gameObject.activeSelf && Choice(0).GetComponent<UnityEngine.UI.Image>().color == Color.white,
                "The default frame must retain its selected gold base.");
            var frameCosts = new[] { 288, 888, 1588, 2588 };
            var frameLevels = new[] { 30, 20, 50, 100 };
            for (var i = 0; i < 12; i++)
            {
                var cost = i >= 4 && i <= 7 ? frameCosts[i - 4] : 0;
                var required = i >= 8 ? frameLevels[i - 8] : 0;
                var requiresJourney = i == 9 || i == 10;
                var levelLabel = required > 0 ? (requiresJourney ? "Journey " : "Level ") + required : "";
                Check((int)Call("FrameCoinCost", i) == cost && (int)Call("FrameRequiredLevels", i) == required &&
                    (bool)Call("FrameRequiresJourney", i) == requiresJourney &&
                    (required == 0 || (string)Call("FrameLevelLabel", i) == levelLabel),
                    "Frame unlock rule differs from its requested coin cost, progress source or level threshold: " + i);
                Check((bool)Call("ProfileFrameUnlocked", i) == (i < 4), "Only the first four frames are initially free: " + i);
                Check(Choice(i).Find("Lock").gameObject.activeSelf == (i >= 4) &&
                    Choice(i).Find("Price").gameObject.activeSelf == (i >= 4) && Choice(i).Find("PriceBadge").gameObject.activeSelf == (i >= 4),
                    "Frame locks and condition pills must match ownership: " + i);
                if (i > 0 && i < 4) Check(Choice(i).GetComponent<UnityEngine.UI.Image>().color.a == 0, "Basic alternative frames must not keep a second gold base.");
                if (i >= 4)
                {
                    var price = Choice(i).Find("Price");
                    Check(price.GetComponent<UnityEngine.UI.Text>().text == (required > 0 ? levelLabel : cost.ToString()) &&
                        price.Find("Coin").gameObject.activeSelf == (cost > 0), "Coin and level condition labels are wrong: " + i);
                    Check(Choice(i).Find("PriceBadge").GetComponent<UnityEngine.UI.Image>().sprite &&
                        Choice(i).Find("PriceBadge").GetSiblingIndex() > Choice(i).Find("Frame").GetSiblingIndex() &&
                        Choice(i).Find("PriceBadge").GetSiblingIndex() < price.GetSiblingIndex(), "Price pills must render behind their text.");
                }
            }
            for (var i = 12; i < 38; i++) Check(!Choice(i).gameObject.activeSelf, "The frame tab must hide surplus avatar rows.");
            var grid = scroll.content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            Check(grid.spacing.y == 58 && grid.padding.bottom == 29 && scroll.content.rect.height >= 616,
                "Frame rows require 215px pitch and room for their bottom condition pills.");
            scroll.verticalNormalizedPosition = 0;
            var badgeCorners = new Vector3[4]; ((RectTransform)Choice(11).Find("PriceBadge")).GetWorldCorners(badgeCorners);
            Check(scroll.viewport.InverseTransformPoint(badgeCorners[0]).y >= scroll.viewport.rect.yMin - .01f,
                "The last level condition pill must remain fully visible at the bottom of the frame list.");
            for (var i = 8; i < 12; i++)
            {
                var required = frameLevels[i - 8];
                var progressField = i == 9 || i == 10 ? "journeyCompleted" : "completed";
                var otherProgressField = progressField == "completed" ? "journeyCompleted" : "completed";
                Set(progressField, 0); Set(otherProgressField, 200);
                Check(!(bool)Call("ProfileFrameUnlocked", i), "Progress from the other game mode must not unlock frame " + i);
                Set(progressField, required - 1);
                Check(!(bool)Call("ProfileFrameUnlocked", i), "Level frames must remain locked below their own progress threshold: " + i);
                Set(otherProgressField, 0); Set(progressField, required);
                Check((bool)Call("ProfileFrameUnlocked", i), "Level frames must unlock at the exact threshold in their own game mode: " + i);
            }
            Set("completed", 0); Set("journeyCompleted", 0); Set("ownedFrames", 15 | (1 << 11)); Set("draftFrame", 11); Refresh();
            Check((bool)Call("ProfileFrameUnlocked", 11) && !Choice(11).Find("Price").gameObject.activeSelf &&
                !Choice(11).Find("Lock").gameObject.activeSelf, "Previously owned frames must remain unlocked without another charge or new level gate.");
            Check(ui.profileFrame.sprite == ui.profileFrameSprites[11] && ui.profileFrame.rectTransform.sizeDelta == new Vector2(215, 215),
                "Special decorative frames must retain their authored sprite and 215px size.");
            Set("frameTab", false); Refresh();
            Check(Choice(37).Find("Frame").GetComponent<UnityEngine.UI.Image>().sprite == ui.profileFrameSprites[0] &&
                Choice(37).Find("AvatarMask/Avatar").GetComponent<UnityEngine.UI.Image>().sprite == ui.avatarSprites[37],
                "Returning to avatars must restore the default frame and the real avatar image.");
            Debug.Log("PASS: 38 free avatars, twelve authored frames, four free / four coin / four separate main-or-journey unlocks, historical ownership, condition pills, native grid scrolling and default gold frame.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
