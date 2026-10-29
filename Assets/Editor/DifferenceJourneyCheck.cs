#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.Reflection;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceJourneyCheck
{
    [MenuItem("Tools/Find Differences/Run Journey Save Checks")]
    public static void Run()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("Start the game through Launcher first.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(UIDifferences).GetField(name, flags).GetValue(ui);
        void Set(string name, object value) => typeof(UIDifferences).GetField(name, flags).SetValue(ui, value);
        object Call(string name) => typeof(UIDifferences).GetMethod(name, flags).Invoke(ui, null);
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        var fields = new Dictionary<string, object>();
        foreach (var name in new[] { "journeyPlaying", "journeyVisible", "completed", "journeyCompleted", "level", "testing", "settled", "roundMistakes", "pendingHint", "roundContent" })
            fields[name] = Get(name);
        var roundProperty = typeof(UIDifferences).GetProperty("Round");
        var previousRound = ui.Round;
        var design = (GameObject)Get("journeyDesign");
        var designVisible = design && design.activeSelf;
        var settings = GameApp.Setting;
        var stored = new Dictionary<string, object>();
        var keys = new List<string>();
        foreach (var prefix in new[] { UIDifferences.Key, UIDifferences.Key + "Journey." })
            foreach (var name in new[] { "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "RoundContent", "AnalyticsRound" })
            {
                var key = prefix + name;
                keys.Add(key);
                if (settings.HasSetting(key)) stored[key] = name == "RoundContent" || name == "AnalyticsRound"
                    ? (object)settings.GetString(key) : settings.GetInt(key);
            }
        try
        {
            DifferenceJourneyDesignBuilder.CheckRouteGeometry();
            var buttons = (UnityEngine.UI.Button[])Get("journeyLevelButtons");
            Check(buttons.Length == UIDifferences.JourneyLevelCount, "Journey must bind all 200 nodes in order.");
            for (var i = 0; i < buttons.Length; i++)
            {
                Check(buttons[i].name == "Level" + (i + 1) && buttons[i].GetComponentInChildren<UnityEngine.UI.Text>(true).text == (i + 1).ToString(),
                    "Journey node order must match the 1-based endpoint id " + (i + 1));
                if (i > 0) Check(Vector3.Distance(buttons[i].transform.position, buttons[i - 1].transform.position) >= 271 * ui.stage.lossyScale.x - .01f,
                    "Saved prefab Journey nodes must not overlap at level " + (i + 1));
            }
            ui.JourneySelfCheck();
            Set("testing", false); Set("journeyPlaying", false); Set("completed", 20); Set("level", 20);
            Set("roundContent", "journey-check-main"); Set("roundMistakes", 1); Set("pendingHint", -1);
            var spots = new[] { new DifferenceSpot(.25f, .5f), new DifferenceSpot(.75f, .5f) };
            roundProperty.SetValue(ui, new DifferenceRound(spots, UIDifferences.ImageAspect, 1, 2));
            Call("SaveRound");
            Check((int)Call("SavedLevel") == 20, "The main round must remain resumable.");

            Set("journeyPlaying", true); Set("journeyCompleted", 0); Set("level", 0);
            Set("roundContent", "journey-check-event");
            roundProperty.SetValue(ui, new DifferenceRound(spots, UIDifferences.ImageAspect, 0, 1));
            Call("SaveRound");
            Check(settings.GetInt(UIDifferences.Key + "RoundLevel") == 20 &&
                settings.GetInt(UIDifferences.Key + "RoundMask") == 1 && settings.GetInt(UIDifferences.Key + "RoundLives") == 2 &&
                settings.GetString(UIDifferences.Key + "RoundContent") == "journey-check-main", "Saving Journey must preserve main progress.");
            Check((int)Call("SavedLevel") == 0 && settings.GetInt(UIDifferences.Key + "Journey.RoundLives") == 1 &&
                settings.GetString(UIDifferences.Key + "Journey.RoundContent") == "journey-check-event", "Journey must resume its own round.");
            Call("ExitJourneyMode");
            Check(!ui.JourneyPlaying && ui.Round == null && ui.Completed == 20 && (int)Call("SavedLevel") == 20,
                "Leaving Journey must restore the main round and main completed count.");
            Debug.Log("PASS: Journey 20-level unlock, IDs 1..200, separate progress/reward eligibility, isolated round saves and main-round restoration.");
        }
        finally
        {
            foreach (var key in keys)
            {
                if (!stored.TryGetValue(key, out var value)) settings.RemoveSetting(key);
                else if (value is string text) settings.SetString(key, text);
                else settings.SetInt(key, (int)value);
            }
            settings.Save();
            foreach (var field in fields) Set(field.Key, field.Value);
            roundProperty.SetValue(ui, previousRound);
            if (design) design.SetActive(designVisible);
        }
    }
}
#endif
