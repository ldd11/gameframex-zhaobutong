#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Globalization;
using Hotfix.Manager;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        [FormerlySerializedAs("firstLevelApiUrl")]
        public string levelApiUrl = "https://jaspergame-diff.tiaoya.com/api/greyfun/levels/1";
        DifferenceRemoteLevel remoteLevel, loadingLevel;
        string loadedApiUrl;
        readonly float[] patchElapsed = new float[DifferenceRound.MaxSpots];
        const float PatchFlashDuration = 2.1f;
        UnityEngine.UI.Image[] originalPatchImages = Array.Empty<UnityEngine.UI.Image>();
        UnityEngine.UI.Image[] changedFlashImages = Array.Empty<UnityEngine.UI.Image>();
        bool OnlineLevels => !testing && !string.IsNullOrWhiteSpace(levelApiUrl);
        int LevelLimit => OnlineLevels ? int.MaxValue - 1 : levels.Length;
        bool UsingRemoteLevel => OnlineLevels && remoteLevel != null && loadedApiUrl == LevelUrl(level);
        DifferenceLevel CurrentLevel => UsingRemoteLevel ? remoteLevel.Level : levels[level];
        public bool IsLoadingLevel => loadingLevel != null || localLoading;
        int albumPage;
        UnityEngine.UI.Button albumPrevious, albumNext;

        public string LevelUrl(int index)
        {
            if (index < 0 || index >= int.MaxValue - 1) throw new ArgumentOutOfRangeException(nameof(index));
            var prefix = (levelApiUrl ?? "").TrimEnd('/');
            var slash = prefix.LastIndexOf('/');
            if (int.TryParse(prefix.Substring(slash + 1), out _)) prefix = prefix.Substring(0, Mathf.Max(0, slash));
            return prefix + "/" + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        IEnumerator LoadRemoteLevel(DifferenceRemoteLevel request, int index, bool resume)
        {
            var url = LevelUrl(index);
            yield return request.Load(url, index + 1);
            while (loadingLevel == request && Time.unscaledTime - loadingStarted < .55f) yield return null;
            if (loadingLevel != request) { request.Dispose(); yield break; }
            loadingLevel = null;
            UpdateFigmaDesign(currentPage);
            startButton.interactable = true;
            if (!OnlineLevels || url != LevelUrl(index)) { request.Dispose(); ShowHome(); yield break; }
            if (request.Error != null || request.Level == null)
            {
                Debug.LogWarning("后台关卡加载失败：" + request.Error);
                request.Dispose();
                ShowDialog("关卡加载失败", "第 " + (index + 1) + " 关暂时无法加载，请稍后重试。", "重试", () => BeginLevel(index, resume), "返回首页", ShowHome);
                yield break;
            }
            ReleaseRemoteLevel();
            remoteLevel = request; loadedApiUrl = url;
            OpenLevel(index, resume);
        }

        void CancelRemoteLoad()
        {
            if (localLoadRoutine != null) { StopCoroutine(localLoadRoutine); localLoadRoutine = null; }
            localLoading = false;
            if (loadingLevel != null) { loadingLevel.Dispose(); loadingLevel = null; }
            if (startButton) startButton.interactable = true;
            UpdateFigmaDesign(currentPage);
        }

        void ReleaseRemoteLevel()
        {
            if (remoteLevel == null) return;
            upperImage.sprite = lowerImage.sprite = null;
            foreach (var image in patchImages) image.sprite = null;
            foreach (var image in originalPatchImages) image.sprite = null;
            foreach (var image in changedFlashImages) image.sprite = null;
            for (var i = 0; i < levels.Length; i++)
                stage.Find("Album/Card/Level" + i + "/Preview").GetComponent<UnityEngine.UI.Image>().sprite = OnlineLevels ? null : levels[i].original;
            remoteLevel.Dispose(); remoteLevel = null; loadedApiUrl = null;
        }

        void EnsureSpotCapacity(int count)
        {
            var previous = differencePatches.Length;
            if (count > previous)
            {
                Array.Resize(ref differencePatches, count); Array.Resize(ref patchImages, count);
                Array.Resize(ref topRings, count); Array.Resize(ref bottomRings, count); Array.Resize(ref progressDots, count);
                for (var i = previous; i < count; i++)
                {
                    var patch = Instantiate(differencePatches[0], differencePatches[0].transform.parent);
                    patch.name = "Difference" + i; differencePatches[i] = patch;
                    patchImages[i] = patch.transform.Find("Picture").GetComponent<UnityEngine.UI.Image>();
                    topRings[i] = Instantiate(topRings[0], upperImage.transform); topRings[i].name = "Found" + i;
                    bottomRings[i] = Instantiate(bottomRings[0], lowerImage.transform); bottomRings[i].name = "Found" + i;
                    progressDots[i] = Instantiate(progressDots[0], progressDots[0].transform.parent); progressDots[i].name = "Dot" + i;
                }
            }
            var originalCount = originalPatchImages.Length;
            Array.Resize(ref originalPatchImages, differencePatches.Length);
            Array.Resize(ref changedFlashImages, differencePatches.Length);
            for (var i = originalCount; i < originalPatchImages.Length; i++)
            {
                var patch = Instantiate(differencePatches[i], upperImage.transform);
                patch.name = "OriginalDifference" + i;
                originalPatchImages[i] = patch.transform.Find("Picture").GetComponent<UnityEngine.UI.Image>();
                patch.SetActive(false);
                var changedFlash = Instantiate(differencePatches[i], lowerImage.transform);
                changedFlash.name = "ChangedFlash" + i;
                changedFlashImages[i] = changedFlash.transform.Find("Picture").GetComponent<UnityEngine.UI.Image>();
                changedFlash.SetActive(false);
            }
            var progress = (RectTransform)progressDots[0].transform.parent;
            var horizontal = progress.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            progress.anchoredPosition = new Vector2(14, -212.5f);
            var width = progress.rect.width - horizontal.padding.horizontal;
            var scale = Mathf.Min(1, width / (Mathf.Max(1, count) * 41 + Mathf.Max(0, count - 1) * 4));
            horizontal.spacing = 4 * scale;
            foreach (var dot in progressDots) dot.rectTransform.sizeDelta = new Vector2(41, 42) * scale;
        }

        UnityEngine.UI.Button AlbumPageButton(string name, string caption, float x, int step)
        {
            var label = Instantiate(At<UnityEngine.UI.Text>("Album/Card/More"), stage.Find("Album/Card"));
            label.name = name; label.text = caption; label.raycastTarget = true;
            label.rectTransform.anchoredPosition = new Vector2(x, -794);
            label.rectTransform.sizeDelta = new Vector2(160, 49);
            var button = label.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = label; button.onClick.AddListener(() => ChangeAlbumPage(step));
            return button;
        }

        void ChangeAlbumPage(int step)
        {
            albumPage = Mathf.Clamp(albumPage + step, 0, completed / levels.Length);
            RefreshAlbum();
        }

        void ConfigurePatchFlash(UnityEngine.UI.Image image, UnityEngine.UI.Image board, RectTransform crop,
            Sprite sprite, Vector2 picturePosition, Vector2 pictureSize)
        {
            var rect = (RectTransform)image.transform.parent;
            rect.SetParent(board.transform, false);
            rect.SetAsLastSibling();
            rect.SetSiblingIndex((board == upperImage ? topRings : bottomRings)[0].transform.GetSiblingIndex());
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = crop.sizeDelta;
            rect.anchoredPosition = crop.anchoredPosition + Vector2.Scale(crop.sizeDelta, new Vector2(.5f, -.5f));
            rect.localScale = Vector3.one;
            image.rectTransform.anchoredPosition = picturePosition; image.rectTransform.sizeDelta = pictureSize;
            image.sprite = sprite; image.color = Color.white;
            rect.gameObject.SetActive(false);
        }

        void ResetPatchFlash(int index)
        {
            originalPatchImages[index].color = changedFlashImages[index].color = Color.white;
            var original = originalPatchImages[index].transform.parent;
            var changed = changedFlashImages[index].transform.parent;
            original.localScale = changed.localScale = Vector3.one;
            original.gameObject.SetActive(false); changed.gameObject.SetActive(false);
        }

        // Briefly exchange the two crops in place three times, then reveal their own pictures.
        void AdvancePatches(float deltaTime)
        {
            if (Round == null || !play.activeSelf) return;
            for (var i = 0; i < Round.Total; i++)
            {
                if (patchElapsed[i] < 0 || !Round.IsFound(i)) continue;
                var elapsed = patchElapsed[i] += Mathf.Max(0, deltaTime);
                var phase = (elapsed % .7f) / .7f;
                var alpha = Mathf.Clamp01(1.4f * Mathf.Sin(Mathf.PI * phase));
                originalPatchImages[i].color = changedFlashImages[i].color = new Color(1, 1, 1, alpha);
                if (elapsed >= PatchFlashDuration)
                {
                    ResetPatchFlash(i);
                    patchElapsed[i] = -1;
                }
            }
        }
    }
}
#endif
