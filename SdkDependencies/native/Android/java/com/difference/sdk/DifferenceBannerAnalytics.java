package com.difference.sdk;

import android.graphics.Point;
import android.util.Log;
import com.applovin.mediation.MaxAdFormat;
import com.applovin.mediation.ads.MaxAdView;
import com.applovin.mediation.unity.MaxUnityAdManager;
import com.applovin.mediation.unity.MaxUnityPlugin;
import com.unity3d.player.UnityPlayer;
import java.lang.reflect.Method;

/** Adds request telemetry to the existing MAX banner; does not replace its ad/revenue listeners. */
public final class DifferenceBannerAnalytics {
    public static void createBanner(final String adUnit, final String unityObject) {
        UnityPlayer.currentActivity.runOnUiThread(() -> {
            try {
                MaxUnityAdManager manager = MaxUnityPlugin.getAdManager();
                // MAX Unity 8.6.5 has no public request hook. This pinned private factory creates
                // the existing managed view WITHOUT loading it. Re-check this signature on upgrades.
                Method retrieve = MaxUnityAdManager.class.getDeclaredMethod("retrieveAdView",
                        String.class, MaxAdFormat.class, String.class, Point.class, boolean.class);
                retrieve.setAccessible(true);
                MaxAdView view = (MaxAdView) retrieve.invoke(manager, adUnit,
                        MaxUnityPlugin.isTablet() ? MaxAdFormat.LEADER : MaxAdFormat.BANNER,
                        "bottom_center", new Point(0, 0), true);
                view.setRequestListener(id -> UnityPlayer.UnitySendMessage(unityObject, "OnBannerRequestStarted", id));
            } catch (Exception exception) {
                // Preserve the original ad flow if a future MAX version changes its private factory.
                // Missing telemetry is preferable to inventing requests or breaking the game.
                Log.w("DifferenceSdk", "Banner request listener unavailable: " + exception.getClass().getSimpleName());
            }
            MaxUnityPlugin.createBanner(adUnit, "bottom_center", true);
            MaxUnityPlugin.hideBanner(adUnit);
            MaxUnityPlugin.stopBannerAutoRefresh(adUnit);
        });
    }
}
