#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameFrameX;
using UnityEngine.UI;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        static readonly Dictionary<string, string> LiteralKeys = new Dictionary<string, string>
        {
            { "Start with one corner and work your way across.", "level_tip_01" },
            { "Compare small sections, one at a time.", "level_tip_02" },
            { "Look for changes in color, shape, and size.", "level_tip_03" },
            { "Count the little things. Is something missing?", "level_tip_04" },
            { "Check the edges—you might find a surprise.", "level_tip_05" },
            { "Look between the big things for tiny clues.", "level_tip_06" },
            { "Flowers, buttons, stripes… little details can be sneaky.", "level_tip_07" },
            { "Does anything look longer, shorter, or slightly different?", "level_tip_08" },
            { "Stuck? Try looking at a different part of the picture.", "level_tip_09" },
            { "Take a little break, then look again with fresh eyes.", "level_tip_10" },
            { "You don’t need eagle eyes—just a little curiosity.", "level_tip_11" },
            { "Every difference you find is a little victory.", "level_tip_12" },
            { "Need a little help? Tap the hint button.", "level_tip_13" },
            { "Hiding a few surprises...", "startup_tip_01" },
            { "Getting the tiny details just right...", "startup_tip_02" },
            { "Your next “Aha!” moment is loading...", "startup_tip_03" },
            { "A little looking, a lot of joy.", "startup_tip_04" },
            { "Level Album", "level_album" },
            { "Level progress restored.", "level_restored" },
            { "Unable to load levels. Please check your connection and try again.", "startup_failed" },
            { "Home", "home" },
            { "This link is unavailable. Please try again later.", "link_unavailable" },
            { "You already claimed today's free hint.", "hint_claimed_today" },
            { "Not enough coins. You need 100 coins.", "not_enough_coins_100" },
            { "You need 30 coins to continue, or retry for free.", "reward_revive_cost" },
            { "Claimed", "hint_claimed" },
            { "Daily Free Hint", "daily_free_hint" },
            { "Reward Claimed", "reward_claimed" },
            { "You received 1 hint.", "hint_received" },
            { "Collect", "collect" },
            { "Purchase Complete", "purchase_complete" },
            { "Continue", "continue" },
            { "About Ads", "about_ads" },
            { "Ads are disabled in this version.", "ads_disabled" },
            { "Complete 3 levels", "achievement_3" },
            { "Complete all available scenes", "achievement_all" },
            { "Completed", "completed" },
            { "In Progress", "in_progress" },
            { "More scenes coming soon", "more_scenes" },
            { "Online Level", "online_level" },
            { "Tap to load level", "tap_load_level" },
            { "Completed · Replay", "completed_replay" },
            { "Start Finding", "start_finding" },
            { "Locked", "locked" },
            { "Game Data", "game_data" },
            { "OK", "ok" },
            { "Video not ready. Please try again later.", "video_not_ready" },
            { "Tutorial complete! Keep finding differences. Level 1 has unlimited hints.", "tutorial_complete" },
            { "Pinch out to zoom in.", "tutorial_zoom" },
            { "Your nickname cannot be empty.", "nickname_empty" },
            { "This item is unavailable.", "item_unavailable" },
            { "Save", "save" },
            { "Edit Profile", "profile_edit" },
            { "Avatar", "avatar" },
            { "Avatar Frame", "avatar_frame" },
            { "Your ad-free access is active. Banner and interstitial ads are disabled.\nYou can still choose to watch rewarded videos for hints.", "ad_free_active" },
            { "This version includes banner and interstitial ads.\nAd-free purchases are not available yet.\nYou can choose to watch rewarded videos for hints.", "ads_included" },
            { "Your progress, coins, hints and profile are saved automatically.\nYou can leave a level and continue later.", "game_data_help" },
            { "Pinch out to zoom in.\nOn a computer, scroll to zoom.", "tutorial_zoom_pc" },
            { "Tap Hint to find a difference.\nUnlimited hints in this level.", "tutorial_hint" },
            { "Tap the highlighted spot\nto find this difference.", "tutorial_highlight_difference" },
            { "Tap the highlighted spot\nin either picture.", "tutorial_highlight_picture" },
            { "Terrace Sunshine", "prefab_53744e13" },
            { "Unlocks after Level 50.", "prefab_7265a216" },
            { "Start", "prefab_952f3754" },
            { "Coming Soon", "prefab_04d2fd85" },
            { "Settings", "prefab_c7f73bb5" },
            { "Free Daily", "prefab_34d2708f" },
            { "Vibration", "prefab_39b345e7" },
            { "Ad-free experience", "prefab_fcb8306d" },
            { "Leave", "prefab_7e3520a9" },
            { "Contact Us", "prefab_9ad0ccff" },
            { "✦  FIND DIFFERENCES  ✦", "prefab_02ebbfc8" },
            { "Finish a new level without mistakes", "prefab_f06f2750" },
            { "Need a hint?", "prefab_0ff5cec1" },
            { "Pause", "prefab_781961bc" },
            { "Complete 20 levels to unlock Christmas Carnival!", "prefab_c6b46221" },
            { "Keep Playing", "prefab_37a4967e" },
            { "Complete your first level", "prefab_4727788e" },
            { "Coin purchases are coming soon", "prefab_95bd75af" },
            { "Shop", "prefab_55de8870" },
            { "Retry", "prefab_9f5cd8a2" },
            { "Sharp Eye", "prefab_b4f9837d" },
            { "Lazy Weekend", "prefab_043b297f" },
            { "Leaderboard", "prefab_0381247a" },
            { "Perfect Find", "prefab_b0018a91" },
            { "Info", "prefab_4b631f69" },
            { "900 Coins", "prefab_e56cc0e3" },
            { "Achievements", "prefab_a6634f24" },
            { "Sound", "prefab_b4e3efeb" },
            { "Choose Music", "prefab_7d840034" },
            { "Music", "prefab_131260cb" },
            { "Explorer", "prefab_8b3ee457" },
            { "Little Joys", "prefab_2c41701d" },
            { "Leave Level?", "prefab_7ed443e3" },
            { "Free", "prefab_75f52718" },
            { "ADS", "prefab_4f213afc" },
            { "No Ads", "prefab_5fd29f77" },
            { "Terms of Service     |     Privacy Policy", "prefab_97b10b52" },
            { "Use hints to find differences.", "prefab_48fb861b" },
            { "Next Level", "prefab_36b6ce6d" },
            { "First Find", "prefab_16dc920e" },
            { "Level Failed", "prefab_84309913" },
            { "Morning Dew", "prefab_220ed7c3" },
            { "The holiday fun begins—jump in and play!", "prefab_fae6d3eb" },
            { "Find 10 differences in total", "prefab_d76846ad" },
            { "Relax and enjoy every level", "prefab_9cb8e67a" },
            { "Reward", "prefab_c7d7652e" },
            { "Are you sure you want to leave?", "prefab_a05cde34" },
            { "Loading...", "prefab_b04ba49f" },
            { "Cozy Living Room", "scene_cozy_living_room" },
            { "Flower Shop", "scene_flower_shop" },
            { "Seaside Cafe", "scene_seaside_cafe" },
        };

        static string Localize(string key, string fallback, params object[] args)
        {
            if (GameApp.Localization != null && GameApp.Localization.HasRawString(key))
                return GameApp.Localization.GetString(key, args);
            return args.Length == 0 ? fallback : string.Format(fallback, args);
        }

        static string LocalizeLiteral(string value)
        {
            if (value == null) return null;
            if (LiteralKeys.TryGetValue(value, out var key)) return Localize(key, value);
            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            const string versionPrefix = "FIND DIFFERENCES Version ";
            if (normalized.StartsWith(versionPrefix, StringComparison.Ordinal))
                return Localize("game_version", "FIND DIFFERENCES\nVersion {0}", normalized.Substring(versionPrefix.Length));
            return LiteralKeys.TryGetValue(normalized, out key) ? Localize(key, value) : value;
        }

        void LocalizeStaticText()
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                text.text = LocalizeLiteral(text.text);
        }
    }
}
#endif
