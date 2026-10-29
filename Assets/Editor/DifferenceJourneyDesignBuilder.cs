#if ENABLE_UI_UGUI
using System;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceJourneyDesignBuilder
{
    const string Art = "Assets/Hotfix/UI/UIAssets/FindDifferences/FigmaOctober/";
    const string ExistingArt = "Assets/Bundles/UI/UIDifferences/Art/Figma/";
    const float Width = 1179, Height = 2556, Spacing = 218;
    static readonly float[] NodeX = { 589, 170, 589, 1009 };

    public static void BuildJourney(GameObject root)
    {
        var ui = root.GetComponent<UIDifferences>();
        var stage = ui.stage;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Bundles/Font/Inter-UI-Bold-5.ttf");
        var page = Rect(stage, "FigmaJourney", 0, 0, Width, Height);
        Image(page, "Background", 0, 0, Width, Height, Sprite("JourneyBackground")).raycastTarget = true;
        var scrollRoot = Rect(page, "Scroll", 50, 405, 1092, 1663);
        var scroll = Component<UnityEngine.UI.ScrollRect>(scrollRoot);
        var viewport = Image(scrollRoot, "Viewport", 0, 0, 1092, 1663, null);
        viewport.raycastTarget = true;
        Component<UnityEngine.UI.Mask>(viewport.transform).showMaskGraphic = false;
        var contentHeight = (UIDifferences.JourneyLevelCount - 1) * Spacing + 340;
        var content = Rect(viewport.transform, "Content", 0, 0, 1092, contentHeight);
        scroll.viewport = viewport.rectTransform; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 80;
        var paths = Rect(content, "Paths", 0, 0, 1092, contentHeight);
        var curve = Sprite("JourneyPath");
        for (var i = 0; i < UIDifferences.JourneyLevelCount - 1; i++)
        {
            var from = new Vector2(NodeX[i % NodeX.Length] - 50, contentHeight - 170 - i * Spacing);
            var to = new Vector2(NodeX[(i + 1) % NodeX.Length] - 50, from.y - Spacing);
            Connect(paths, "Connection" + i, from, to, curve, NodeX[i % NodeX.Length] == 589);
        }
        var buttons = new UnityEngine.UI.Button[UIDifferences.JourneyLevelCount];
        for (var segment = 0; segment * 7 < buttons.Length; segment++)
        {
            var segmentY = contentHeight - (segment + 1) * 7 * Spacing;
            var group = Rect(content, "MapSegment" + segment, 0, segmentY, 1092, 7 * Spacing);
            for (var j = 0; j < 7 && segment * 7 + j < buttons.Length; j++)
            {
                var index = segment * 7 + j;
                var y = contentHeight - 170 - index * Spacing - segmentY;
                var image = Image(group, "Level" + (index + 1), NodeX[index % NodeX.Length] - 50 - 133, y - 135.5f, 266, 271,
                    Sprite(index == 0 ? "JourneyGold" : "JourneySilver"));
                var button = MakeButton(image);
                button.interactable = index == 0;
                Label(image.transform, "Label", 20, 81, 226, 135, (index + 1).ToString(), 90, font);
                buttons[index] = button;
            }
        }
        content.anchoredPosition = new Vector2(0, contentHeight - 1663);
        Image(page, "Title", 215, 38, 754, 377, Sprite("JourneyTitle"));
        var play = MakeButton(Image(page, "Play", 117, 2068, 946, 294, Sprite("JourneyPlay")));
        var playLabel = Label(play.transform, "Label", 20, 30, 906, 220, "Play Level 1", 90, font);
        var close = MakeButton(Image(page, "Close", 1050, 42, 80, 80, AssetDatabase.LoadAssetAtPath<Sprite>(ExistingArt + "Close.png")));

        var entry = stage.Find("FigmaHome/Journey");
        if (!entry) throw new InvalidOperationException("The authored home Journey banner is missing.");
        var entryImage = Component<UnityEngine.UI.Image>(entry);
        entryImage.color = Color.clear; entryImage.raycastTarget = true;
        var entryButton = MakeButton(entryImage);
        entryButton.interactable = false;
        var bannerFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Bundles/Font/Inter-UI-Medium-3.ttf");
        var lockedLabel = Label(entry, "LevelTips", 13, 41, 994, 73,
            "Complete 20 levels to unlock Christmas Carnival!", 30, bannerFont);
        lockedLabel.fontStyle = FontStyle.Normal;
        UnityEngine.Object.DestroyImmediate(lockedLabel.GetComponent<UnityEngine.UI.Outline>());
        var lockedShadow = Component<UnityEngine.UI.Shadow>(lockedLabel.transform);
        lockedShadow.effectColor = new Color(0, .16f, .24f, .55f); lockedShadow.effectDistance = new Vector2(0, -1);
        var unlocked = Label(entry, "UnlockedLabel", 13, 60.5f, 994, 80,
            "The holiday fun begins—jump in and play!", 36, bannerFont);
        unlocked.fontStyle = FontStyle.Normal;
        unlocked.gameObject.SetActive(false);
        var locked = new[] { entry.Find("CurLevel"), entry.Find("Scrollbar"), entry.Find("LevelTips") };
        var progressBar = entry.Find("Scrollbar").GetComponent<UnityEngine.UI.Scrollbar>();
        var progressColors = progressBar.colors; progressColors.disabledColor = progressColors.normalColor;
        progressBar.colors = progressColors;
        progressBar.interactable = false;
        foreach (var graphic in progressBar.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        var data = new SerializedObject(ui);
        void Bind(string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;
        Bind("journeyDesign", page.gameObject); Bind("journeyScroll", scroll);
        Bind("journeyPlayButton", play); Bind("journeyCloseButton", close); Bind("journeyEntryButton", entryButton);
        Bind("journeyPlayLabel", playLabel); Bind("journeyUnlockedLabel", unlocked);
        Bind("journeyUnlockProgress", entry.Find("CurLevel").GetComponent<UnityEngine.UI.Text>());
        Bind("journeyUnlockBar", progressBar);
        Bind("journeyLockOverlay", entry.Find("JourneyMask/MaskBg").gameObject);
        Bind("journeyCurrentNodeSprite", Sprite("JourneyGold")); Bind("journeyDefaultNodeSprite", Sprite("JourneySilver"));
        var buttonArray = data.FindProperty("journeyLevelButtons"); buttonArray.arraySize = buttons.Length;
        for (var i = 0; i < buttons.Length; i++) buttonArray.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        var lockedArray = data.FindProperty("journeyLockedObjects"); lockedArray.arraySize = locked.Length;
        for (var i = 0; i < locked.Length; i++) lockedArray.GetArrayElementAtIndex(i).objectReferenceValue = locked[i].gameObject;
        data.ApplyModifiedPropertiesWithoutUndo();
        page.gameObject.SetActive(false);
    }

    public static void CheckRouteGeometry()
    {
        for (var i = 1; i < UIDifferences.JourneyLevelCount; i++)
        {
            var separation = new Vector2(NodeX[i % NodeX.Length] - NodeX[(i - 1) % NodeX.Length], Spacing);
            if (separation.magnitude < 271) throw new InvalidOperationException("Adjacent Journey nodes overlap at level " + (i + 1));
        }
    }

    static void Connect(Transform parent, string name, Vector2 from, Vector2 to, Sprite curve, bool fromCenter)
    {
        if (Mathf.Approximately(from.x, to.x))
        {
            var straight = Image(parent, name, from.x - 16, to.y, 32, from.y - to.y, null);
            straight.color = new Color32(191, 45, 74, 255);
            return;
        }
        var size = curve.rect.size;
        var left = new Vector2(size.x * 19 / 439, 0);
        var right = new Vector2(size.x, size.y * 220 / 236);
        var sourceFrom = fromCenter ? right : left;
        var sourceTo = fromCenter ? left : right;
        var scale = new Vector2((to.x - from.x) / (sourceTo.x - sourceFrom.x), (to.y - from.y) / (sourceTo.y - sourceFrom.y));
        var center = from - Vector2.Scale(sourceFrom - size * .5f, scale);
        var image = Image(parent, name, 0, 0, size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y), curve);
        var rect = image.rectTransform;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(center.x, -center.y);
        rect.localScale = new Vector3(Mathf.Sign(scale.x), Mathf.Sign(scale.y), 1);
    }

    static T Component<T>(Transform parent) where T : Component
    { var value = parent.GetComponent<T>(); return value ? value : parent.gameObject.AddComponent<T>(); }
    static Sprite Sprite(string name)
    { var value = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png"); return value ? value : throw new InvalidOperationException("Missing imported Journey sprite: " + name); }
    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var existing = parent.Find(name);
        var rect = existing ? (RectTransform)existing : (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.localScale = Vector3.one; rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
    {
        var image = Component<UnityEngine.UI.Image>(Rect(parent, name, x, y, w, h));
        image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
        return image;
    }
    static UnityEngine.UI.Button MakeButton(UnityEngine.UI.Image image)
    {
        image.raycastTarget = true;
        var button = Component<UnityEngine.UI.Button>(image.transform); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = Color.white; colors.disabledColor = Color.white;
        button.colors = colors; button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        return button;
    }
    static UnityEngine.UI.Text Label(Transform parent, string name, float x, float y, float w, float h, string text, int size, Font font)
    {
        var label = Component<UnityEngine.UI.Text>(Rect(parent, name, x, y, w, h));
        label.font = font; label.text = text; label.fontSize = size; label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = Component<UnityEngine.UI.Outline>(label.transform);
        outline.effectColor = new Color(0, .16f, .24f, .75f); outline.effectDistance = new Vector2(2, -2);
        return label;
    }
}
#endif
