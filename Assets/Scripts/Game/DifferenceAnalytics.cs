using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GameFrameX.Startup.Application
{
    // The optional SDK owns the senders; HotFix never references Firebase or Adjust.
    public static class DifferenceAnalytics
    {
        const string Key = "DifferenceAnalytics.";
        const int QueueLimit = 512;
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        [Serializable] sealed class Field
        {
            public string name, text, kind;
            public long integer;
            public double number;
        }
        [Serializable] sealed class Event
        {
            public string name;
            public bool adjust;
            public Field[] fields;
        }
        [Serializable] sealed class Pending { public List<Event> events = new List<Event>(); }
        static Pending pending = new Pending();
        static Action<string, Dictionary<string, object>> firebaseSender;
        static Func<string, bool> adjustSender;
        static bool initialized, draining;
        static decimal revenue;
        static double firstLaunchUtc, activeSeconds, unsavedSeconds;
        static int bannerRequests, bannerShown, bannerLoadFailed, bannerShowFailed;

        public static bool Enabled => DifferenceAds.SdkEnabled;
        public static string Network
        {
            get
            {
                switch (UnityEngine.Application.internetReachability)
                {
                    case NetworkReachability.NotReachable: return "offline";
                    case NetworkReachability.ReachableViaCarrierDataNetwork: return "cellular";
                    case NetworkReachability.ReachableViaLocalAreaNetwork: return "wifi";
                    default: return "unknown";
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime()
        {
            initialized = draining = false;
            pending = new Pending();
            firebaseSender = null; adjustSender = null;
            unsavedSeconds = 0;
        }

        public static void Initialize()
        {
            if (!Enabled || initialized) return;
            initialized = true;
            try
            {
                var json = PlayerPrefs.GetString(Key + "Pending", "");
                if (!string.IsNullOrEmpty(json)) pending = JsonUtility.FromJson<Pending>(json) ?? new Pending();
                if (pending.events == null) pending.events = new List<Event>();
            }
            catch (Exception) { pending = new Pending(); Debug.LogWarning("[Analytics] Invalid saved event queue discarded."); }
            firstLaunchUtc = ReadDouble("FirstLaunchUtc");
            if (firstLaunchUtc <= 0)
            {
                firstLaunchUtc = UtcNow;
                WriteDouble("FirstLaunchUtc", firstLaunchUtc);
            }
            decimal.TryParse(PlayerPrefs.GetString(Key + "RevenueUsd", "0"), NumberStyles.Number, Invariant, out revenue);
            revenue = Math.Max(0, revenue);
            activeSeconds = Math.Max(0, ReadDouble("D0ActiveSeconds"));
            bannerRequests = Math.Max(0, PlayerPrefs.GetInt(Key + "BannerRequests", 0));
            bannerShown = Math.Max(0, PlayerPrefs.GetInt(Key + "BannerShown", 0));
            bannerLoadFailed = Math.Max(0, PlayerPrefs.GetInt(Key + "BannerLoadFailed", 0));
            bannerShowFailed = Math.Max(0, PlayerPrefs.GetInt(Key + "BannerShowFailed", 0));
            // Carry the last incomplete batch into the next launch, before new requests arrive.
            ReportBannerBatch();
            PlayerPrefs.Save();
        }

        public static bool Track(string name, params object[] keyValues)
        {
            if (!Enabled) return false;
            Initialize();
            if (!Identifier(name) || keyValues == null || keyValues.Length % 2 != 0 || keyValues.Length > 50) return false;
            var fields = new List<Field>();
            var names = new HashSet<string>();
            for (var i = 0; i < keyValues.Length; i += 2)
            {
                var fieldName = keyValues[i] as string;
                if (!Identifier(fieldName) || !names.Add(fieldName)) return false;
                var value = keyValues[i + 1];
                var field = new Field { name = fieldName };
                if (value is string text) { field.kind = "s"; field.text = text.Length > 100 ? text.Substring(0, 100) : text; }
                else if (value is long integer) { field.kind = "l"; field.integer = integer; }
                else if (value is int smallInteger) { field.kind = "l"; field.integer = smallInteger; }
                else if (value is double number && Finite(number)) { field.kind = "d"; field.number = number; }
                else return false;
                fields.Add(field);
            }
            return Enqueue(new Event { name = name, fields = fields.ToArray() });
        }

        public static bool TrackOnce(string name, params object[] keyValues)
        {
            if (!Enabled || PlayerPrefs.GetInt(Key + "Once." + name, 0) == 1) return false;
            if (!Track(name, keyValues)) return false;
            PlayerPrefs.SetInt(Key + "Once." + name, 1); PlayerPrefs.Save();
            return true;
        }

        public static void TrackAdjust(string name)
        {
            if (!Enabled || !Identifier(name)) return;
            Initialize();
            Enqueue(new Event { name = name, adjust = true, fields = new Field[0] });
        }

        public static void TrackAdjustOnce(string name)
        {
            if (!Enabled || !Identifier(name) || PlayerPrefs.GetInt(Key + "AdjustOnce." + name, 0) == 1) return;
            Initialize();
            if (!Enqueue(new Event { name = name, adjust = true, fields = new Field[0] })) return;
            PlayerPrefs.SetInt(Key + "AdjustOnce." + name, 1); PlayerPrefs.Save();
        }

        public static void SetFirebaseSender(Action<string, Dictionary<string, object>> sender)
        { if (!Enabled) return; Initialize(); firebaseSender = sender; Drain(); }
        public static void SetAdjustSender(Func<string, bool> sender)
        { if (!Enabled) return; Initialize(); adjustSender = sender; Drain(); }

        static bool Enqueue(Event item)
        {
            Drain();
            // ponytail: 512 pending events per SDK; use a database if a larger offline outbox is needed.
            // Unconfigured Adjust tokens must not exhaust Firebase's capacity.
            var channelCount = 0;
            foreach (var queued in pending.events) if (queued.adjust == item.adjust) channelCount++;
            if (channelCount >= QueueLimit) { Debug.LogWarning("[Analytics] Event queue full; event not accepted: " + item.name); return false; }
            pending.events.Add(item);
            SavePending();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Analytics] " + (item.adjust ? "Adjust " : "Firebase ") + JsonUtility.ToJson(item));
#endif
            Drain();
            return true;
        }

        static void Drain()
        {
            if (draining || !Enabled) return;
            draining = true;
            var changed = false;
            try
            {
                for (var i = 0; i < pending.events.Count;)
                {
                    var item = pending.events[i];
                    var sent = false;
                    try
                    {
                        if (item.adjust) sent = adjustSender != null && adjustSender(item.name);
                        else if (firebaseSender != null)
                        {
                            var values = new Dictionary<string, object>();
                            foreach (var field in item.fields)
                                values[field.name] = field.kind == "s" ? (object)field.text : field.kind == "l" ? (object)field.integer : field.number;
                            firebaseSender(item.name, values); sent = true;
                        }
                    }
                    catch (Exception) { /* The SDK can become unavailable during startup; retain the event. */ }
                    if (sent) { pending.events.RemoveAt(i); changed = true; }
                    else i++;
                }
            }
            finally { draining = false; if (changed) SavePending(); }
        }

        public static void RecordAdRevenue(double value, string format, string network, string adUnit, string placement = null)
        {
            if (!Enabled || !Finite(value) || value < 0) return;
            Initialize();
            decimal next;
            try { next = checked(revenue + (decimal)value); }
            catch (OverflowException) { return; }
            var values = new List<object> { "currency", "USD", "value", value, "ad_platform", "AppLovin",
                "ad_format", format ?? "unknown", "ad_source", network ?? "unknown", "ad_unit_name", adUnit ?? "unknown" };
            if (!string.IsNullOrEmpty(placement)) { values.Add("trigger_timing"); values.Add(placement); }
            Track("ad_impression", values.ToArray());
            revenue = next;
            PlayerPrefs.SetString(Key + "RevenueUsd", revenue.ToString(Invariant));
            var thresholds = new[] { .01m, .02m, .03m, .05m, .10m };
            var events = new[] { "ad_revenue_001", "ad_revenue_002", "ad_revenue_003", "ad_revenue_005", "ad_revenue_010" };
            for (var i = 0; i < thresholds.Length; i++)
                if (revenue >= thresholds[i]) TrackOnce(events[i], "currency", "USD", "threshold_usd", (double)thresholds[i], "cumulative_ad_revenue_usd", (double)revenue);
            PlayerPrefs.Save();
        }

        public static void RecordCompletedPicture(string pictureId)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(pictureId)) return;
            Initialize();
            var pictureKey = Key + "Picture." + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pictureId));
            var count = Math.Max(0, PlayerPrefs.GetInt(Key + "CompletedPictures", 0));
            if (PlayerPrefs.GetInt(pictureKey, 0) == 0)
            {
                PlayerPrefs.SetInt(pictureKey, 1); count++;
                PlayerPrefs.SetInt(Key + "CompletedPictures", count);
            }
            foreach (var threshold in new[] { 10, 20, 30, 50 })
                if (count >= threshold) TrackOnce("level_" + threshold + "_complete");
            PlayerPrefs.Save();
        }

        public static void AddActivePlayTime(double seconds)
        {
            if (!Enabled || !Finite(seconds) || seconds <= 0) return;
            Initialize();
            var now = UtcNow;
            var eligible = Math.Max(0, Math.Min(now, firstLaunchUtc + 86400) - Math.Max(now - seconds, firstLaunchUtc));
            if (eligible <= 0) return;
            activeSeconds += eligible; unsavedSeconds += eligible;
            foreach (var minutes in new[] { 10, 30, 60 })
                if (activeSeconds >= minutes * 60) TrackOnce("d0_" + minutes + "m_play");
            if (unsavedSeconds >= 10) SavePlayTime();
        }

        public static void TutorialStepCompleted(int step)
        {
            var events = new[] { "first_step", "second_step", "third_step", "fourth_step" };
            if (step >= 1 && step <= events.Length) TrackOnce(events[step - 1]);
        }
        public static void RatingPopupShown() => Track("rating_popup_show");
        public static void RatingClicked(int rating)
        { if (rating >= 1 && rating <= 5) Track(rating == 5 ? "rating_click_5" : "rating_click_1_4", "rating", (long)rating); }

        public static void BannerRequest() { if (!Enabled) return; Initialize(); bannerRequests++; SaveBanner(); }
        public static void BannerShown() { if (!Enabled) return; Initialize(); bannerShown++; SaveBanner(); BannerFlushIfReady(); }
        public static void BannerLoaded() => BannerFlushIfReady();
        public static void BannerLoadFailed() { if (!Enabled) return; Initialize(); bannerLoadFailed++; SaveBanner(); BannerFlushIfReady(); }
        public static void BannerShowFailed() { if (!Enabled) return; Initialize(); bannerShowFailed++; SaveBanner(); BannerFlushIfReady(); }
        public static void BannerFlushIfReady() { if (!Enabled) return; Initialize(); if (bannerRequests >= 20) ReportBannerBatch(); }
        static void ReportBannerBatch()
        {
            if (bannerRequests == 0 && bannerShown == 0 && bannerLoadFailed == 0 && bannerShowFailed == 0) return;
            if (!Track("banner_ad_batch_report", "max_ad_request_count", (long)bannerRequests,
                "max_ad_show_success_count", (long)bannerShown, "max_ad_load_fail_count", (long)bannerLoadFailed,
                "max_ad_show_fail_count", (long)bannerShowFailed)) return;
            bannerRequests = bannerShown = bannerLoadFailed = bannerShowFailed = 0; SaveBanner();
        }
        static void SaveBanner()
        {
            PlayerPrefs.SetInt(Key + "BannerRequests", bannerRequests); PlayerPrefs.SetInt(Key + "BannerShown", bannerShown);
            PlayerPrefs.SetInt(Key + "BannerLoadFailed", bannerLoadFailed); PlayerPrefs.SetInt(Key + "BannerShowFailed", bannerShowFailed);
            PlayerPrefs.Save();
        }
        static void SavePlayTime() { WriteDouble("D0ActiveSeconds", activeSeconds); unsavedSeconds = 0; PlayerPrefs.Save(); }
        public static void Flush() { if (!Enabled) return; Initialize(); SavePlayTime(); Drain(); PlayerPrefs.Save(); }
        static void SavePending() { PlayerPrefs.SetString(Key + "Pending", JsonUtility.ToJson(pending)); PlayerPrefs.Save(); }
        static double UtcNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static double ReadDouble(string name) => double.TryParse(PlayerPrefs.GetString(Key + name, "0"), NumberStyles.Float, Invariant, out var value) && Finite(value) ? value : 0;
        static void WriteDouble(string name, double value) => PlayerPrefs.SetString(Key + name, value.ToString("R", Invariant));
        static bool Identifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 40 || !Letter(value[0])) return false;
            foreach (var c in value) if (!Letter(c) && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }
        static bool Letter(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';
    }
}
