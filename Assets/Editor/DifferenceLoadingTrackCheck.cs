#if ENABLE_UI_UGUI
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DifferenceLoadingTrackCheck
{
    [MenuItem("Tools/Find Differences/Check Loading Track")]
    public static void Run()
    {
        var root = new GameObject("Track", typeof(RectTransform));
        try
        {
            var track = (RectTransform)root.transform;
            var child = new GameObject("Progress", typeof(RectTransform));
            child.transform.SetParent(track, false);
            var fill = (RectTransform)child.transform;
            var caption = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            caption.transform.SetParent(track, false);
            var text = caption.GetComponent<Text>();
            var expected = new[] { "Hiding a few surprises...", "Getting the tiny details just right...", "Your next “Aha!” moment is loading...", "A little looking, a lot of joy." };
            var handler = Type.GetType("GameFrameX.Startup.Application.LauncherUIHandler, GameFrameX.Startup.Application", true);
            var update = handler.GetMethod("UpdateLoadingTrack");
            foreach (var width in new[] { 300f, 700f })
            {
                track.sizeDelta = new Vector2(width, 40);
                fill.anchoredPosition = new Vector2(-width * .5f + 10, 0);
                foreach (var value in new[] { 0f, .5f, 1f, -1f, 2f })
                {
                    update.Invoke(null, new object[] { track, 0f, value });
                    var target = (width - 20) * Mathf.Clamp01(value);
                    if (Mathf.Abs(fill.rect.width - target) > .01f) throw new Exception("Wrong progress width: " + fill.rect.width + " != " + target);
                }
                for (var i = 0; i < 9; i++)
                {
                    update.Invoke(null, new object[] { track, i * 3f, .5f });
                    if (text.text != expected[i % 4]) throw new Exception("Wrong caption at " + i * 3 + ": " + text.text);
                }
            }
            Debug.Log("PASS: loading track 0/50/100 percent, clamping, two widths and all four rotating captions.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
