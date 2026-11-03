using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GameFrameX.Asset.Runtime;
using GameFrameX.Event.Runtime;
using GameFrameX.GlobalConfig.Runtime;
using GameFrameX.Runtime;
using GameFrameX.Startup.Runtime;
using GameFrameX.UI.Runtime;
using Unity.Startup;
using UnityEngine;

namespace GameFrameX.Startup.Application
{
    /// <summary>
    /// 启动器 UI 处理类，负责加载界面、提示文本、进度条以及强制更新提示的显示与控制。
    /// </summary>
    /// <remarks>
    /// Launcher UI handler responsible for displaying and controlling the loading screen, tip text, progress bar, and force update prompt.
    /// </remarks>
    public static class LauncherUIHandler
    {
        private static UILauncher _ui;
#if ENABLE_UI_UGUI
        private static Transform dataLoadingTrack;
        private static Coroutine dataLoadingRoutine;
        private static readonly string[] LoadingCaptions =
        {
            "Hiding a few surprises...",
            "Getting the tiny details just right...",
            "Your next “Aha!” moment is loading...",
            "A little looking, a lot of joy."
        };

        public static void UpdateLoadingTrack(Transform track, float elapsed, float progress)
        {
            if (!track) return;
            var fill = track.Find("Progress") as RectTransform;
            var caption = track.Find("Caption");
            if (fill && track is RectTransform bounds)
            {
                var inset = Mathf.Max(0, bounds.rect.width * .5f + fill.anchoredPosition.x);
                fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(0, bounds.rect.width - inset * 2) * Mathf.Clamp01(progress));
            }
            if (caption && caption.TryGetComponent<UnityEngine.UI.Text>(out var text))
            {
                var index = (int)(Mathf.Max(0, elapsed) / 3f) % LoadingCaptions.Length;
                var key = "startup_tip_0" + (index + 1);
                text.text = GameApp.Localization.HasRawString(key) ? GameApp.Localization.GetString(key) : LoadingCaptions[index];
            }
        }

        public static void BeginDataLoading()
        {
            if (!_ui) return;
            if (dataLoadingRoutine != null) _ui.StopCoroutine(dataLoadingRoutine);
            if (!dataLoadingTrack) dataLoadingTrack = _ui.transform.Find("Loading");
            if (!dataLoadingTrack) return;
            dataLoadingTrack.gameObject.SetActive(true);
            dataLoadingTrack.SetAsLastSibling();
            dataLoadingRoutine = _ui.StartCoroutine(AnimateDataLoading());
        }

        private static IEnumerator AnimateDataLoading()
        {
            var started = Time.unscaledTime;
            while (dataLoadingTrack)
            {
                var elapsed = Time.unscaledTime - started;
                UpdateLoadingTrack(dataLoadingTrack, elapsed, .9f * (1 - Mathf.Exp(-elapsed / 5f)));
                yield return null;
            }
        }

        public static void EndDataLoading(bool success)
        {
            if (_ui && dataLoadingRoutine != null) _ui.StopCoroutine(dataLoadingRoutine);
            dataLoadingRoutine = null;
            if (!dataLoadingTrack) return;
            if (success) UpdateLoadingTrack(dataLoadingTrack, 0, 1);
            else dataLoadingTrack.gameObject.SetActive(false);
        }
#endif

        /// <summary>
        /// 启动加载界面，并注册 FairyGUI 加载器扩展。
        /// </summary>
        /// <remarks>
        /// Start the loading screen and register the FairyGUI loader extension.
        /// </remarks>
        public static async void Start()
        {
#if ENABLE_UI_FAIRYGUI
            FairyGUI.UIObjectFactory.SetLoaderExtension(typeof(FairyGuiExtensionLoader));
#endif
#if ENABLE_UI_UGUI
            await StartAsync("UI/UILauncher/UGUI");
#else
            await StartAsync("UI/UILauncher");
#endif
        }

        /// <summary>
        /// 异步打开启动器 UI 并订阅资源下载进度事件。
        /// </summary>
        /// <remarks>
        /// Asynchronously open the launcher UI and subscribe to the asset download progress event.
        /// </remarks>
        /// <param name="uiResName">启动器 UI 资源名称 / The launcher UI asset name</param>
        /// <returns>异步任务 / The async task</returns>
        public static async UniTask StartAsync(string uiResName)
        {
            DifferenceLocalization.Initialize();
            try
            {
                _ui = await GameApp.UI.OpenFullScreenAsync<UILauncher>(uiResName);
#if ENABLE_UI_UGUI
                if (_ui) DifferenceLocalizedFonts.Apply(_ui.gameObject);
#endif
                if (DifferenceAnalytics.Enabled)
                {
                    await UniTask.NextFrame();
                    if (_ui != null && _ui.Visible) DifferenceStartupAnalytics.SplashVisible();
                }
            }
            catch
            {
                DifferenceStartupAnalytics.Fail("launcher_load_failed");
                throw;
            }

            GameApp.Event.CheckSubscribe(AssetDownloadProgressUpdateEventArgs.EventId, SetProgressUpdate);
        }

        /// <summary>
        /// 关闭启动器 UI 并释放资源。
        /// </summary>
        /// <remarks>
        /// Close the launcher UI and release resources.
        /// </remarks>
        public static void Dispose()
        {
#if ENABLE_UI_UGUI
            dataLoadingRoutine = null;
            dataLoadingTrack = null;
#endif
            GameApp.UI.CloseUIForm<UILauncher>();
            _ui = null;
            DifferenceStartupAnalytics.LauncherClosed();
        }

        /// <summary>
        /// 设置启动器提示文本。
        /// </summary>
        /// <remarks>
        /// Set the launcher tip text.
        /// </remarks>
        /// <param name="text">要显示的提示文本 / The tip text to display</param>
        public static void SetTipText(string text)
        {
            if (_ui == null)
            {
                return;
            }

            _ui.m_TipText.text = text;
        }

        /// <summary>
        /// 标记资源下载完成，切换下载面板的显示状态。
        /// </summary>
        /// <remarks>
        /// Mark asset download as finished and switch the download panel display state.
        /// </remarks>
        public static void SetProgressUpdateFinish()
        {
            if (_ui == null)
            {
                return;
            }

#if ENABLE_UI_FAIRYGUI
            _ui.m_IsDownload.SetSelectedIndex(0);
#endif
        }

        /// <summary>
        /// 设置下载进度条和已下载大小信息。
        /// </summary>
        /// <remarks>
        /// Set the download progress bar and downloaded size info.
        /// </remarks>
        /// <param name="progress">下载进度，取值范围 [0, 1] / Download progress in range [0, 1]</param>
        /// <param name="sizeInfo">已下载与总大小信息字符串 / Downloaded size and total size info string</param>
        public static void SetProgress(float progress, string sizeInfo)
        {
            if (_ui == null)
            {
                return;
            }

#if ENABLE_UI_FAIRYGUI
            _ui.m_IsDownload.SetSelectedIndex(1);
            _ui.m_ProgressBar.value = Mathf.Clamp01(progress) * 100;
#else
            if (_ui.m_ProgressBar) _ui.m_ProgressBar.value = Mathf.Clamp01(progress) * 100;
            var track = _ui.transform.Find("Loading");
            if (track) UpdateLoadingTrack(track, Time.unscaledTime, progress);
#endif
            _ui.m_TipText.text = GameApp.Localization.HasRawString("download_size")
                ? GameApp.Localization.GetString("download_size", sizeInfo)
                : $"Downloading {sizeInfo}";
        }

        /// <summary>
        /// 显示强制或可选更新提示面板。
        /// </summary>
        /// <remarks>
        /// Show the force or optional update prompt panel.
        /// </remarks>
        /// <param name="gameAppVersion">版本信息响应 / The version info response</param>
        /// <param name="onContinue">用户确认更新后的回调 / Callback invoked after user confirms the update</param>
        public static void ShowUpgrade(ResponseGameAppVersion gameAppVersion, Action onContinue)
        {
#if ENABLE_UI_FAIRYGUI
            if (_ui == null)
            {
                onContinue?.Invoke();
                return;
            }

            _ui.m_IsUpgrade.SetSelectedIndex(1);
            _ui.m_upgrade.m_EnterButton.title = "OK";
            _ui.m_upgrade.m_TextContent.title = gameAppVersion.UpdateAnnouncement;
            _ui.m_upgrade.m_TextContent.onClickLink.Set(context => { ApplicationHelper.OpenURL(context.data.ToString()); });
            _ui.m_upgrade.m_EnterButton.onClick.Set(() =>
            {
                if (gameAppVersion.IsForce)
                {
                    ApplicationHelper.OpenURL(gameAppVersion.AppDownloadUrl);
                    return;
                }

                _ui.m_IsUpgrade.SetSelectedIndex(0);
                onContinue?.Invoke();
            });
#else
            onContinue?.Invoke();
#endif
        }

        /// <summary>
        /// 异步显示强制或可选更新提示面板，并返回用户是否继续。
        /// </summary>
        /// <remarks>
        /// Asynchronously show the force or optional update prompt panel and return whether the user continues.
        /// </remarks>
        /// <param name="upgradeInfo">启动器更新信息 / The launcher upgrade info</param>
        /// <returns>异步任务，用户确认后返回结果 / The async task returning the result after user confirmation</returns>
        public static UniTask<bool> ShowUpgradeAsync(StartupUpgradeInfo upgradeInfo)
        {
            var completionSource = new UniTaskCompletionSource<bool>();
#if ENABLE_UI_FAIRYGUI
            if (_ui == null)
            {
                completionSource.TrySetResult(true);
                return completionSource.Task;
            }

            _ui.m_IsUpgrade.SetSelectedIndex(1);
            _ui.m_upgrade.m_EnterButton.title = "OK";
            _ui.m_upgrade.m_TextContent.title = upgradeInfo.UpdateAnnouncement;
            _ui.m_upgrade.m_TextContent.onClickLink.Set(context => { ApplicationHelper.OpenURL(context.data.ToString()); });
            _ui.m_upgrade.m_EnterButton.onClick.Set(() =>
            {
                if (upgradeInfo.IsForce)
                {
                    ApplicationHelper.OpenURL(upgradeInfo.AppDownloadUrl);
                    completionSource.TrySetResult(false);
                    return;
                }

                _ui.m_IsUpgrade.SetSelectedIndex(0);
                completionSource.TrySetResult(true);
            });
#else
            completionSource.TrySetResult(true);
#endif
            return completionSource.Task;
        }

        /// <summary>
        /// 资源下载进度事件处理函数，更新进度条和已下载大小信息。
        /// </summary>
        /// <remarks>
        /// Asset download progress event handler that updates the progress bar and downloaded size info.
        /// </remarks>
        /// <param name="sender">事件源对象 / Event source object</param>
        /// <param name="gameEventArgs">资源下载进度更新事件参数 / Asset download progress update event arguments</param>
        public static void SetProgressUpdate(object sender, GameEventArgs gameEventArgs)
        {
            if (_ui == null)
            {
                return;
            }

#if ENABLE_UI_FAIRYGUI
            _ui.m_IsDownload.SetSelectedIndex(1);
#endif
            var message = (AssetDownloadProgressUpdateEventArgs)gameEventArgs;
            float progress = message.CurrentDownloadSizeBytes / (message.TotalDownloadSizeBytes * 1f);
            string currentSizeMb = Utility.File.GetBytesSize(message.CurrentDownloadSizeBytes);
            string totalSizeMb = Utility.File.GetBytesSize(message.TotalDownloadSizeBytes);
            SetProgress(progress, $"{currentSizeMb}/{totalSizeMb}");
        }
    }
}
