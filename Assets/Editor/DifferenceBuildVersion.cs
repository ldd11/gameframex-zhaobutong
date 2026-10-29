using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class DifferenceBuildVersion
{
    // Run after GameFrameX's build number increment and the iOS SDK export setup.
    [PostProcessBuild(1001)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.Android && target != BuildTarget.iOS) return;
        var current = PlayerSettings.bundleVersion;
        if (!TryNextVersion(current, out var next))
        {
            Debug.LogWarning("Cannot increment bundleVersion '" + current + "'. Use a numeric version such as 1.0.1.");
            return;
        }
        PlayerSettings.bundleVersion = next;
        Debug.Log("Built version " + current + "; next build version: " + next);
    }

    internal static bool TryNextVersion(string current, out string next)
    {
        next = current;
        if (!Version.TryParse(current, out _)) return false;
        var separator = current.LastIndexOf('.');
        var last = int.Parse(current.Substring(separator + 1), CultureInfo.InvariantCulture);
        if (last == int.MaxValue) return false;
        next = current.Substring(0, separator + 1) + (last + 1).ToString(CultureInfo.InvariantCulture);
        return true;
    }
}
