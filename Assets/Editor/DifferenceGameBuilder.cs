#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceGameBuilder
{
    const string Folder = "Assets/Bundles/UI/UIDifferences/";
    static Font font;
    static Sprite round, circle, ring;
    static Sprite[] icons;
    static readonly Color Ink = new Color(.22f,.16f,.13f);
    static readonly Color Blue = new Color(.25f,.44f,.83f);
    static readonly Color Navy = new Color(.14f,.19f,.34f);
    static readonly Color Cream = new Color(1,.965f,.84f);
    static readonly Color Green = new Color(.42f,.91f,.035f);
    static readonly Vector4[] CafeRegions = {
        new Vector4(.17f,.14f,.075f,.19f), new Vector4(.846f,.255f,.067f,.12f),
        new Vector4(.49f,.80f,.15f,.15f), new Vector4(.25f,.635f,.13f,.13f),
        new Vector4(.41f,.59f,.09f,.064f), new Vector4(.332f,.55f,.075f,.083f),
        new Vector4(.442f,.53f,.054f,.096f), new Vector4(.878f,.75f,.19f,.22f),
        new Vector4(.105f,.818f,.21f,.285f), new Vector4(.600f,.11f,.04f,.05f),
        new Vector4(.04f,.37f,.05f,.085f), new Vector4(.10f,.45f,.05f,.085f),
        new Vector4(.235f,.258f,.035f,.029f), new Vector4(.397f,.477f,.085f,.10f),
        new Vector4(.497f,.675f,.074f,.135f)
    };
    static Vector4 Region(float x,float y,float w,float h) { return new Vector4(x+w/2,y+h/2,w,h); }

    [MenuItem("Tools/Find Differences/Build Game Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式");
        if (File.Exists(Folder + "UIDifferences.prefab")) { RefreshCurrentPrefabData(); return; }
        DifferenceRound.SelfCheck();
        font = AssetDatabase.LoadAssetAtPath<Font>(Folder + "Art/NotoSansCJKsc-Medium.otf");
        if (!font) throw new InvalidOperationException("中文字体尚未导入");
        round = Shape("Rounded",0); circle = Shape("Circle",1); ring = Shape("Ring",2);
        icons = SpriteGrid("UIIconsV2",4,3);
        var animals = SpriteGrid("AvatarsV2",4,2);
        var data = new[] {
            new DifferenceLevel { title="温馨客厅", original=ImportSprite("Art/LivingRoom.png"), changed=ImportSprite("Art/LivingRoomChanged.png"), regions=new[] {
                Region(0,.100586f,.116536f,.155273f), Region(.125651f,.068359f,.064453f,.074219f),
                Region(.405599f,.126953f,.042318f,.066406f), Region(.719401f,.081055f,.072266f,.105469f),
                Region(.077474f,.370117f,.057943f,.073242f), Region(.268880f,.361328f,.075521f,.088867f),
                Region(.563802f,.323242f,.126302f,.168945f), Region(.343099f,.584961f,.098958f,.070313f),
                Region(.039714f,.708008f,.052734f,.081055f), Region(.771484f,.514648f,.102865f,.144531f) } },
            new DifferenceLevel { title="花园花店", original=ImportSprite("Art/FlowerGarden.png"), changed=ImportSprite("Art/FlowerGardenChanged.png"), regions=new[] {
                Region(.057292f,.148438f,.024740f,.045898f), Region(.208333f,.130859f,.037760f,.070313f),
                Region(.718750f,.129883f,.045573f,.069336f), Region(.166016f,.456055f,.037109f,.048828f),
                Region(.044922f,.554688f,.161458f,.169922f), Region(.929688f,.238281f,.057943f,.094727f),
                Region(.820313f,.466797f,.109375f,.151367f), Region(.906901f,.708984f,.046875f,.090820f),
                Region(.068359f,.791992f,.048828f,.059570f), Region(.588542f,.806641f,.156250f,.135742f) } },
            new DifferenceLevel { title="海边咖啡馆", original=ImportSprite("Art/Cafe.png"), changed=ImportSprite("Art/CafeChanged.png"), regions=CafeRegions }
        };
        var root = new GameObject("UIDifferences",typeof(RectTransform));
        try
        {
            var rect=(RectTransform)root.transform; rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;
            root.AddComponent<UnityEngine.UI.Image>().color=Color.black;
            var ui=root.AddComponent<UIDifferences>(); ui.levels=data; ui.avatarSprites=animals;
            ui.designSprites = LoadDesignSprites();
            ui.stage=Rect(root.transform,"Stage",0,0,720,1280);
            ui.stage.anchorMin=ui.stage.anchorMax=ui.stage.pivot=new Vector2(.5f,.5f);ui.stage.anchoredPosition=Vector2.zero;
            BuildHome(ui);
            BuildShop(ui);
            BuildRanking(ui);
            BuildPlay(ui);
            BuildNavigation(ui);
            BuildSettings(ui);
            BuildProfile(ui);
            BuildMusic(ui);
            BuildAchievements(ui);
            BuildAlbum(ui);
            BuildDialog(ui);
            var toast=Rect(ui.stage,"Toast",120,144,480,52);
            Image(toast,"Background",0,0,480,52,new Color(.1f,.2f,.37f,.85f),round);
            ui.noticeLabel=Label(toast,"Text",4,0,472,52,"",24,Color.white);toast.gameObject.SetActive(false);
            ui.audioSource=root.AddComponent<AudioSource>();ui.audioSource.playOnAwake=false;
            ui.musicSource=root.AddComponent<AudioSource>();ui.musicSource.playOnAwake=false;ui.musicSource.loop=true;ui.musicSource.volume=.16f;
            ui.foundSound=Tone("Found",880);ui.missSound=Tone("Miss",190);ui.winSound=Tone("Win",1320);
            ui.musicTracks=new AudioClip[4];for(var i=0;i<4;i++)ui.musicTracks[i]=Music(i);
            foreach(var page in new[]{ui.play,ui.shop,ui.ranking,ui.settings,ui.profile,ui.musicPanel,ui.achievements,ui.album,ui.modal})page.SetActive(false);
            ApplyGameplayArt(ui);
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"UIDifferences.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();Verify();
        GameFrameX.UI.UGUI.Editor.UGUICodeGenerator.Generate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"UIDifferences.prefab"));
        Debug.Log("Find Differences V2: 3 unique scenes, complete UI pages, profile, music, saved rounds and animated feedback built.");
    }

    static Sprite[] LoadDesignSprites()
    {
        var paths = Directory.GetFiles(Folder + "Art/Figma", "*.png").OrderBy(path => path).ToArray();
        if (paths.Length == 0) throw new InvalidOperationException("Art/Figma 中没有 PNG 图片");
        return paths.Select(file =>
        {
            var path = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            // Newly added UI images use Unity's Default texture settings until explicitly imported as sprites.
            if (importer && importer.textureType == TextureImporterType.Default && importer.spriteImportMode == SpriteImportMode.None)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) throw new InvalidOperationException("无法加载 UI Sprite：" + path + "。请检查图片是否有效及 Sprite 导入设置。");
            return sprite;
        }).ToArray();
    }

    [MenuItem("Tools/Find Differences/Refresh Current Prefab Data")]
    public static void RefreshCurrentPrefabData()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式");
        var path = Folder + "UIDifferences.prefab";
        var sprites = LoadDesignSprites();
        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var editingCurrent = prefabStage != null && prefabStage.assetPath == path;
        var root = editingCurrent ? prefabStage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            RefreshFigmaBindings(ui);
            ui.designSprites = sprites;
            if (!ui.crossTop) ui.crossTop = ui.upperImage.transform.Find("Miss").GetComponent<UnityEngine.UI.Graphic>();
            if (!ui.crossBottom) ui.crossBottom = ui.lowerImage.transform.Find("Miss").GetComponent<UnityEngine.UI.Graphic>();
            RefreshSpriteReferences(ui, sprites);
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                RefreshSpriteReferences(image, sprites);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (!editingCurrent) PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Verify();
        GameFrameX.UI.UGUI.Editor.UGUICodeGenerator.Generate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        Debug.Log($"当前预制体数据已更新：{sprites.Length} 张 UI 图片，已按现有节点重新生成 UI 代码；布局保留。");
    }

    [MenuItem("Tools/Find Differences/Refresh UI Bindings Only")]
    public static void RefreshUIBindingsOnly()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式");
        var path = Folder + "UIDifferences.prefab";
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var editing = stage != null && stage.assetPath == path;
        var root = editing ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            RefreshFigmaBindings(root.GetComponent<UIDifferences>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (!editing) PrefabUtility.UnloadPrefabContents(root); }
        GameFrameX.UI.UGUI.Editor.UGUICodeGenerator.Generate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    [MenuItem("Tools/Find Differences/Disable Scene UI Preview")]
    public static void DisableSceneUIPreview()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式");
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.path != "Assets/Scenes/Launcher.unity") continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var ui in root.GetComponentsInChildren<UIDifferences>(true))
                {
                    if (!ui.gameObject.activeSelf) continue;
                    Undo.RecordObject(ui.gameObject, "Disable duplicate scene UI preview");
                    ui.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ui.gameObject);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }

    static void RefreshFigmaBindings(UIDifferences ui)
    {
        if (!ui.foundFlightPrefab)
            ui.foundFlightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Effect/SelectEffect/a1a1/xingxingtuowei2.prefab");
        if (!ui.foundArrivalPrefab)
            ui.foundArrivalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Effect/SelectEffect/a1a1/dagoufaguang.prefab");
        if (!ui.stage.Find("FigmaHome")) return;
        var data = new SerializedObject(ui);
        foreach (var binding in new[] {
            "designSettingsButton|FigmaHome|Settings", "designStartButton|FigmaHome|Start",
            "designSettingsClose|FigmaSettings|Close", "designMusicButton|FigmaSettings|Music",
            "designSoundButton|FigmaSettings|Sound", "designVibrationButton|FigmaSettings|Vibration",
            "designFailClose|FigmaFail|Close", "designContinueButton|FigmaFail|Continue", "designRetryButton|FigmaFail|Retry",
            "designHintClose|FigmaHint|Close", "designFreeButton|FigmaHint|Free", "designBuyButton|FigmaHint|Buy",
            "designNextButton|FigmaVictory|Next" })
        {
            var parts = binding.Split('|');
            var property = data.FindProperty(parts[0]);
            if (property.objectReferenceValue) continue;
            var page = ui.stage.Find(parts[1]);
            property.objectReferenceValue = UniqueChild<UnityEngine.UI.Button>(page, parts[2]);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (var motion in ui.GetComponentsInChildren<DifferencePopupMotion>(true))
        {
            var popup = new SerializedObject(motion);
            if (!popup.FindProperty("card").objectReferenceValue)
                popup.FindProperty("card").objectReferenceValue = UniqueChild<RectTransform>(motion.transform, "Card");
            if (!popup.FindProperty("close").objectReferenceValue)
                popup.FindProperty("close").objectReferenceValue = UniqueChild<RectTransform>(motion.transform, "Close");
            if (!popup.FindProperty("scrim").objectReferenceValue)
                popup.FindProperty("scrim").objectReferenceValue = UniqueChild<UnityEngine.UI.Image>(motion.transform, "Scrim");
            popup.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static T UniqueChild<T>(Transform root, string name) where T : Component
    {
        var matches = root.GetComponentsInChildren<T>(true).Where(item => item.name == name).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException($"{root.name} 中需要唯一的 {name} ({typeof(T).Name})，找到 {matches.Length} 个，请检查绑定。");
        return matches[0];
    }

    [MenuItem("Tools/Find Differences/Update Gameplay Art")]
    public static void UpdateGameplayArt()
    {
        RefreshCurrentPrefabData();
    }

    static void RefreshSpriteReferences(UnityEngine.Object target, Sprite[] sprites)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is Sprite old)) continue;
            var oldPath = AssetDatabase.GetAssetPath(old);
            if (!oldPath.StartsWith(Folder + "Art/", StringComparison.Ordinal) || oldPath.StartsWith(Folder + "Art/Figma/", StringComparison.Ordinal)) continue;
            var name = old.name;
            switch (name)
            {
                case "HintBulb": name = "LoadingMagnifier"; break;
                case "HintButton": name = "RoundedPanel"; break;
                case "PlayBackdropV2": name = "PlayBackground"; break;
                case "BeachV2": name = "HomeArtwork"; break;
            }
            var replacement = Array.Find(sprites, sprite => sprite.name == name);
            if (replacement) property.objectReferenceValue = replacement;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ApplyGameplayArt(UIDifferences ui)
    {
        var glowShader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "Art/FoundGlow.shader");
        if (!glowShader) throw new InvalidOperationException("找不到发光 Shader：" + Folder + "Art/FoundGlow.shader");
        var glowPath = Folder + "Art/FoundGlow.mat";
        ui.foundFlightMaterial = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
        if (!ui.foundFlightMaterial)
        {
            ui.foundFlightMaterial = new Material(glowShader) { name = "FoundGlow" };
            AssetDatabase.CreateAsset(ui.foundFlightMaterial, glowPath);
        }
        else ui.foundFlightMaterial.shader = glowShader;
        EditorUtility.SetDirty(ui.foundFlightMaterial);
        var hintShader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "Art/HintSpotlight.shader");
        if (!hintShader) throw new InvalidOperationException("找不到提示聚光 Shader");
        var hintPath = Folder + "Art/HintSpotlight.mat";
        ui.hintSpotlightMaterial = AssetDatabase.LoadAssetAtPath<Material>(hintPath);
        if (!ui.hintSpotlightMaterial)
        {
            ui.hintSpotlightMaterial = new Material(hintShader) { name = "HintSpotlight" };
            AssetDatabase.CreateAsset(ui.hintSpotlightMaterial, hintPath);
        }
        else ui.hintSpotlightMaterial.shader = hintShader;
        EditorUtility.SetDirty(ui.hintSpotlightMaterial);
        ui.hintHandSprite = ImportSprite("Art/Figma/HintHand.png");
        var handImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + "Art/Figma/HintHand.png");
        handImporter.maxTextureSize = 512; handImporter.SaveAndReimport();
        ui.progressQuestion = ImportSprite("Art/Figma/ProgressQuestion.png");
        ui.progressCheck = ImportSprite("Art/Figma/ProgressCheck.png");
        var layout = ui.stage.Find("Play/Progress").GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 4;
        foreach (var dot in ui.progressDots)
        {
            dot.sprite = ui.progressQuestion; dot.color = Color.white;
            dot.rectTransform.sizeDelta = new Vector2(41, 41);
            dot.rectTransform.pivot = new Vector2(.5f, .5f);
            // Keep the bound text's state for debugging; artwork supplies the visible glyph.
            var legacyLabel = dot.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacyLabel) legacyLabel.enabled = false;
        }
        var button = ui.hintButton.GetComponent<UnityEngine.UI.Image>();
        button.sprite = ImportSprite("Art/HintButton.png"); button.type = UnityEngine.UI.Image.Type.Simple;
        button.color = Color.white; button.preserveAspect = true;
        var shadow = button.GetComponent<UnityEngine.UI.Shadow>();
        if (shadow) shadow.enabled = false;
        var bulb = ImportSprite("Art/HintBulb.png");
        foreach (var path in new[] { "Play/Hint/Bulb", "Shop/Scroll/Viewport/Content/Free/Icon", "Shop/Scroll/Viewport/Content/HintOffer/Icon" })
            ui.stage.Find(path).GetComponent<UnityEngine.UI.Image>().sprite = bulb;
        var bulbRect = (RectTransform)ui.hintButton.transform.Find("Bulb");
        bulbRect.anchoredPosition = new Vector2(17, -10); bulbRect.sizeDelta = new Vector2(68, 80);
        foreach (var marker in ui.topRings.Concat(ui.bottomRings))
        {
            marker.color = Color.clear;
            var outline = marker.transform.Find("Outline");
            if (outline) continue;
            var rect = Rect(marker.transform, "Outline", 0, 0, 0, 0);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<DifferenceFoundRing>().raycastTarget = false;
        }
    }

    static void BuildHome(UIDifferences ui)
    {
        ui.home=Rect(ui.stage,"Home",0,0,720,1280).gameObject;var p=ui.home.transform;
        Image(p,"Beach",0,0,720,1280,Color.white,ImportSprite("Art/BeachV2.png"));
        var avatar=Button(p,"Avatar",24,12,80,80,"",22,UIDifferences.FrameColors[0]);ui.homeFrame=avatar.GetComponent<UnityEngine.UI.Image>();
        ui.homeAvatar=Image(avatar.transform,"Animal",3,3,74,74,Color.white,ui.avatarSprites[0]);
        var wallet=TransparentButton(p,"WalletButton",118,18,155,68);
        Image(wallet.transform,"Wallet",23,12,130,45,Cream,round);
        Icon(wallet.transform,"Coin",0,0,65,65,1);
        ui.coinsLabel=Label(wallet.transform,"Coins",72,10,70,48,"10",28,Ink);
        ui.settingsButton=IconButton(p,"Settings",632,15,65,65,2);
        var noAds=Button(p,"NoAds",580,124,110,94,"",24,new Color(1,.72f,.18f));
        Label(noAds.transform,"Ads",7,3,96,58,"ADS",37,new Color(.53f,.23f,.07f));
        var slash=Image(noAds.transform,"Slash",15,26,86,9,new Color(.74f,.23f,.10f));slash.rectTransform.localEulerAngles=new Vector3(0,0,-35);
        Image(noAds.transform,"Tag",-2,72,114,34,Cream,round);Label(noAds.transform,"TagLabel",0,72,110,34,"无广告",21,Ink);
        Image(p,"StartRim",167,861,386,132,new Color(1,.94f,.71f),round);
        ui.startButton=Button(p,"Start",176,870,368,112,"第1关",48,Green);
        ui.startLabel=ui.startButton.GetComponentInChildren<UnityEngine.UI.Text>();
    }

    static void BuildNavigation(UIDifferences ui)
    {
        ui.navigation=Rect(ui.stage,"Navigation",0,1108,720,172).gameObject;var p=ui.navigation.transform;
        Image(p,"Footer",0,94,720,78,new Color(.13f,.24f,.51f));
        var names=new[]{"Shop","HomeTab","Trophies"};var labels=new[]{"商店","主页","排行榜"};var ids=new[]{4,5,3};
        for(var i=0;i<3;i++)
        {
            var b=Button(p,names[i],i*240,i==1?-24:0,240,i==1?120:96,"",24,new Color(.21f,.43f,.87f));
            Icon(b.transform,"Icon",78,3,84,78,ids[i]);
            var label=b.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();label.text=labels[i];
            label.rectTransform.anchoredPosition=new Vector2(5,-83);label.rectTransform.sizeDelta=new Vector2(230,31);label.fontSize=23;
            label.gameObject.SetActive(i==1);
            if(i==0)ui.shopButton=b;if(i==2)ui.trophyButton=b;
        }
        Label(p,"Brand",0,119,720,42,"✦  FIND DIFFERENCES  ✦",22,new Color(.30f,.43f,.70f));
    }

    static void BuildShop(UIDifferences ui)
    {
        ui.shop=Rect(ui.stage,"Shop",0,0,720,1280).gameObject;var p=ui.shop.transform;
        Image(p,"Background",0,0,720,1280,Navy);Header(p,"商店");
        Image(p,"Wallet",18,25,136,44,Cream,round);Icon(p,"Coin",8,18,59,59,1);Label(p,"Coins",66,24,80,46,"10",26,Ink);
        IconButton(p,"Close",644,18,56,56,9);
        Label(p,"HintCount",250,100,220,42,"我的提示  ×5",23,new Color(.73f,.81f,.94f));
        var content=Scroll(p,"Scroll",50,153,620,930,1140);
        var row=Offer(content,"Free",0,6,"×1",new Color(.33f,.88f,.44f));Button(row,"Buy",388,25,190,72,"每日免费",28,new Color(1,.68f,.17f));
        row=Offer(content,"HintOffer",130,6,"×1",Cream);Button(row,"Buy",388,25,190,72,"900 金币",27,Green);
        var amounts=new[]{"1 000","5 000","10 000","25 000","50 000","100 000"};
        for(var i=0;i<amounts.Length;i++)
        {
            row=Offer(content,"Coins"+i,260+i*130,1,amounts[i],Cream);
            var b=Button(row,"Buy",388,25,190,72,"暂未开放",26,new Color(.57f,.66f,.72f));b.interactable=false;
        }
        Label(content,"Note",20,1044,580,65,"金币充值尚未开放",23,new Color(.76f,.83f,.96f));
    }
    static Transform Offer(Transform p,string name,float y,int icon,string amount,Color color)
    {
        var row=Image(p,name,12,y,594,116,color,round).transform;
        Icon(row,"Icon",20,16,86,84,icon);Label(row,"Amount",127,20,235,78,amount,34,Ink).fontStyle=FontStyle.Bold;return row;
    }
    static void BuildRanking(UIDifferences ui)
    {
        ui.ranking=Rect(ui.stage,"Ranking",0,0,720,1280).gameObject;var p=ui.ranking.transform;
        Image(p,"Background",0,0,720,1280,Navy);Header(p,"排行榜");
        // A locked board is visible in the reference at this progression; do not fabricate opponents.
        Image(p,"LockLoop",320,515,80,85,new Color(.65f,.79f,.91f),ring);
        Image(p,"Lock",315,553,90,74,new Color(1,.73f,.12f),round);Label(p,"Keyhole",315,552,90,70,"●",30,new Color(.4f,.23f,.05f));
        Label(p,"Locked",80,665,560,60,"在第 50 关后解锁。",29,Color.white);
        Label(p,"PlayerName",100,747,520,42,"Player_1001",23,new Color(.59f,.69f,.84f));
    }
    static void BuildPlay(UIDifferences ui)
    {
        ui.play=Rect(ui.stage,"Play",0,0,720,1280).gameObject;var p=ui.play.transform;
        Image(p,"Background",0,0,720,1280,Color.white,ImportSprite("Art/PlayBackdropV2.png"));
        ui.backButton=IconButton(p,"Back",19,20,58,56,8);
        ui.levelLabel=Label(p,"Level",180,16,360,65,"第1关",32,Color.white);Outline(ui.levelLabel.gameObject,new Color(.12f,.29f,.50f),new Vector2(1,-2));
        Image(p,"LifeBadge",614,22,86,45,new Color(.27f,.47f,.72f),round);Icon(p,"Heart",614,23,43,43,7);
        ui.heartsLabel=Label(p,"Lives",655,24,37,40,"3",28,Color.white);
        var dots=Rect(p,"Progress",18,88,684,48);var layout=dots.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=7;layout.childControlWidth=layout.childControlHeight=false;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        ui.progressDots=new UnityEngine.UI.Image[15];
        for(var i=0;i<15;i++){ui.progressDots[i]=Image(dots,"Dot"+i,0,0,37,37,new Color(.73f,.87f,1),circle);var t=Label(ui.progressDots[i].transform,"Text",0,0,37,37,"?",27,Color.white);Outline(t.gameObject,new Color(.26f,.43f,.55f),new Vector2(1,-1));}
        Image(p,"Frame",57,197,606,810,new Color(.18f,.34f,.53f));
        ui.upperImage=Board(p,"UpperViewport",60,200,ui);ui.lowerImage=Board(p,"LowerViewport",60,604,ui);
        var a=ui.upperImage.GetComponent<DifferenceBoard>();var b=ui.lowerImage.GetComponent<DifferenceBoard>();a.peer=b;b.peer=a;
        ui.spots=new DifferenceSpot[15];ui.differencePatches=new GameObject[15];ui.patchImages=new UnityEngine.UI.Image[15];ui.topRings=new UnityEngine.UI.Image[15];ui.bottomRings=new UnityEngine.UI.Image[15];
        for(var i=0;i<15;i++)
        {
            var patch=Rect(ui.lowerImage.transform,"Difference"+i,0,0,50,50);patch.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            ui.differencePatches[i]=patch.gameObject;ui.patchImages[i]=Image(patch,"Picture",0,0,600,400,Color.white,ui.levels[0].changed);
        }
        for(var i=0;i<15;i++)
        {
            ui.topRings[i]=Marker(ui.upperImage.transform,"Found"+i);ui.bottomRings[i]=Marker(ui.lowerImage.transform,"Found"+i);
        }
        ui.crossTop=Cross(ui.upperImage.transform);ui.crossBottom=Cross(ui.lowerImage.transform);
        var hint=Button(p,"Hint",309,1039,102,102,"",25,new Color(.16f,.60f,.94f));ui.hintButton=hint;
        Icon(hint.transform,"Bulb",4,0,95,97,6);Image(hint.transform,"Badge",76,72,40,40,new Color(.94f,.20f,.10f),circle);
        ui.hintLabel=Label(hint.transform,"Count",76,72,40,40,"5",27,Color.white);
        ui.progressLabel=Label(p,"Count",260,1162,200,39,"0 / 10",23,new Color(.87f,.95f,1));
        var zoom=Button(p,"Zoom",582,1056,67,65,"+",39,new Color(.3f,.51f,.80f));Label(zoom.transform,"Caption",-5,69,77,28,"缩放",19,Color.white);
    }
    static UnityEngine.UI.Image Board(Transform p,string name,float x,float y,UIDifferences ui)
    {
        var view=Rect(p,name,x,y,600,400);view.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var img=Image(view,"Image",0,0,600,400,Color.white,ui.levels[0].original);img.raycastTarget=true;img.gameObject.AddComponent<DifferenceBoard>().owner=ui;return img;
    }
    static UnityEngine.UI.Image Marker(Transform p,string name)
    {
        var image=Image(p,name,0,0,65,65,new Color(.61f,1,.20f),ring);image.rectTransform.pivot=new Vector2(.5f,.5f);image.gameObject.SetActive(false);return image;
    }
    static UnityEngine.UI.Text Cross(Transform p)
    {var t=Label(p,"Miss",0,0,60,60,"×",59,new Color(1,.17f,.10f));t.rectTransform.pivot=new Vector2(.5f,.5f);Outline(t.gameObject,Color.white,new Vector2(2,-2));t.gameObject.SetActive(false);return t;}

    static void BuildSettings(UIDifferences ui)
    {
        ui.settings=Rect(ui.stage,"Settings",0,0,720,1280).gameObject;var p=ui.settings.transform;
        Image(p,"Background",0,0,720,1280,Navy).raycastTarget=true;Header(p,"设置");IconButton(p,"Close",639,18,60,60,9);
        var c=Scroll(p,"Scroll",0,100,720,1150,1230);
        var banner=Button(c,"NoAds",18,5,684,130,"",28,new Color(1,.73f,.21f));Label(banner.transform,"Headline",36,8,580,50,"轻松寻找，享受每一关",30,Ink);Label(banner.transform,"Detail",36,65,580,39,"无广告体验",24,Ink);
        Toggle(c,"Sound",155,"音效");Toggle(c,"Music",265,"音乐");
        Label(c,"Section1",25,386,670,70,"成就",23,new Color(.50f,.58f,.72f)).alignment=TextAnchor.MiddleLeft;
        SettingRow(c,"Achievements",465,"成就");SettingRow(c,"Album",575,"关卡相册");
        Label(c,"Section2",25,686,670,70,"信息",23,new Color(.50f,.58f,.72f)).alignment=TextAnchor.MiddleLeft;
        SettingRow(c,"SelectMusic",765,"选择音乐");SettingRow(c,"SaveInfo",875,"游戏数据");
        Label(c,"Version",20,1030,680,80,"FIND DIFFERENCES\n版本 2.0",21,new Color(.5f,.58f,.71f));
    }
    static void SettingRow(Transform p,string name,float y,string text)
    {
        var bg=Image(p,name,0,y,720,106,new Color(.21f,.27f,.42f));bg.raycastTarget=true;
        var b=bg.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=bg;
        Label(b.transform,"Label",43,0,600,106,text,27,Color.white).alignment=TextAnchor.MiddleLeft;
        Image(b.transform,"Divider",22,104,698,1,new Color(.38f,.44f,.56f));
        Label(b.transform,"Arrow",650,18,48,67,"›",43,Color.white);
    }
    static void Toggle(Transform p,string name,float y,string text)
    {
        SettingRow(p,name,y,text);var b=p.Find(name);b.Find("Arrow").gameObject.SetActive(false);
        var sw=Image(b,"Switch",609,29,85,49,Green,round);Image(sw.transform,"Knob",37,3,43,43,new Color(.88f,.95f,1),circle);
    }
    static void BuildProfile(UIDifferences ui)
    {
        ui.profile=Overlay(ui,"Profile");var p=ui.profile.transform;var c=Panel(p,"编辑个人资料",99,149,522,920);
        var user=Image(c,"User",23,132,476,130,Cream,round).transform;
        ui.profileFrame=Image(user,"Frame",18,19,92,92,UIDifferences.FrameColors[0],round);ui.profileAvatar=Image(ui.profileFrame.transform,"Avatar",5,5,82,82,Color.white,ui.avatarSprites[0]);
        var entry=Image(user,"Nickname",122,37,330,59,new Color(.92f,.79f,.64f),round);entry.raycastTarget=true;
        ui.nicknameInput=entry.gameObject.AddComponent<UnityEngine.UI.InputField>();ui.nicknameInput.targetGraphic=entry;ui.nicknameInput.characterLimit=16;
        var text=Label(entry.transform,"Text",10,0,310,59,"Player_1001",28,new Color(.1f,.3f,.60f));text.alignment=TextAnchor.MiddleLeft;ui.nicknameInput.textComponent=text;ui.nicknameInput.text="Player_1001";
        Image(c,"ChoicesCard",23,292,476,420,Cream,round);
        Button(c,"AvatarTab",45,311,216,60,"头像",25,Blue);Button(c,"FrameTab",261,311,216,60,"头像框",25,Blue);
        var choices=Rect(c,"Choices",44,393,440,300);
        ui.avatars=new UnityEngine.UI.Image[8];
        for(var i=0;i<8;i++)
        {
            var b=Button(choices,"Choice"+i,i%4*111,i/4*143,98,99,"",20,UIDifferences.FrameColors[0]);
            ui.avatars[i]=Image(b.transform,"Avatar",5,3,88,88,Color.white,ui.avatarSprites[i]);
            var selected=Image(b.transform,"Selected",70,68,33,33,Green,circle);Label(selected.transform,"Check",0,0,33,33,"✓",25,Color.white);
            var price=Label(b.transform,"Price",-2,100,102,33,"",21,Ink);price.fontStyle=FontStyle.Bold;
        }
        Button(c,"Save",80,780,362,93,"保存",36,Green);
    }
    static void BuildMusic(UIDifferences ui)
    {
        ui.musicPanel=Overlay(ui,"MusicPanel");var c=Panel(ui.musicPanel.transform,"选择音乐",84,329,552,620);
        var names=new[]{"悠闲周末","清晨甘露","微小确幸","露台暖阳"};
        for(var i=0;i<4;i++)
        {var b=Button(c,"Track"+i,23,132+i*109,506,96,"",26,Cream);Label(b.transform,"Play",16,12,66,70,"▶",33,new Color(.94f,.54f,.08f));Label(b.transform,"Name",93,12,311,70,names[i],28,Ink).alignment=TextAnchor.MiddleLeft;Image(b.transform,"Check",439,24,49,49,Green,round);Label(b.transform,"Selected",439,24,49,49,"",32,Color.white);}
    }
    static void BuildAchievements(UIDifferences ui)
    {
        ui.achievements=Overlay(ui,"Achievements");var c=Panel(ui.achievements.transform,"成就",60,220,600,850);
        var names=new[]{"初露锋芒","观察达人","完美发现","旅行收藏家"};var details=new[]{"完成第一个关卡","累计找到 10 处不同","无失误完成一个新关卡","完成全部已开放场景"};
        for(var i=0;i<4;i++)
        {var row=Image(c,"Row"+i,23,142+i*162,554,146,Cream,round);Icon(row.transform,"Medal",13,22,92,97,11);Label(row.transform,"Name",119,13,285,42,names[i],28,Ink).alignment=TextAnchor.MiddleLeft;Label(row.transform,"Detail",119,56,412,35,details[i],22,Ink).alignment=TextAnchor.MiddleLeft;Label(row.transform,"Progress",115,100,170,33,"0 / 1",22,Ink);Label(row.transform,"State",365,100,161,33,"进行中",22,new Color(.27f,.5f,.18f));}
    }
    static void BuildAlbum(UIDifferences ui)
    {
        ui.album=Overlay(ui,"Album");var c=Panel(ui.album.transform,"关卡相册",54,195,612,885);
        for(var i=0;i<ui.levels.Length;i++)
        {var b=Button(c,"Level"+i,24,144+i*213,564,194,"",27,Cream);Image(b.transform,"Preview",12,16,243,162,Color.white,ui.levels[i].original);Label(b.transform,"Title",267,25,281,60,(i+1)+"  "+ui.levels[i].title,27,Ink);Label(b.transform,"Count",267,84,281,45,ui.levels[i].regions.Length+" 处不同",23,Ink);Label(b.transform,"Status",267,133,281,40,"尚未解锁",23,new Color(.18f,.46f,.14f));}
        Label(c,"More",40,794,532,49,"更多场景，持续更新",25,Color.white);
    }
    static void BuildDialog(UIDifferences ui)
    {
        ui.modal=Overlay(ui,"Modal");var c=Panel(ui.modal.transform,"",69,274,582,760);
        ui.modalTitle=c.Find("Title").GetComponent<UnityEngine.UI.Text>();
        ui.modalClose=ui.modal.transform.Find("Close").GetComponent<UnityEngine.UI.Button>();
        ui.resultIcon=Icon(c,"Medal",226,103,130,121,11);
        ui.modalBody=Label(c,"Body",34,228,514,148,"",29,Color.white);
        ui.modalAction=Button(c,"Action",57,404,468,91,"继续",34,Green);ui.modalActionLabel=ui.modalAction.GetComponentInChildren<UnityEngine.UI.Text>();
        ui.modalSecondary=Button(c,"Secondary",87,520,408,78,"返回首页",29,new Color(.17f,.35f,.70f));
        ui.modalTertiary=Button(c,"Tertiary",122,622,338,65,"返回首页",25,new Color(.23f,.40f,.73f));

        //for(var i=0;i<ui.confetti.Length;i++){var piece=Image(ui.modal.transform,"Confetti"+i,0,0,8+i%3*4,14+i%2*5,Color.HSVToRGB(i/30f,.8f,1));ui.confetti[i]=piece.rectTransform;piece.gameObject.SetActive(false);}
    }
    static GameObject Overlay(UIDifferences ui,string name)
    {var root=Rect(ui.stage,name,0,0,720,1280);Image(root,"Scrim",0,0,720,1280,new Color(0,.02f,.07f,.76f)).raycastTarget=true;return root.gameObject;}
    static Transform Panel(Transform p,string title,float x,float y,float w,float h)
    {var c=Image(p,"Card",x,y,w,h,Blue,round).transform;Label(c,"Title",30,31,w-60,66,title,37,Color.white);IconButton(p,"Close",x+w-42,y-16,65,65,9);return c;}
    static void Header(Transform p,string title)
    {Image(p,"Header",0,0,720,94,new Color(.20f,.33f,.66f));Image(p,"HeaderLine",0,85,720,5,new Color(.68f,.80f,1));var t=Label(p,"Title",175,10,370,69,title,39,Color.white);Outline(t.gameObject,new Color(.08f,.11f,.21f),new Vector2(1,-2));}
    static RectTransform Scroll(Transform p,string name,float x,float y,float w,float h,float contentHeight)
    {
        var root=Rect(p,name,x,y,w,h);var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var view=Image(root,"Viewport",0,0,w,h,Color.white);view.raycastTarget=true;view.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var content=Rect(view.transform,"Content",0,0,w,contentHeight);
        scroll.viewport=view.rectTransform;scroll.content=content;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=36;
        return content;
    }

    [MenuItem("Tools/Find Differences/Verify Game Assets")]
    public static void Verify()
    {
        DifferenceRound.SelfCheck();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"UIDifferences.prefab");if(!prefab)throw new InvalidOperationException("找不到游戏 Prefab");
        foreach(var t in prefab.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new InvalidOperationException("缺少脚本："+t.name);
        foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            var names=new System.Collections.Generic.HashSet<string>();
            foreach(Transform child in t)if(!names.Add(child.name))throw new InvalidOperationException("同级 UI 节点重名："+t.name+"/"+child.name);
        }
        var ui=prefab.GetComponent<UIDifferences>();
        var designPaths = Directory.GetFiles(Folder + "Art/Figma", "*.png").Select(path => path.Replace('\\', '/')).ToArray();
        var dependencies = AssetDatabase.GetDependencies(Folder + "UIDifferences.prefab");
        if (ui.designSprites == null || ui.designSprites.Length != designPaths.Length || designPaths.Length == 0 ||
            ui.designSprites.Any(sprite => !sprite) ||
            designPaths.Any(path => !dependencies.Contains(path) || !ui.designSprites.Contains(AssetDatabase.LoadAssetAtPath<Sprite>(path))))
            throw new InvalidOperationException("Figma UI 图片没有完整绑定到 Bundle 预制体");
        if (Directory.Exists("Assets/Resources/FindDifferences"))
            throw new InvalidOperationException("Figma UI 图片仍残留在 Resources");
        foreach(var field in typeof(UIDifferences).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly))
            if(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)&&!(field.GetValue(ui)as UnityEngine.Object))throw new InvalidOperationException("丢失引用："+field.Name);
        foreach(var level in ui.levels)
        {
            if(!level.original||!level.changed||level.regions.Length<1||level.regions.Length>15)throw new InvalidOperationException("关卡数据不完整");
            foreach(var r in level.regions)if(r.x-r.z/2 < -.001f||r.y-r.w/2 < -.001f||r.x+r.z/2>1.001f||r.y+r.w/2>1.001f)throw new InvalidOperationException("区域越界："+level.title);
        }
        foreach(var board in prefab.GetComponentsInChildren<DifferenceBoard>(true))if(board.owner!=ui||!board.peer||!board.GetComponent<UnityEngine.UI.Image>().raycastTarget||!board.transform.parent.GetComponent<UnityEngine.UI.RectMask2D>())throw new InvalidOperationException("图片点击与缩放绑定不完整");
        File.WriteAllText("Temp/FindDifferences-build-check.txt","PASS: 3 unique levels, UI references, 2 linked boards, 35 regions, sprite assets, no missing scripts.");
        Debug.Log("Find Differences V2 prefab validation passed.");
    }
    static RectTransform Rect(Transform p,string name,float x,float y,float w,float h)
    {var go=new GameObject(name,typeof(RectTransform));go.layer=LayerMask.NameToLayer("UI");var r=(RectTransform)go.transform;r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static UnityEngine.UI.Image Image(Transform p,string name,float x,float y,float w,float h,Color color,Sprite sprite=null)
    {var image=Rect(p,name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.sprite=sprite;image.raycastTarget=false;if(sprite==round)image.type=UnityEngine.UI.Image.Type.Sliced;return image;}
    static UnityEngine.UI.Image Icon(Transform p,string name,float x,float y,float w,float h,int index)
    {var image=Image(p,name,x,y,w,h,Color.white,icons[index]);image.preserveAspect=true;return image;}
    static UnityEngine.UI.Text Label(Transform p,string name,float x,float y,float w,float h,string value,int size,Color color)
    {var t=Rect(p,name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.Text>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
    static UnityEngine.UI.Button Button(Transform p,string name,float x,float y,float w,float h,string text,int size,Color color)
    {
        var bg=Image(p,name,x,y,w,h,color,round);bg.raycastTarget=true;var b=bg.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=bg;
        var colors=b.colors;colors.highlightedColor=Color.white;colors.pressedColor=new Color(.72f,.82f,.96f);colors.disabledColor=new Color(.64f,.66f,.70f);b.colors=colors;
        var label=Label(bg.transform,"Label",5,0,w-10,h,text,size,Color.white);Outline(label.gameObject,new Color(.10f,.18f,.12f,.9f),new Vector2(1,-2));
        var shadow=bg.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(.04f,.1f,.2f,.7f);shadow.effectDistance=new Vector2(0,-4);return b;
    }
    static UnityEngine.UI.Button TransparentButton(Transform p,string name,float x,float y,float w,float h)
    {var img=Image(p,name,x,y,w,h,new Color(1,1,1,0));img.raycastTarget=true;return img.gameObject.AddComponent<UnityEngine.UI.Button>();}
    static UnityEngine.UI.Button IconButton(Transform p,string name,float x,float y,float w,float h,int index)
    {var img=Icon(p,name,x,y,w,h,index);img.raycastTarget=true;var b=img.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=img;return b;}
    static void Outline(GameObject go,Color color,Vector2 distance){var o=go.AddComponent<UnityEngine.UI.Outline>();o.effectColor=color;o.effectDistance=distance;}
    static Sprite ImportSprite(string relative)
    {
        var path=Folder+relative;if(!File.Exists(path))throw new FileNotFoundException("等待图片："+path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=2048;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite[] SpriteGrid(string name,int columns,int rows)
    {
        var source=ImportSprite("Art/"+name+".png").texture;
        if(!AssetDatabase.IsValidFolder(Folder+"Sprites"))AssetDatabase.CreateFolder(Folder.TrimEnd('/'),"Sprites");
        var result=new Sprite[columns*rows];
        for(var i=0;i<result.Length;i++)
        {
            var left=Mathf.RoundToInt(i%columns*source.width/(float)columns);var right=Mathf.RoundToInt((i%columns+1)*source.width/(float)columns);
            var top=Mathf.RoundToInt(i/columns*source.height/(float)rows);var bottom=Mathf.RoundToInt((i/columns+1)*source.height/(float)rows);
            var sprite=Sprite.Create(source,new UnityEngine.Rect(left,source.height-bottom,right-left,bottom-top),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name=name+"_"+i;
            var path=Folder+"Sprites/"+sprite.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(existing){EditorUtility.CopySerialized(sprite,existing);UnityEngine.Object.DestroyImmediate(sprite);result[i]=existing;}else{AssetDatabase.CreateAsset(sprite,path);result[i]=sprite;}
        }
        return result;
    }
    static Sprite Shape(string name,int kind)
    {
        var t=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float dx=x-31.5f,dy=y-31.5f;float d=kind==0?new Vector2(Mathf.Max(0,Mathf.Abs(dx)-15),Mathf.Max(0,Mathf.Abs(dy)-15)).magnitude-16:new Vector2(dx,dy).magnitude-30;
            var alpha=Mathf.Clamp01(.5f-d);if(kind==2)alpha*=Mathf.Clamp01(new Vector2(dx,dy).magnitude-25);
            var shade=kind==2?1:Mathf.Lerp(.73f,1,y/63f);
            if(kind!=2){if(d>-2)shade=.30f;else if(d>-4)shade=1;else if(d>-6)shade=.75f;}
            t.SetPixel(x,y,new Color(shade,shade,shade,alpha));
        }
        t.Apply();File.WriteAllBytes(Folder+"Art/"+name+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);var sprite=ImportSprite("Art/"+name+".png");
        if(kind==0){var imp=(TextureImporter)AssetImporter.GetAtPath(Folder+"Art/"+name+".png");imp.spriteBorder=new Vector4(24,24,24,24);imp.SaveAndReimport();sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"Art/"+name+".png");}return sprite;
    }
    static AudioClip Tone(string name,float frequency)
    {return WriteWave(name,.23f,(i,rate)=>Math.Sin(i*2*Math.PI*frequency/rate)*Math.Sin(Math.PI*i/(rate*.23))* .35);}
    static AudioClip Music(int track)
    {
        var notes=new[]{0,4,7,12,7,4,2,7,11,14,11,7,5,9,12,17,12,9,4,7,12,16,12,7};
        return WriteWave("Music"+track,12,(i,rate)=>{
            var time=i/(double)rate;var beat=time/.5;var step=(int)beat;var local=(beat-step)*.5;
            var midi=60+track*2+notes[(step+track*3)%notes.Length];var frequency=440*Math.Pow(2,(midi-69)/12.0);
            var envelope=(1-Math.Exp(-local*55))*Math.Exp(-local*7);
            var note=(Math.Sin(2*Math.PI*frequency*local)+.24*Math.Sin(4*Math.PI*frequency*local))*envelope*.16;
            var bass=110*Math.Pow(2,track*2/12.0);return note+Math.Sin(time*2*Math.PI*bass)*.035*Math.Pow(Math.Sin(Math.PI*time/12),2);
        });
    }
    static AudioClip WriteWave(string name,float seconds,Func<int,int,double> sample)
    {
        var path=Folder+"Art/"+name+".wav";const int rate=22050;var count=(int)(rate*seconds);
        using(var w=new BinaryWriter(File.Create(path))){w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);for(var i=0;i<count;i++)w.Write((short)(Math.Max(-1,Math.Min(1,sample(i,rate)))*32767));}
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
#endif
