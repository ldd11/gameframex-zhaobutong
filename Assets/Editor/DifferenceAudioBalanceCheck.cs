#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Hotfix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DifferenceAudioBalanceCheck
{
    const string Prefab = "Assets/Bundles/UI/UIDifferences/UIDifferences.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Find Differences/Inspect Live Audio Only")]
    public static void InspectLiveAudioOnly()
    {
        var report = new StringBuilder("Audio snapshot " + DateTime.Now.ToString("O") + "\n");
        var editorMute = typeof(EditorUtility).GetProperty("audioMasterMute", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        report.AppendLine("Editor audioMasterMute=" + (editorMute == null ? "unavailable" : editorMute.GetValue(null)));
        report.AppendLine("Playing=" + Application.isPlaying + ", Listener.pause=" + AudioListener.pause + ", Listener.volume=" + AudioListener.volume);
        var configuration = AudioSettings.GetConfiguration();
        var output = new float[1024];
        AudioListener.GetOutputData(output, 0);
        var peak = 0f;
        foreach (var sample in output) peak = Mathf.Max(peak, Mathf.Abs(sample));
        report.AppendLine("Audio configuration: sampleRate=" + configuration.sampleRate + ", speakerMode=" + configuration.speakerMode +
            ", dspTime=" + AudioSettings.dspTime + ", outputPeak=" + peak);
        foreach (var listener in UnityEngine.Object.FindObjectsOfType<AudioListener>())
            report.AppendLine("Listener " + listener.name + ": enabled=" + listener.enabled + ", active=" + listener.gameObject.activeInHierarchy);
        foreach (var ui in UnityEngine.Object.FindObjectsOfType<UIDifferences>())
        {
            report.AppendLine("UI " + ui.name + ": sound=" + typeof(UIDifferences).GetField("sound", Private).GetValue(ui) +
                ", music=" + typeof(UIDifferences).GetField("musicEnabled", Private).GetValue(ui));
            foreach (var source in new[] { ui.audioSource, ui.musicSource })
                report.AppendLine(source ? "Source " + source.GetInstanceID() + ": enabled=" + source.enabled +
                    ", active=" + source.gameObject.activeInHierarchy + ", mute=" + source.mute + ", volume=" + source.volume +
                    ", spatialBlend=" + source.spatialBlend + ", playing=" + source.isPlaying + ", time=" + source.time +
                    ", clip=" + (source.clip ? source.clip.name : "<none>") : "Source: <missing>");
            report.AppendLine("Bound SFX: button=" + (ui.buttonSound ? ui.buttonSound.name : "<missing>") +
                ", found=" + (ui.foundSound ? ui.foundSound.name : "<missing>"));
        }
        var path = Path.GetFullPath("Temp/FindDifferences-live-audio.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Find Differences/Reset Editor Audio Output Only")]
    public static void ResetEditorAudioOutputOnly()
    {
        var playing = new List<(AudioSource source, float time)>();
        foreach (var source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            if (source.isPlaying && source.clip) playing.Add((source, source.time));
        if (!AudioSettings.Reset(AudioSettings.GetConfiguration()))
            throw new InvalidOperationException("Unity could not reinitialize the audio output device.");
        foreach (var item in playing)
        {
            if (!item.source || !item.source.clip || !item.source.isActiveAndEnabled) continue;
            item.source.Play();
            item.source.time = Mathf.Clamp(item.time, 0, Mathf.Max(0, item.source.clip.length - .01f));
        }
        Debug.Log("Unity audio output reinitialized; resumed " + playing.Count + " playing sources.");
    }

    [MenuItem("Tools/Find Differences/Apply Audio Balance Defaults Only")]
    public static void ApplyDefaults()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var editing = stage != null && stage.assetPath == Prefab;
        var root = editing ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var source = root.GetComponent<UIDifferences>().musicSource;
            Check(source, "The music source must remain bound.");
            if (editing) Undo.RecordObject(source, "Apply audio balance");
            source.volume = UIDifferences.BackgroundMusicVolume;
            EditorUtility.SetDirty(source);
            Check(PrefabUtility.SaveAsPrefabAsset(root, Prefab), "Could not save audio balance defaults.");
        }
        finally { if (!editing) PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("PASS: saved only the targeted prefab's background music volume default.");
    }

    [MenuItem("Tools/Find Differences/Check Audio Balance Only")]
    public static void CheckAudioBalanceOnly()
    {
        var report = new StringBuilder("Audio balance: imported PCM measurements; no player settings changed.\n");
        var path = Path.GetFullPath("Temp/FindDifferences-audio-balance.txt");
        try
        {
            var ui = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<UIDifferences>();
            Check(ui.audioSource && ui.musicSource && ui.audioSource != ui.musicSource && ui.musicSource.loop,
                "Music and effects must retain separate sources, with looping music.");
            Check(Mathf.Abs(ui.audioSource.volume - 1f) < .001f,
                "Effects must retain the original source volume without attenuation.");
            Check(Mathf.Abs(ui.musicSource.volume - UIDifferences.BackgroundMusicVolume) < .001f &&
                UIDifferences.BackgroundMusicVolume >= .25f && UIDifferences.BackgroundMusicVolume <= .4f,
                "Apply Audio Balance Defaults Only before checking the saved music level.");
            var clips = new[] { ui.buttonSound, ui.failSound, ui.foundSound, ui.missSound, ui.winSound,
                ui.coinCreateSound, ui.progressCompleteSound, ui.tipsSound, ui.homeMusic, ui.gameMusic };
            var names = new[] { "Click_Btn.wav", "False.wav", "Found.wav", "Miss.wav", "Win.wav",
                "CoinCreate.WAV", "Successtips.wav", "Tips.WAV", "MusicHome.wav", "MusicGame.wav" };
            var rms = new Dictionary<AudioClip, double>();
            for (var i = 0; i < clips.Length; i++)
            {
                Check(clips[i] && AssetDatabase.GetAssetPath(clips[i]) == "Assets/Bundles/UI/UIDifferences/Audio/" + names[i],
                    "Authored audio reference changed: " + names[i]);
                rms.Add(clips[i], Measure(clips[i], report));
            }
            foreach (var music in new[] { ui.homeMusic, ui.gameMusic })
            {
                var musicDb = rms[music] + 20 * Math.Log10(ui.musicSource.volume);
                report.AppendLine($"{music.name}: playback RMS {musicDb:F2} dBFS");
            }
            report.AppendLine($"Music source RMS difference: {Math.Abs(rms[ui.homeMusic] - rms[ui.gameMusic]):F2} dB.");
            if (Application.isPlaying) CheckPlayback(ui);
            report.AppendLine(Application.isPlaying ? "PASS: muted fixture home/game/return/off/on playback and volume." : "Play fixture skipped in Edit mode.");
            report.AppendLine("PASS: references, original effect source volume, valid PCM and existing music volume.");
            Debug.Log(report.ToString());
        }
        catch (Exception error) { report.AppendLine("FAIL: " + error); throw; }
        finally { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, report.ToString()); }
    }

    static double Measure(AudioClip clip, StringBuilder report)
    {
        var unload = clip.loadState == AudioDataLoadState.Unloaded;
        try
        {
            Check(clip.loadState == AudioDataLoadState.Loaded || clip.LoadAudioData(), "Cannot load PCM: " + clip.name);
            Check(clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0 && clip.channels > 0, "PCM is not ready: " + clip.name);
            var data = new float[Math.Min(4096, clip.samples) * clip.channels];
            double sum = 0, peak = 0;
            for (var frame = 0; frame < clip.samples; frame += data.Length / clip.channels)
            {
                Check(clip.GetData(data, frame), "Cannot read imported PCM: " + clip.name);
                var count = Math.Min(data.Length, (clip.samples - frame) * clip.channels);
                for (var i = 0; i < count; i++) { sum += (double)data[i] * data[i]; peak = Math.Max(peak, Math.Abs(data[i])); }
            }
            var db = 20 * Math.Log10(Math.Max(1e-9, Math.Sqrt(sum / ((double)clip.samples * clip.channels))));
            Check(peak > .0001 && peak <= 1.001, "Audio must contain valid, non-silent PCM: " + clip.name);
            report.AppendLine($"{clip.name}: {clip.samples / (double)clip.frequency:F3}s, RMS {db:F2} dBFS, peak {peak:F4}");
            return db;
        }
        finally { if (unload) clip.UnloadAudioData(); }
    }

    static void CheckPlayback(UIDifferences authored)
    {
        var root = new GameObject("AudioBalanceFixture") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var ui = root.AddComponent<UIDifferences>(); ui.enabled = false;
            ui.home = new GameObject("Home"); ui.home.transform.SetParent(root.transform);
            ui.play = new GameObject("Play"); ui.play.transform.SetParent(root.transform);
            ui.homeMusic = authored.homeMusic; ui.gameMusic = authored.gameMusic;
            ui.musicSource = root.AddComponent<AudioSource>(); ui.musicSource.mute = true;
            ui.musicSource.playOnAwake = false; ui.musicSource.loop = true; ui.musicSource.ignoreListenerPause = true;
            var toggle = typeof(UIDifferences).GetField("musicEnabled", Private);
            var play = typeof(UIDifferences).GetMethod("PlayMusicForPage", Private);
            root.SetActive(true); toggle.SetValue(ui, true);
            foreach (var page in new[] { ui.home, ui.play, ui.home })
            {
                ui.musicSource.volume = .01f; play.Invoke(ui, new object[] { page });
                Check(ui.musicSource.clip == (page == ui.play ? ui.gameMusic : ui.homeMusic) && ui.musicSource.isPlaying &&
                    Mathf.Abs(ui.musicSource.volume - UIDifferences.BackgroundMusicVolume) < .001f, "Page music or its volume failed to restore.");
            }
            toggle.SetValue(ui, false); play.Invoke(ui, new object[] { ui.play });
            Check(!ui.musicSource.isPlaying, "Music off must stop playback.");
            toggle.SetValue(ui, true); play.Invoke(ui, new object[] { ui.home });
            Check(ui.musicSource.isPlaying && ui.musicSource.clip == ui.homeMusic, "Music on must resume the appropriate track.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
#endif
