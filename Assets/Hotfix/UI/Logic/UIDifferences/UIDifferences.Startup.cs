#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameFrameX.Startup.Application;
using UnityEngine;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        public static readonly object StartupDataMarker = new object();
        CancellationTokenSource startupDataCancellation;

        public async Task PrepareStartupDataAsync()
        {
            CancelStartupData();
            if (testing || !OnlineLevels) { InitializeTutorial(); return; }
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            startupDataCancellation = cancellation;
            string error = null;
            try
            {
                using (var cache = new DifferenceBatchCache())
                {
                    var warmup = cache.Ensure(level + 1, DifferenceBatchCache.DefaultTimeout);
                    try
                    {
                        await warmup.ToUniTask(cancellationToken: cancellation.Token).Timeout(
                            TimeSpan.FromSeconds(DifferenceBatchCache.DefaultTimeout), DelayType.Realtime);
                    }
                    finally { (warmup as IDisposable)?.Dispose(); }
                    error = cache.Error;
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) { error = exception.Message; }
            finally
            {
                cancellation.Cancel();
                if (ReferenceEquals(startupDataCancellation, cancellation)) startupDataCancellation = null;
                cancellation.Dispose();
            }
            if (!this) return;
            if (error == null) { InitializeTutorial(); return; }
            Debug.LogWarning("Startup level cache failed: " + error);
            StopTutorial();
            ShowHomeInternal();
            StartCoroutine(ShowStartupDataError());
        }

        void CancelStartupData()
        {
            var cancellation = startupDataCancellation;
            startupDataCancellation = null;
            if (cancellation != null) cancellation.Cancel();
        }

        IEnumerator ShowStartupDataError()
        {
            // Startup sets HomeReady only after this warmup returns and the launcher closes.
            var deadline = Time.realtimeSinceStartupAsDouble + DifferenceBatchCache.DefaultTimeout;
            while (!ApplicationStartupEntry.HomeReady && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (ApplicationStartupEntry.HomeReady)
                Notice("Unable to load levels. Please check your connection and try again.", 3);
        }
    }
}
#endif
