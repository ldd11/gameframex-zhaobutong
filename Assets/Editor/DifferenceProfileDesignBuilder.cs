#if ENABLE_UI_UGUI
using System;
using System.Linq;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceProfileDesignBuilder
{
    const string Art = "Assets/Hotfix/UI/UIAssets/FindDifferences/FigmaOctober/";

    public static void BuildProfile(GameObject root)
    {
        var ui = root.GetComponent<UIDifferences>();
        var panel = Load("ProfilePanel");
        var frameBase = Load("ProfileAvatarFrame");
        var selected = Load("ProfileCheck");
        var pencil = Load("ProfilePencil");
        var locked = Load("ProfileLock");
        var userBackground = Load("ProfileUserBg");
        var nicknameBackground = Load("ProfileNicknameBg");
        var framePlaceholder = Load("ProfileFramePlaceholder");
        var tabOn = Load("ProfileTabLeftOn");
        var tabOff = Load("ProfileTabRightOff");
        var frames = Enumerable.Range(0, 12).Select(index => Load("ProfileFrame" + index)).ToArray();
        frames[0] = frameBase; // Keep the default gold border above opaque avatar artwork.
        // Keep the four previously appended animals at indices 8..11 for saved selections.
        ui.avatarSprites = Enumerable.Range(0, 38).Select(index => Load("ProfileAvatar" + index)).ToArray();
        var homeAvatarButton = Button(ui.stage.Find("FigmaHome"), "Avatar", 62, 127, 157, 157, frameBase);
        ui.homeAvatar = MaskedAvatar(homeAvatarButton.transform, "Animal", ui.avatarSprites[0], framePlaceholder);
        if (homeAvatarButton.transform.Find("Label")) homeAvatarButton.transform.Find("Label").gameObject.SetActive(false);
        var oldCard = ui.profile.transform.Find("Card");
        var font = oldCard.Find("Title").GetComponent<UnityEngine.UI.Text>().font;
        var settingsTitle = ui.stage.Find("FigmaSettings")?.GetComponentsInChildren<UnityEngine.UI.Text>(true).FirstOrDefault(text => text.text == "Settings");
        if (settingsTitle) font = settingsTitle.font;
        var nicknameFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Bundles/UI/UIDifferences/Art/NotoSansCJKsc-Medium.otf");
        if (!nicknameFont) throw new InvalidOperationException("Missing nickname font: NotoSansCJKsc-Medium.otf");
        var profile = (RectTransform)ui.profile.transform;
        Stretch(profile);
        var scrim = Image(profile, "Scrim", 0, 0, 1179, 2556, null, new Color(0, 0, 0, .7f));
        Stretch(scrim.rectTransform); scrim.raycastTarget = true;
        var card = Image(profile, "Card", 105, 560, 970, 1225, panel, Color.white).rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.anchoredPosition = new Vector2(0, 105.5f);
        Text(card, "Title", 80, 64, 810, 118, "Edit Profile", 85, Color.white, font);

        var close = Button(profile, "Close", 0, 0, 120, 120, null);
        close.image.color = Color.clear;
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(.5f, .5f);
        // ProfilePanel.svg already draws the X; align only its transparent hit area.
        closeRect.anchoredPosition = new Vector2(365.2f, 597.75f);
        foreach (Transform child in close.transform) child.gameObject.SetActive(false);

        var user = Image(card, "User", 101, 240, 778, 201, userBackground, Color.white).rectTransform;
        var userFrame = Image(user, "Frame", 41.5f, 22, 157, 157, frameBase, Color.white).rectTransform;
        userFrame.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
        ui.profileAvatar = MaskedAvatar(userFrame, "Avatar", ui.avatarSprites[0], framePlaceholder);
        ui.profileFrame = Image(userFrame, "Decoration", 0, 0, 157, 157, frames[0], Color.white);
        ui.profileFrame.preserveAspect = true;
        ui.profileFrame.transform.SetAsLastSibling();
        var nickname = Image(user, "Nickname", 228.5f, 42, 518, 117, nicknameBackground, Color.white).rectTransform;
        var input = nickname.GetComponent<UnityEngine.UI.InputField>();
        input.characterLimit = 16;
        input.textComponent = Text(nickname, "Text", 55, 0, 294, 117, "Player_1001", 40, new Color(.23f, .27f, .27f), nicknameFont);
        input.textComponent.alignment = TextAnchor.MiddleLeft;
        input.targetGraphic = nickname.GetComponent<UnityEngine.UI.Image>();
        input.targetGraphic.raycastTarget = true;
        ui.nicknameInput = input;
        Button(user, "ConfirmName", 607.5f, 53.5f, 94, 94, pencil);

        Image(card, "ChoicesCard", 69, 375, 832, 775, null, Color.clear);
        var avatarTab = Button(card, "AvatarTab", 128, 476, 357, 117, tabOn);
        var frameTab = Button(card, "FrameTab", 485, 476, 357, 117, tabOff);
        Text(avatarTab.transform, "Label", 0, 0, 357, 117, "Avatar", 39, Color.white, font);
        Text(frameTab.transform, "Label", 0, 0, 357, 117, "Avatar Frame", 39, new Color(1, 1, 1, .51f), font);
        ui.profileTabSelectedSprite = tabOn; ui.profileTabUnselectedSprite = Load("ProfileTabLeftOff");
        ui.profileFrameTabSelectedSprite = Load("ProfileTabRightOn"); ui.profileFrameTabUnselectedSprite = tabOff;
        ui.profileFrameSprites = frames;
        ui.profileFramePlaceholder = framePlaceholder;
        var homeFrameBase = homeAvatarButton.image;
        homeFrameBase.sprite = frameBase; homeFrameBase.color = Color.white; homeFrameBase.preserveAspect = true;
        var homeSize = ((RectTransform)homeFrameBase.transform).sizeDelta;
        var decorationSize = Mathf.Max(homeSize.x, homeSize.y);
        ui.homeFrame = Image(homeFrameBase.transform, "Decoration", (homeSize.x - decorationSize) / 2,
            (homeSize.y - decorationSize) / 2, decorationSize, decorationSize, frames[0], Color.white);
        ui.homeFrame.preserveAspect = true;
        ui.homeFrame.transform.SetAsLastSibling();
        var previousChoices = card.Find("Choices") ?? card.Find("ChoicesScroll/Viewport/Choices");
        var scrollRoot = Rect(card, "ChoicesScroll", 71, 628, 834, 499);
        var scroll = scrollRoot.GetComponent<UnityEngine.UI.ScrollRect>() ?? scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = Image(scrollRoot, "Viewport", 0, 0, 834, 499, null, Color.clear).rectTransform;
        Stretch(viewport); viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        if (!viewport.GetComponent<UnityEngine.UI.RectMask2D>()) viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        if (previousChoices && previousChoices.parent != viewport) previousChoices.SetParent(viewport, false);
        var choiceCount = Mathf.Max(ui.avatarSprites.Length, frames.Length);
        var choices = Rect(viewport, "Choices", 0, 0, 834, Mathf.CeilToInt(choiceCount / 4f) * 191 - 34);
        var grid = choices.GetComponent<UnityEngine.UI.GridLayoutGroup>() ?? choices.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(157, 157); grid.spacing = new Vector2(49, 34);
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal; grid.childAlignment = TextAnchor.UpperLeft;
        // Decorative frames extend 29px beyond the avatar slots; leave room inside the clipping viewport.
        grid.padding = new RectOffset(29, 29, 0, 0);
        var fitter = choices.GetComponent<UnityEngine.UI.ContentSizeFitter>() ?? choices.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = choices; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        ui.profileChoicesScroll = scroll;
        ui.avatars = new UnityEngine.UI.Image[ui.avatarSprites.Length];
        for (var index = 0; index < choiceCount; index++)
        {
            var choice = Button(choices, "Choice" + index, index % 4 * 206, index / 4 * 191, 157, 157, frameBase);
            Text(choice.transform, "Label", 0, 0, 157, 157, "", 20, Color.white, font);
            var avatar = MaskedAvatar(choice.transform, "Avatar", ui.avatarSprites[Mathf.Min(index, ui.avatarSprites.Length - 1)], framePlaceholder);
            if (index < ui.avatars.Length) ui.avatars[index] = avatar;
            var decoration = Image(choice.transform, "Frame", 0, 0, 157, 157, frames[0], Color.white);
            decoration.preserveAspect = true;
            var selection = Image(choice.transform, "Selected", 113, 113, 55, 55, selected, Color.white);
            Text(selection.transform, "Check", 0, 0, 55, 55, "", 20, Color.white, font).gameObject.SetActive(false);
            selection.gameObject.SetActive(index == 0);
            var lockIcon = Image(choice.transform, "Lock", 53, 49, 51, 68, locked, Color.white);
            lockIcon.preserveAspect = true; lockIcon.gameObject.SetActive(false);
            decoration.transform.SetAsLastSibling(); selection.transform.SetAsLastSibling(); lockIcon.transform.SetAsLastSibling();
            var price = Text(choice.transform, "Price", 0, 144, 157, 42, "", 28, new Color(.42f, .29f, .12f), font);
            ConfigurePrice(price);
            var priceBadge = Image(choice.transform, "PriceBadge", 0, 144, 157, 42,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bundles/UI/UIDifferences/Art/Figma/Rounded.png"), new Color32(255, 246, 216, 255));
            priceBadge.type = UnityEngine.UI.Image.Type.Sliced; priceBadge.pixelsPerUnitMultiplier = 1.2f;
            var priceCoin = Image(price.transform, "Coin", 9, 6, 32, 32,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bundles/UI/UIDifferences/Art/Figma/Coin.png"), Color.white);
            priceCoin.preserveAspect = true;
            ui.LayoutProfilePrice(price.rectTransform, index);
            priceBadge.transform.SetAsLastSibling();
            price.transform.SetAsLastSibling();
            priceBadge.gameObject.SetActive(false); price.gameObject.SetActive(false); priceCoin.gameObject.SetActive(false);
            choice.gameObject.SetActive(index < ui.avatarSprites.Length);
        }
        card.Find("Save").gameObject.SetActive(false);
        close.transform.SetAsLastSibling();
        ui.profile.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(choices);
        scroll.verticalNormalizedPosition = 1;
        ui.profile.SetActive(false);
        EditorUtility.SetDirty(ui);
    }

    static void ConfigurePrice(UnityEngine.UI.Text price)
    {
        price.fontStyle = FontStyle.Normal; price.fontSize = 28; price.lineSpacing = 1;
        price.supportRichText = true; price.alignment = TextAnchor.MiddleCenter; price.alignByGeometry = false;
        price.horizontalOverflow = HorizontalWrapMode.Wrap; price.verticalOverflow = VerticalWrapMode.Truncate;
        price.resizeTextForBestFit = true; price.resizeTextMinSize = 10; price.resizeTextMaxSize = 28;
        price.raycastTarget = false; price.maskable = true;
    }

    static Sprite Load(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        if (!sprite) throw new InvalidOperationException("Missing profile sprite: " + Art + name + ".png");
        return sprite;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
    }

    static UnityEngine.UI.Image MaskedAvatar(Transform parent, string name, Sprite sprite, Sprite roundedMask)
    {
        var existing = parent.Find(name) ?? parent.Find("AvatarMask/" + name);
        var mask = Image(parent, "AvatarMask", 5, 5, 147, 147, roundedMask, Color.white).rectTransform;
        var component = mask.GetComponent<UnityEngine.UI.Mask>() ?? mask.gameObject.AddComponent<UnityEngine.UI.Mask>();
        component.showMaskGraphic = false;
        if (existing && existing.parent != mask) existing.SetParent(mask, false);
        var avatar = Image(mask, name, 0, 0, 147, 147, sprite, Color.white);
        avatar.preserveAspect = true;
        return avatar;
    }

    static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var existing = parent.Find(name);
        var rect = existing ? (RectTransform)existing : (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (!existing) rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity; rect.gameObject.SetActive(true);
        return rect;
    }

    static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float width, float height, Sprite sprite, Color color)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Simple; image.color = color; image.raycastTarget = false;
        return image;
    }

    static UnityEngine.UI.Button Button(Transform parent, string name, float x, float y, float width, float height, Sprite sprite)
    {
        var image = Image(parent, name, x, y, width, height, sprite, Color.white);
        image.raycastTarget = true;
        var button = image.GetComponent<UnityEngine.UI.Button>() ?? image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = UnityEngine.UI.ColorBlock.defaultColorBlock;
        colors.pressedColor = new Color(.85f, .9f, .95f); button.colors = colors;
        foreach (var shadow in image.GetComponents<UnityEngine.UI.Shadow>()) shadow.enabled = false;
        return button;
    }

    static UnityEngine.UI.Text Text(Transform parent, string name, float x, float y, float width, float height, string value, int size, Color color, Font font)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var text = rect.GetComponent<UnityEngine.UI.Text>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        text.font = font; text.text = value; text.fontSize = size; text.fontStyle = FontStyle.Normal;
        text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        foreach (var shadow in text.GetComponents<UnityEngine.UI.Shadow>()) shadow.enabled = false;
        return text;
    }
}
#endif
