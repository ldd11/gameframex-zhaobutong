#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GameFrameX.Startup.Application;
using Hotfix.Manager;
using UnityEngine;

namespace Hotfix.UI
{
    // Saved alongside the existing round. Runtime frame samples are deliberately not serialized.
    [Serializable]
    internal sealed class DifferenceRoundAnalyticsClock
    {
        public int level;
        public string content, picture;
        public double duration, front, part, savedUtc, frozenUtc;
        public long tips;
        public bool running;
        double lastFrame;
        bool wasOperable;

        static double NonNegative(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);
        public void Restore(bool active, double utc, double frame)
        {
            duration = NonNegative(duration); front = NonNegative(front); part = NonNegative(part); tips = Math.Max(0, tips);
            if (running && active && savedUtc > 0) duration += NonNegative(utc - savedUtc);
            if (!active && frozenUtc <= 0) frozenUtc = utc;
            running = active; savedUtc = utc; lastFrame = frame; wasOperable = false;
        }
        public double Sample(double utc, double frame, bool operable)
        {
            var active = running && wasOperable && operable ? NonNegative(frame - lastFrame) : 0;
            if (running) duration += NonNegative(frame - lastFrame);
            front += active; part += active;
            savedUtc = utc; lastFrame = frame; wasOperable = operable;
            return active;
        }
        public void Freeze(double utc) { running = false; frozenUtc = utc; }
        public void Resume(double utc, double frame)
        {
            // Failure snapshots stay frozen; a later revive includes the intervening wait only in total elapsed time.
            if (!running && frozenUtc > 0) duration += NonNegative(utc - frozenUtc);
            running = true; frozenUtc = 0; savedUtc = utc; lastFrame = frame; wasOperable = false;
        }
        public double FoundPart() { var elapsed = part; part = 0; return elapsed; }
    }

    public sealed partial class UIDifferences
    {
        const string AnalyticsRoundKey = Key + "AnalyticsRound";
        DifferenceRoundAnalyticsClock analyticsClock;
        DifferenceRound analyticsRound;
        bool analyticsSuppressedRound, analyticsPaused, analyticsUnfocused, analyticsTerminalTracked;
        bool analyticsHintVisible, analyticsReviveVisible, analyticsLoading;
        double analyticsLoadStarted, analyticsLoadAdStarted = -1, analyticsLoadAdSeconds;
        bool AnalyticsEnabled => DifferenceAnalytics.Enabled && !testing;
        bool AnalyticsRoundEnabled => AnalyticsEnabled && !analyticsSuppressedRound && analyticsClock != null && Round == analyticsRound;
        static double AnalyticsUtc => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        static string Known(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

        bool AnalyticsOperable => adUiOpen && isActiveAndEnabled && !analyticsPaused && !analyticsUnfocused &&
            currentPage == play && play.activeInHierarchy && Round != null && !Round.Finished && !settled &&
            !IsLoadingLevel && !AdInputBlocked && !figmaResultActive && !modal.activeSelf && !settings.activeSelf &&
            !profile.activeSelf && !musicPanel.activeSelf && !achievements.activeSelf && !album.activeSelf &&
            !(hintDesign && hintDesign.activeSelf);

        void SampleAnalytics()
        {
            if (!AnalyticsRoundEnabled) return;
            var active = analyticsClock.Sample(AnalyticsUtc, Time.realtimeSinceStartupAsDouble, AnalyticsOperable);
            if (active > 0) DifferenceAnalytics.AddActivePlayTime(active);
        }

        void OpenAnalyticsRound(bool restored, bool isRetry)
        {
            analyticsRound = Round; analyticsSuppressedRound = false; analyticsClock = null;
            analyticsTerminalTracked = Round.Finished;
            if (!AnalyticsEnabled) return;
            var data = CurrentLevel;
            // Local assets have no backend id. A real asset name identifies the local picture across replays.
            if (!UsingRemoteLevel && data.original)
            {
                if (string.IsNullOrWhiteSpace(data.analyticsPicId) && !string.IsNullOrWhiteSpace(data.original.name)) data.analyticsPicId = "local:" + data.original.name;
                if (string.IsNullOrWhiteSpace(data.analyticsPicName)) data.analyticsPicName = Known(data.original.name);
            }
            if (restored)
            {
                try { analyticsClock = JsonUtility.FromJson<DifferenceRoundAnalyticsClock>(GameApp.Setting.GetString(AnalyticsRoundKey, "")); }
                catch (Exception) { analyticsClock = null; }
            }
            if (analyticsClock == null || analyticsClock.level != level || analyticsClock.content != roundContent || analyticsClock.picture != Known(data.analyticsPicId))
                analyticsClock = new DifferenceRoundAnalyticsClock { level = level, content = roundContent, picture = Known(data.analyticsPicId) };
            analyticsClock.Restore(!Round.Finished, AnalyticsUtc, Time.realtimeSinceStartupAsDouble);
            if (restored) return;
            var fields = AnalyticsFields();
            fields.Add("is_retry"); fields.Add(isRetry ? "1" : "0");
            DifferenceAnalytics.Track("level_click", fields.ToArray());
            if (level < 50) DifferenceAnalytics.TrackAdjust("level_click_" + (level + 1).ToString(CultureInfo.InvariantCulture));
        }

        List<object> AnalyticsFields() => new List<object> {
            "level", (level + 1).ToString(CultureInfo.InvariantCulture),
            "pic_id", Known(CurrentLevel.analyticsPicId), "pic_name", Known(CurrentLevel.analyticsPicName),
            "nowcoin", (long)coins, "nowitips", (long)hints, "difficulty", Known(CurrentLevel.analyticsDifficulty) };

        void SaveAnalyticsRound()
        {
            if (!AnalyticsRoundEnabled) return;
            SampleAnalytics();
            GameApp.Setting.SetString(AnalyticsRoundKey, JsonUtility.ToJson(analyticsClock));
        }

        void OnApplicationQuit()
        {
            if (Round != null) SaveRound();
            if (AnalyticsEnabled) DifferenceAnalytics.Flush();
        }

        void CountAnalyticsHint() { if (AnalyticsRoundEnabled) { SampleAnalytics(); analyticsClock.tips++; SaveAnalyticsRound(); } }

        void TrackAnalyticsPart(int index)
        {
            if (!AnalyticsRoundEnabled) return;
            SampleAnalytics();
            var data = CurrentLevel;
            var part = data.analyticsParts != null && index < data.analyticsParts.Length ? data.analyticsParts[index] : null;
            DifferenceAnalytics.Track("duration_part", "level", (level + 1).ToString(CultureInfo.InvariantCulture),
                "pic_id", Known(data.analyticsPicId), "pic_name", Known(data.analyticsPicName),
                "difficulty", Known(data.analyticsDifficulty), "progress", (long)Round.Count,
                "part", Known(part), "duration", analyticsClock.FoundPart());
        }

        void FreezeAnalyticsRound()
        {
            if (!AnalyticsRoundEnabled) return;
            SampleAnalytics(); analyticsClock.Freeze(AnalyticsUtc);
        }

        void TrackAnalyticsResult(bool won)
        {
            if (!AnalyticsRoundEnabled || analyticsTerminalTracked) return;
            analyticsTerminalTracked = true;
            TrackAnalyticsState(won ? "level_complete" : "level_fail", !won);
            if (won && Known(CurrentLevel.analyticsPicId) != "unknown") DifferenceAnalytics.RecordCompletedPicture(CurrentLevel.analyticsPicId);
        }

        void TrackAnalyticsState(string name, bool progress, string reviveType = null)
        {
            var fields = AnalyticsFields();
            if (progress) { fields.Add("progress"); fields.Add((long)Round.Count); }
            fields.Add("duration"); fields.Add(analyticsClock.duration);
            fields.Add("duration_front"); fields.Add(analyticsClock.front);
            fields.Add("usetips"); fields.Add(analyticsClock.tips);
            if (reviveType != null) { fields.Add("type"); fields.Add(reviveType); }
            DifferenceAnalytics.Track(name, fields.ToArray());
        }

        void TrackAnalyticsRevive(bool spendCoins)
        {
            if (!AnalyticsRoundEnabled) return;
            analyticsClock.Resume(AnalyticsUtc, Time.realtimeSinceStartupAsDouble);
            analyticsTerminalTracked = false;
            TrackAnalyticsState("level_revive", true, spendCoins ? "coin" : "rewarded");
        }

        void TrackRewardIntent(string timing)
        { if (AnalyticsEnabled && !analyticsSuppressedRound) DifferenceAnalytics.Track("reward_ad_trigger", "trigger_timing", timing); }

        void UpdateRewardEntryAnalytics()
        {
            var shown = AnalyticsEnabled && !analyticsSuppressedRound && adUiOpen && isActiveAndEnabled &&
                !analyticsPaused && !analyticsUnfocused && !AdInputBlocked && !IsLoadingLevel && !modal.activeSelf && !settings.activeSelf;
            var hint = shown && hintDesign && hintDesign.activeInHierarchy && designFreeButton && designFreeButton.IsActive() && designFreeButton.IsInteractable();
            var revive = shown && figmaResultActive && !figmaResultWon && designContinueButton && designContinueButton.IsActive() && designContinueButton.IsInteractable();
            if (hint && !analyticsHintVisible) DifferenceAnalytics.Track("reward_ad_entry_show", "trigger_timing", "hint_1");
            if (revive && !analyticsReviveVisible) DifferenceAnalytics.Track("reward_ad_entry_show", "trigger_timing", "revive");
            analyticsHintVisible = hint; analyticsReviveVisible = revive;
        }

        void BeginAnalyticsLoad()
        {
            analyticsLoading = AnalyticsEnabled;
            analyticsLoadStarted = Time.realtimeSinceStartupAsDouble;
            analyticsLoadAdSeconds = 0; analyticsLoadAdStarted = DifferenceAds.Busy ? analyticsLoadStarted : -1;
            if (analyticsLoading) DifferenceAnalytics.Track("jigsaw_level_trigger", "network", DifferenceAnalytics.Network);
        }
        void SampleAnalyticsLoadAd(bool shown)
        {
            if (!analyticsLoading) return;
            if (shown && analyticsLoadAdStarted < 0) analyticsLoadAdStarted = Time.realtimeSinceStartupAsDouble;
            else if (!shown && analyticsLoadAdStarted >= 0)
            { analyticsLoadAdSeconds += Math.Max(0, Time.realtimeSinceStartupAsDouble - analyticsLoadAdStarted); analyticsLoadAdStarted = -1; }
        }
        void CancelAnalyticsLoad() { analyticsLoading = false; analyticsLoadAdStarted = -1; }
        void FinishAnalyticsLoad()
        {
            if (!analyticsLoading || !AnalyticsEnabled) return;
            SampleAnalyticsLoadAd(false);
            DifferenceAnalytics.Track("loading_level_enter", "category_to_loaded", Math.Max(0, Time.realtimeSinceStartupAsDouble - analyticsLoadStarted) * 1000,
                "ad_during_load", analyticsLoadAdSeconds * 1000, "image_id", Known(CurrentLevel.analyticsPicId));
            CancelAnalyticsLoad();
        }
        void FailAnalyticsLoad(string error, DifferenceLevel data)
        {
            if (!analyticsLoading || !AnalyticsEnabled) return;
            var cause = error != null && error.StartsWith("关卡下载失败", StringComparison.Ordinal) ? "level_download_failed" : "invalid_level_data";
            DifferenceAnalytics.Track("splash_to_jigsaw_fail", "image_id", Known(data?.analyticsPicId), "cause", cause, "network", DifferenceAnalytics.Network);
            CancelAnalyticsLoad();
        }
        IEnumerator TrackHomeReturn(double started)
        {
            yield return null;
            if (AnalyticsEnabled && !analyticsSuppressedRound && currentPage == home && homeDesign.activeInHierarchy && !IsLoadingLevel)
                DifferenceAnalytics.Track("category_cover_switch", "total_duration", Math.Max(0, Time.realtimeSinceStartupAsDouble - started) * 1000);
        }
    }
}
#endif
