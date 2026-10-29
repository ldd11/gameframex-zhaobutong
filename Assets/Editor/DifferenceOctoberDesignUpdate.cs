#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DifferenceOctoberDesignUpdate
{
    const string Prefab = "Assets/Bundles/UI/UIDifferences/UIDifferences.prefab";
    const string Art = "Assets/Hotfix/UI/UIAssets/FindDifferences/FigmaOctober/";
    const string ExistingArt = "Assets/Bundles/UI/UIDifferences/Art/Figma/";
    const string Request = "Temp/FindDifferences-october.request";
    const string Report = "Temp/FindDifferences-october.txt";
    static Font font;
    static DifferenceOctoberDesignUpdate() { EditorApplication.update += Tick; }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        var action = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if (action == "apply") Apply();
            else if (action == "check") Validate();
            else if (action == "journey") DifferenceJourneyCheck.Run();
            else if (action == "run") { DifferenceOctoberPlayCheck.Run(); return; }
            else if (action == "start") EditorApplication.isPlaying = true;
            else if (action == "stop") EditorApplication.isPlaying = false;
            File.WriteAllText(Report, "PASS: " + action);
        }
        catch (Exception error) { File.WriteAllText(Report, "FAIL: " + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Find Differences/Apply October Figma Design")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before updating the prefab.");
        ImportArt();
        var editing = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var inStage = editing != null && editing.assetPath == Prefab;
        var root = inStage ? editing.prefabContentsRoot : PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var level = root.GetComponent<UIDifferences>().levelLabel;
            level.gameObject.SetActive(true);
            level.text = "Level 1";
            level.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Bundles/Font/Inter-UI-Bold-5.ttf");
            level.fontSize = 60; level.fontStyle = FontStyle.Normal;
            level.color = new Color32(218, 234, 255, 255); level.alignment = TextAnchor.MiddleCenter;
            level.resizeTextForBestFit = false; level.raycastTarget = false;
            level.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 73);
            foreach (var shadow in level.GetComponents<UnityEngine.UI.Shadow>()) shadow.enabled = false;
            BuildPause(root);
            DifferenceProfileDesignBuilder.BuildProfile(root);
            DifferenceJourneyDesignBuilder.BuildJourney(root);
            // The full winter artwork already includes the fireworks, so an extra crop creates a visible seam.
            var duplicateFireworks = root.transform.Find("Stage/FigmaJourney/Fireworks");
            if (duplicateFireworks) UnityEngine.Object.DestroyImmediate(duplicateFireworks.gameObject);
            RestoreSnow(root, "FigmaHome", "xue");
            RestoreSnow(root, "FigmaLoading", "xue (1)");
            CropGraphic(root.transform.Find("Stage/FigmaPause/Card/Home/Icon").GetComponent<Image>(), new Vector4(.1192053f, .11711325f, .7483444f, .7584803f));
            foreach (var image in uiLocks(root)) CropGraphic(image, new Vector4(.1880734f, .08069883f, .6108562f, .8252912f));
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { if (!inStage) PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Validate();
    }

    static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (var file in Directory.GetFiles(Art, "*.png"))
        {
            var path = file.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var name = Path.GetFileNameWithoutExtension(path);
            importer.maxTextureSize = name.StartsWith("ProfileFrame") || name == "JourneyGold" || name == "JourneySilver" ? 512 : 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100;
            importer.SaveAndReimport();
        }
    }

    static Image[] uiLocks(GameObject root) => root.GetComponent<UIDifferences>().profile.GetComponentsInChildren<Image>(true).Where(image => image.name == "Lock").ToArray();

    static void RestoreSnow(GameObject root, string page, string name)
    {
        var parent = root.transform.Find("Stage/" + page);
        if (parent.Find(name)) return;
        var snow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Bundles/UI/UIDifferences/Effect/SelectEffect/Snow/xue.prefab"), parent);
        snow.name = name; snow.layer = parent.gameObject.layer;
        var rect = (RectTransform)snow.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(100,100);
        rect.localScale = Vector3.one * 100; rect.localRotation = Quaternion.identity;
        rect.SetSiblingIndex(1);
    }

    static void CropGraphic(Image image, Vector4 crop, float alpha = 1)
    {
        var rect = image.rectTransform;
        var cropped = Picture(rect, "Source", -crop.x * rect.rect.width / crop.z, -crop.y * rect.rect.height / crop.w,
            rect.rect.width / crop.z, rect.rect.height / crop.w, image.sprite);
        cropped.color = new Color(1,1,1,alpha);
        image.color = Color.clear; image.preserveAspect = false;
        if (!image.GetComponent<RectMask2D>()) image.gameObject.AddComponent<RectMask2D>();
    }

    static void BuildPause(GameObject root)
    {
        var ui = root.GetComponent<UIDifferences>();
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Bundles/Font/Inter-UI-Bold-5.ttf");
        var data = new SerializedObject(ui);
        void Bind(string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;
        var pause = Popup(ui.stage, "FigmaPause", "Pause", true);
        Bind("pauseDesign", pause.gameObject); Bind("pauseCloseButton", pause.Find("Close").GetComponent<Button>());
        var card = pause.Find("Card");
        var types = new[] { "Music", "Sound", "Vibration" };
        foreach (var kind in types)
        {
            var i = Array.IndexOf(types, kind);
            var row = Button(card, kind, 101, 280 + i * 176, 778, 151, Load("PauseItemBg"));
            Picture(row.transform, "Icon", 34, 33, 103, 90, Existing(kind));
            Label(row.transform, "Label", 156, 29, 355, 90, kind, 52, new Color32(74,108,140,255), TextAnchor.MiddleLeft);
            var toggle = Picture(row.transform, "Toggle", 557, 39, 190, 79, Existing("ToggleOn"));
            Bind("pause" + kind + "Button", row); Bind("pause" + kind + "Toggle", toggle);
        }
        var home = Button(card, "Home", 118.5f, 845, 195, 195, Load("PauseHomeButton"));
        Picture(home.transform, "Icon", 52, 47, 90, 83, Load("PauseHome"));
        var resume = Button(card, "Continue", 351.5f, 845, 499, 195, Load("PauseContinueButton"));
        Label(resume.transform, "Label", 15, 15, 469, 150, "Continue", 70, Color.white);
        Bind("pauseContinueButton", resume); Bind("pauseHomeButton", home);
        var leave = Popup(ui.stage, "FigmaLeave", "Leave Level?", false);
        Bind("leaveDesign", leave.gameObject); Bind("leaveCloseButton", leave.Find("Close").GetComponent<Button>());
        card = leave.Find("Card");
        Label(card, "Description", 115, 339, 740, 181, "Are you sure you want to\nleave?", 56, new Color32(3,85,157,255));
        var keep = Button(card, "KeepPlaying", 136, 547, 720, 195, Load("LeaveKeepButton"));
        Label(keep.transform, "Label", 10, 15, 700, 150, "Keep Playing", 70, Color.white);
        var confirm = Button(card, "Leave", 139, 769, 720, 220, Load("LeaveButton"));
        Label(confirm.transform, "Label", 10, 20, 700, 160, "Leave", 80, new Color32(145,92,43,255));
        Bind("leaveKeepPlayingButton", keep); Bind("leaveConfirmButton", confirm);
        ui.backButton.image.sprite = Load("PauseIcon"); ui.backButton.image.color = Color.white;
        ui.backButton.image.preserveAspect = true;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform Popup(Transform stage, string name, string title, bool pause)
    {
        var page = Rect(stage, name, 0, 0, 1179, 2556);
        var scrim = Picture(page, "Scrim", 0, 0, 1179, 2556, null);
        scrim.color = new Color(0,0,0,.8f); scrim.raycastTarget = true;
        scrim.rectTransform.anchorMin = Vector2.zero; scrim.rectTransform.anchorMax = Vector2.one;
        scrim.rectTransform.pivot = Vector2.one * .5f; scrim.rectTransform.anchoredPosition = scrim.rectTransform.sizeDelta = Vector2.zero;
        var card = Rect(page, "Card", 105, 556, 970, 1225);
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
        card.anchoredPosition = new Vector2(0, 109.5f);
        if (pause) Picture(card, "Background", 0, 0, 970, 1225, Load("PausePanel"));
        else
        {
            Picture(card, "Outer", 0, 0, 970, 1074, Load("LeaveOuter"));
            Picture(card, "Inner", 41, 204, 891, 831, Load("LeaveInner"));
            Picture(card, "X", 793, 84, 81, 78, Existing("Close"));
        }
        Label(card, "Title", 80, 28, 810, 148, title, 85, Color.white);
        var close = Button(page, "Close", 870, 607, 140, 140, null); close.image.color = Color.clear;
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one * .5f;
        closeRect.anchoredPosition = new Vector2(344, 601);
        var motion = page.GetComponent<DifferencePopupMotion>() ?? page.gameObject.AddComponent<DifferencePopupMotion>();
        var serialized = new SerializedObject(motion);
        serialized.FindProperty("card").objectReferenceValue = card;
        serialized.FindProperty("close").objectReferenceValue = closeRect;
        serialized.FindProperty("scrim").objectReferenceValue = scrim;
        serialized.ApplyModifiedPropertiesWithoutUndo(); page.gameObject.SetActive(false);
        return page;
    }

    [MenuItem("Tools/Find Differences/Check October Figma Design")]
    public static void Validate()
    {
        DifferenceProfileDesignCheck.Validate(); DifferencePauseDesignCheck.Validate();
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            typeof(UIDifferences).GetMethod("InitView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ui, null);
            if (!ui.levelLabel.gameObject.activeSelf || ui.levelLabel.fontSize != 60 ||
                ui.levelLabel.color != (Color)new Color32(218, 234, 255, 255) || ui.levelLabel.alignment != TextAnchor.MiddleCenter ||
                ui.levelLabel.font != AssetDatabase.LoadAssetAtPath<Font>("Assets/Bundles/Font/Inter-UI-Bold-5.ttf"))
                throw new InvalidOperationException("Gameplay Level label must match its Figma font, size, color and visibility.");
            ui.JourneySelfCheck();
            var data = new SerializedObject(ui);
            if (data.FindProperty("journeyLevelButtons").arraySize != 200) throw new InvalidOperationException("Journey requires 200 level buttons.");
            foreach (var name in new[] { "journeyDesign", "journeyEntryButton", "journeyPlayButton", "journeyCloseButton", "journeyUnlockProgress", "journeyScroll" })
                if (!data.FindProperty(name).objectReferenceValue) throw new InvalidOperationException("Journey reference missing: " + name);
            Debug.Log("October Figma design, generated bindings, 20-level unlock and 200-level endpoint checks passed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png") ?? throw new InvalidOperationException("Missing art: " + name);
    static Sprite Existing(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ExistingArt + name + ".png");
    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var child = parent.Find(name);
        var rect = child ? (RectTransform)child : (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h); rect.localScale = Vector3.one;
        return rect;
    }
    static Image Picture(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
    {
        var rect = Rect(parent,name,x,y,w,h); var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = Color.white; image.raycastTarget = false; return image;
    }
    static Button Button(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
    {
        var image = Picture(parent,name,x,y,w,h,sprite); image.raycastTarget = true;
        var button = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None }; return button;
    }
    static Text Label(Transform parent, string name, float x, float y, float w, float h, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var rect = Rect(parent,name,x,y,w,h); var text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
        text.font = font; text.text = value; text.fontSize = size; text.color = color; text.raycastTarget = false;
        text.alignment = alignment; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
#endif
