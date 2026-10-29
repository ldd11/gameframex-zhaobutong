#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DifferenceJourneyMusicCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Find Differences/Check Journey Music Return Only")]
    public static void Run()
    {
        var live = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!Application.isPlaying || !live)
            throw new InvalidOperationException("Open the game through Launcher in Play mode before checking music.");
        if (live.AdInputBlocked)
            throw new InvalidOperationException("Wait for the active ad operation before checking music.");
        var reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/FindDifferences-journey-music-return.txt"));
        var report = new StringBuilder().AppendLine("\nSnapshot " + DateTime.Now.ToString("O"));
        object Read(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
        string Clip(AudioClip clip) => clip ? clip.name + " [id=" + clip.GetInstanceID() + ", path=" +
            (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clip)) ? "<runtime/bundle>" : AssetDatabase.GetAssetPath(clip)) + "]" : "<null>";
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        var liveSource = live.musicSource;
        var originalClip = liveSource ? liveSource.clip : null;
        var originalEnabled = (bool)Read(live, "musicEnabled");
        var originalPage = Read(live, "currentPage");
        var originalJourneyVisible = live.JourneyVisible;
        var originalJourneyPlaying = live.JourneyPlaying;
        var originalPause = AudioListener.pause;
        string SavedState()
        {
            var state = new StringBuilder();
            foreach (var name in new[] { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "JourneyCompleted", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "Journey.RoundLevel", "Journey.RoundMask", "Journey.RoundLives", "Journey.RoundMistakes", "Journey.RoundHint" })
                state.Append(name).Append('=').Append(GameApp.Setting.HasSetting(UIDifferences.Key + name)).Append(':').Append(GameApp.Setting.GetInt(UIDifferences.Key + name, 0)).Append(';');
            foreach (var name in new[] { "Sound", "Music", "Vibration", "TutorialComplete" })
                state.Append(name).Append('=').Append(GameApp.Setting.HasSetting(UIDifferences.Key + name)).Append(':').Append(GameApp.Setting.GetBool(UIDifferences.Key + name, false)).Append(';');
            foreach (var name in new[] { "Nickname", "GiftDate", "RoundContent", "Journey.RoundContent" })
                state.Append(name).Append('=').Append(GameApp.Setting.HasSetting(UIDifferences.Key + name)).Append(':').Append(GameApp.Setting.GetString(UIDifferences.Key + name, "")).Append(';');
            return state.ToString();
        }
        var originalSavedState = SavedState();
        report.AppendLine("live.currentPage=" + ((originalPage as GameObject) ? ((GameObject)originalPage).name : "<null>") +
            ", JourneyVisible=" + originalJourneyVisible + ", JourneyPlaying=" + originalJourneyPlaying + ", musicEnabled=" + originalEnabled);
        report.AppendLine("live.currentClip=" + Clip(originalClip));
        report.AppendLine("live.homeMusic=" + Clip(live.homeMusic));
        report.AppendLine("live.gameMusic=" + Clip(live.gameMusic));
        report.AppendLine("live.journeyMusic=" + Clip(live.journeyMusic));
        report.AppendLine("live.source=" + (liveSource ? liveSource.name + ", isPlaying=" + liveSource.isPlaying + ", time=" +
            liveSource.time + ", mute=" + liveSource.mute + ", volume=" + liveSource.volume : "<null>") +
            ", AudioListener.pause=" + originalPause);
        GameObject fixtureRoot = null, sourceObject = null;
        try
        {
            Check(liveSource && live.homeMusic && live.gameMusic, "The live UI requires its music source and both configured clips.");
            fixtureRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab"));
            fixtureRoot.name = "JourneyMusicReturnCheck";
            var fixture = fixtureRoot.GetComponent<UIDifferences>();
            fixture.enabled = false;
            report.AppendLine("prefab.homeMusic=" + Clip(fixture.homeMusic));
            report.AppendLine("prefab.gameMusic=" + Clip(fixture.gameMusic));
            report.AppendLine("prefab.journeyMusic=" + Clip(fixture.journeyMusic));
            Check(fixture.journeyMusic && AssetDatabase.GetAssetPath(fixture.journeyMusic) ==
                "Assets/Bundles/UI/UIDifferences/Audio/JoureyBgMusic.wav",
                "Journey music must reference the requested JoureyBgMusic asset.");
            sourceObject = new GameObject("JourneyMusicRoutingCheck", typeof(AudioSource)) { hideFlags = HideFlags.HideAndDontSave };
            var source = sourceObject.GetComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.mute = true; source.ignoreListenerPause = true;
            fixture.musicSource = source; fixture.homeMusic = live.homeMusic; fixture.gameMusic = live.gameMusic;
            void Set(string name, object value) => typeof(UIDifferences).GetField(name, Private).SetValue(fixture, value);
            foreach (var name in new[] { "journeyCompleted", "coins", "hints", "avatar", "frame", "music", "ownedAvatars", "ownedFrames", "totalFound", "perfect", "vibration", "nickname", "giftDate" })
                Set(name, Read(live, name));
            Set("testing", true); Set("completed", UIDifferences.JourneyUnlockLevel); Set("musicEnabled", true); Set("sound", false);
            // OnOpen would restore a round and open ads/tutorials. OnInit only binds this isolated instance.
            fixture.OnInit();
            Check(fixture.Round == null, "The isolated UI must not create a round or write round saves.");
            var journey = (GameObject)Read(fixture, "journeyDesign");
            var homeDesign = (GameObject)Read(fixture, "homeDesign");
            var close = (Button)Read(fixture, "journeyCloseButton");
            var homeTab = fixture.home.transform.parent.Find("Navigation/HomeTab").GetComponent<Button>();
            void OpenJourney()
            {
                fixture.ShowJourney();
                Check(fixture.JourneyVisible && journey.activeInHierarchy && !homeDesign.activeSelf &&
                    Read(fixture, "currentPage") == fixture.home && source.clip == fixture.journeyMusic && source.isPlaying && source.loop,
                    "Actual ShowJourney must display the map with looping Journey music.");
                source.Stop(); source.clip = fixture.gameMusic; source.Play();
            }
            void ReturnedHome(string label)
            {
                Check(!fixture.JourneyVisible && !fixture.JourneyPlaying && !journey.activeSelf &&
                    fixture.home.activeInHierarchy && homeDesign.activeInHierarchy && Read(fixture, "currentPage") == fixture.home &&
                    source.clip == fixture.homeMusic && fixture.Round == null,
                    label + " must return from Journey to the visible Home page, replacing the stale game clip.");
                Check(SavedState() == originalSavedState, label + " must not modify player settings or either saved round.");
                report.AppendLine("PASS actual " + label + " -> visible Home, Journey hidden, " + Clip(source.clip) + "; fixture.isPlaying=" + source.isPlaying);
            }
            OpenJourney();
            close.onClick.Invoke();
            ReturnedHome("Journey Close button");
            OpenJourney();
            homeTab.onClick.Invoke();
            ReturnedHome("Navigation/HomeTab button");
            report.AppendLine("PASS ShowJourney plays the requested Journey music and both return buttons restore Home music.");
            var playMusic = typeof(UIDifferences).GetMethod("PlayMusicForPage", Private);
            Check(playMusic != null, "Music routing method is missing.");
            void Route(string label, GameObject page, bool visible, bool playing, AudioClip expected)
            {
                Set("currentPage", page); Set("journeyVisible", visible); Set("journeyPlaying", playing); Set("musicEnabled", true);
                source.clip = expected == fixture.homeMusic ? fixture.gameMusic : fixture.homeMusic;
                playMusic.Invoke(fixture, new object[] { page });
                Check(source.clip == expected, label + " selected " + Clip(source.clip) + " instead of " + Clip(expected));
                report.AppendLine("PASS " + label + " -> " + Clip(source.clip) + "; fixture.isPlaying=" + source.isPlaying);
            }
            Route("ordinary Home", fixture.home, false, false, fixture.homeMusic);
            Route("Journey map", fixture.home, true, false, fixture.journeyMusic);
            Route("Home after leaving Journey map with a stale game clip", fixture.home, false, false, fixture.homeMusic);
            Route("Journey gameplay", fixture.play, false, true, fixture.gameMusic);
            Route("Home after Journey gameplay", fixture.home, false, false, fixture.homeMusic);
            Route("ordinary gameplay", fixture.play, false, false, fixture.gameMusic);
            Route("Journey map before disabling music", fixture.home, true, false, fixture.journeyMusic);
            Set("musicEnabled", false);
            playMusic.Invoke(fixture, new object[] { fixture.home });
            Check(!source.isPlaying, "Turning music off must stop the isolated music source.");
            report.AppendLine("PASS music disabled stops the isolated source.");
            Route("reenabled Journey music", fixture.home, true, false, fixture.journeyMusic);
            Route("Home after reenabling music", fixture.home, false, false, fixture.homeMusic);
            Check(live.musicSource == liveSource && liveSource.clip == originalClip &&
                (bool)Read(live, "musicEnabled") == originalEnabled && Read(live, "currentPage") == originalPage &&
                live.JourneyVisible == originalJourneyVisible && live.JourneyPlaying == originalJourneyPlaying && AudioListener.pause == originalPause &&
                SavedState() == originalSavedState,
                "The check must not change the live UI's music references, page state or listener pause.");
            report.AppendLine("PASS live UI and settings were not modified; its playback clock continued normally.");
            report.AppendLine("LIMITATION: real OnInit/ShowJourney/Close/HomeTab handlers run on an isolated prefab with Round=null and a muted source, " +
                "without OnOpen, ads or network. Button UnityEvents are invoked directly, so pointer hit testing and audible playback are not asserted.");
            Debug.Log("Journey music routing checks passed. Live state was recorded without modification: " + reportPath);
        }
        catch (Exception error)
        {
            report.AppendLine("FAIL " + error);
            throw;
        }
        finally
        {
            if (sourceObject) UnityEngine.Object.DestroyImmediate(sourceObject);
            if (fixtureRoot) UnityEngine.Object.DestroyImmediate(fixtureRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.AppendAllText(reportPath, report.ToString());
        }
    }
}
#endif
