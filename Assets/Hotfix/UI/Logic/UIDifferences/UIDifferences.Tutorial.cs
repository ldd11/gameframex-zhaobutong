#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        // Keep saved step IDs; 2 was the removed second-difference lesson.
        enum TutorialStep { FirstDifference, Zoom, HintButton = 3, HintDifference, Complete }
        bool tutorialPending;
        TutorialStep tutorialStep;
        int tutorialTarget = -1, tutorialShown = -1;
        float tutorialShowAt;
        DifferenceHintSpotlight tutorialShade;
        RectTransform tutorialVisuals;
        readonly RectTransform[] tutorialPoints = new RectTransform[2];
        readonly DifferenceHintHand[] tutorialHands = new DifferenceHintHand[2];
        public Sprite tutorialPinchHandSprite, tutorialPinchHandOpenSprite, tutorialZoomArrowSprite;
        readonly RectTransform[] tutorialPinchHands = new RectTransform[2];
        readonly RectTransform[] tutorialZoomArrows = new RectTransform[4];
        Text tutorialMessage;
        bool TutorialRunning => tutorialPending && level == 0 && Round != null && !Round.Finished && play.activeSelf;
        public bool TutorialAllowsView => !TutorialRunning || tutorialStep == TutorialStep.Zoom;

        void InitializeTutorial()
        {
            StopTutorial();
            // Players who have already cleared a level keep their normal startup flow.
            tutorialPending = !testing && completed == 0 && !GameApp.Setting.GetBool(Key + "TutorialComplete", false);
            if (!testing && completed == 0) BeginLevel(0, true);
        }

        void ShowFirstLevelLoadError(string error)
        {
            homeDesign.SetActive(false);
            ShowDialog("关卡加载失败", error, "重试", () => BeginLevel(0, true), terminal: true);
        }

        void OpenTutorial(bool restored)
        {
            if (!tutorialPending || level != 0) return;
            tutorialStep = restored ? (TutorialStep)Mathf.Clamp(GetInt("TutorialStep", 0), 0, 4) : TutorialStep.FirstDifference;
            if ((int)tutorialStep == 2) tutorialStep = TutorialStep.HintButton;
            if (Round.Count == 0) tutorialStep = TutorialStep.FirstDifference;
            else if (tutorialStep == TutorialStep.FirstDifference) tutorialStep = TutorialStep.Zoom;
            if (Round.Complete) { FinishTutorial(); return; }
            if (tutorialStep == TutorialStep.HintDifference && pendingHint < 0) tutorialStep = TutorialStep.HintButton;
            tutorialTarget = tutorialStep == TutorialStep.HintDifference ? pendingHint : Round.Hint();
            tutorialShown = -1; tutorialShowAt = Time.unscaledTime;
            GameApp.Setting.SetInt(Key + "TutorialStep", (int)tutorialStep);
            GameApp.Setting.Save();
        }

        void SetTutorialStep(TutorialStep step)
        {
            tutorialStep = step;
            tutorialTarget = Round.Hint();
            tutorialShown = -1;
            GameApp.Setting.SetInt(Key + "TutorialStep", (int)step);
            SaveRound();
            HideTutorialVisuals();
        }

        void TutorialFound(int index)
        {
            if (!tutorialPending || level != 0) return;
            if (Round.Complete) { FinishTutorial(); return; }
            if (index != tutorialTarget) return;
            if (tutorialStep == TutorialStep.HintDifference) { FinishTutorial(); return; }
            if (tutorialStep == TutorialStep.FirstDifference) SetTutorialStep(TutorialStep.Zoom);
            tutorialShowAt = Time.unscaledTime + Mathf.Max(DifferenceFoundFlight.Duration, PatchFlashDuration);
        }

        void TutorialHintUsed(int index)
        {
            if (!TutorialRunning || tutorialStep != TutorialStep.HintButton || index < 0) return;
            SetTutorialStep(TutorialStep.HintDifference);
            tutorialTarget = index;
        }

        bool TutorialAllowsClick(float x, float y)
        {
            if (!TutorialRunning) return true;
            if (Time.unscaledTime < tutorialShowAt || tutorialStep == TutorialStep.Zoom || tutorialStep == TutorialStep.HintButton)
                return false;
            return Round.HitIndex(x, y) == tutorialTarget;
        }

        void FinishTutorial()
        {
            GameApp.Setting.SetBool(Key + "TutorialComplete", true);
            GameApp.Setting.SetInt(Key + "TutorialStep", (int)TutorialStep.Complete);
            SaveRound(); GameApp.Setting.Save();
            StopTutorial();
            Notice("引导完成！继续寻找其他不同之处。第1关提示不限次数。", 3);
        }

        void StopTutorial()
        {
            tutorialPending = false; tutorialShown = -1;
            HideTutorialVisuals();
            if (backButton) backButton.gameObject.SetActive(true);
            if (hintButton) hintButton.interactable = true;
        }

        void HideTutorialVisuals()
        {
            if (tutorialShade) tutorialShade.Cancel();
            if (tutorialVisuals) tutorialVisuals.gameObject.SetActive(false);
        }

        void AdvanceTutorial()
        {
            if (!TutorialRunning || IsLoadingLevel || AdInputBlocked || modal.activeSelf || settings.activeSelf ||
                (!testing && !GameFrameX.Startup.Application.ApplicationStartupEntry.HomeReady))
            { HideTutorialVisuals(); tutorialShown = -1; return; }
            backButton.gameObject.SetActive(false);
            resetZoomButton.gameObject.SetActive(false);
            hintButton.interactable = tutorialStep == TutorialStep.HintButton && Time.unscaledTime >= tutorialShowAt;
            hintHand.Show(false);
            noticeLabel.transform.parent.gameObject.SetActive(false);
            if (Time.unscaledTime < tutorialShowAt) return;
            if (tutorialStep == TutorialStep.Zoom && upperImage.transform.localScale.x >= 1.25f)
            {
                SetTutorialStep(TutorialStep.HintButton);
                hintButton.interactable = true;
            }
            EnsureTutorialVisuals();
            if (tutorialShown != (int)tutorialStep) ShowTutorialStep();
            var hint = tutorialStep == TutorialStep.HintButton;
            var zoom = tutorialStep == TutorialStep.Zoom;
            for (var i = 0; i < 2; i++)
            {
                tutorialPoints[i].gameObject.SetActive(!hint || i == 0);
                tutorialHands[i].Show(!zoom);
                var image = i == 0 ? upperImage.rectTransform : lowerImage.rectTransform;
                var button = (RectTransform)hintButton.transform;
                tutorialPoints[i].position = hint ? button.TransformPoint(button.rect.center) : TutorialPoint(image);
                if (zoom)
                {
                    var viewport = (RectTransform)image.parent;
                    tutorialPoints[i].position = viewport.TransformPoint(viewport.rect.center);
                }
                var cycle = Mathf.Repeat(Time.unscaledTime, 2.4f) / 2.4f;
                var opening = cycle < .5f;
                var spread = (1 - Mathf.Cos(cycle * Mathf.PI * 2)) * .5f;
                tutorialPinchHands[i].gameObject.SetActive(zoom);
                tutorialPinchHands[i].GetComponent<Image>().sprite = opening ? tutorialPinchHandOpenSprite : tutorialPinchHandSprite;
                for (var arrow = 0; arrow < 2; arrow++)
                {
                    var rect = tutorialZoomArrows[i * 2 + arrow];
                    rect.gameObject.SetActive(zoom);
                    var top = arrow == 0;
                    rect.anchoredPosition = new Vector2(-48, (top ? 1 : -1) * Mathf.Lerp(34, 78, spread));
                    rect.localRotation = Quaternion.Euler(0, 0, (top == opening ? 0 : 180) + (top ? -15 : 15));
                }
            }
            var upperView = (RectTransform)upperImage.transform.parent;
            var lowerView = (RectTransform)lowerImage.transform.parent;
            tutorialMessage.rectTransform.parent.position = (upperView.TransformPoint(new Vector2(upperView.rect.center.x, upperView.rect.yMin)) +
                lowerView.TransformPoint(new Vector2(lowerView.rect.center.x, lowerView.rect.yMax))) * .5f;
        }

        Vector3 TutorialPoint(RectTransform image)
        {
            var spot = spots[tutorialTarget];
            return image.TransformPoint(new Vector2(image.rect.xMin + spot.x * image.rect.width, image.rect.yMax - spot.y * image.rect.height));
        }

        void ShowTutorialStep()
        {
            tutorialShown = (int)tutorialStep;
            if (tutorialStep == TutorialStep.FirstDifference)
            {
                var spot = spots[tutorialTarget];
                tutorialShade.Show(upperImage.GetComponent<DifferenceBoard>(), upperImage.rectTransform, lowerImage.rectTransform,
                    new Vector2(spot.x, spot.y), tutorialTarget, false);
            }
            else if (tutorialStep == TutorialStep.HintButton)
            {
                var button = (RectTransform)hintButton.transform;
                tutorialShade.Show(upperImage.GetComponent<DifferenceBoard>(), button, button, new Vector2(.5f, .5f), -1, false);
            }
            else tutorialShade.Cancel();
            tutorialMessage.text = tutorialStep == TutorialStep.Zoom ? (Application.isMobilePlatform ? "双指向外滑动，放大图片" : "双指向外滑动，放大图片（电脑可滚动滚轮）") :
                tutorialStep == TutorialStep.HintButton ? "点击提示按钮，找到不同之处 · 本关无限提示" :
                tutorialStep == TutorialStep.HintDifference ? "点击高亮位置，找到这处不同" : "点击高亮位置，上下任意一张图都可以";
            tutorialVisuals.gameObject.SetActive(true);
            tutorialVisuals.SetAsLastSibling();
        }

        void EnsureTutorialVisuals()
        {
            if (tutorialVisuals) return;
            tutorialShade = DifferenceHintSpotlight.Create((RectTransform)play.transform, hintSpotlightMaterial);
            tutorialShade.name = "TutorialShade";
            tutorialVisuals = TutorialRect((RectTransform)play.transform, "Tutorial", Vector2.zero);
            tutorialVisuals.anchorMin = Vector2.zero; tutorialVisuals.anchorMax = Vector2.one;
            tutorialVisuals.offsetMin = tutorialVisuals.offsetMax = Vector2.zero;
            for (var i = 0; i < 2; i++)
            {
                tutorialPoints[i] = TutorialRect(tutorialVisuals, "Target" + i, Vector2.zero);
                tutorialHands[i] = DifferenceHintHand.Create(tutorialPoints[i], hintHandSprite);
                tutorialHands[i].transform.localScale = Vector3.one * .6f;
                var hand = TutorialRect(tutorialPoints[i], "PinchHand", new Vector2(160, 160));
                var handImage = hand.gameObject.AddComponent<Image>(); handImage.sprite = tutorialPinchHandSprite;
                handImage.preserveAspect = true; handImage.raycastTarget = false;
                tutorialPinchHands[i] = hand;
                for (var arrow = 0; arrow < 2; arrow++)
                {
                    var rect = TutorialRect(tutorialPoints[i], "ZoomArrow" + arrow, new Vector2(44, 60));
                    var visual = rect.gameObject.AddComponent<Image>(); visual.sprite = tutorialZoomArrowSprite;
                    visual.preserveAspect = true; visual.raycastTarget = false;
                    tutorialZoomArrows[i * 2 + arrow] = rect;
                }
            }
            var message = TutorialRect(tutorialVisuals, "Instruction", new Vector2(560, 72));
            var background = message.gameObject.AddComponent<Image>(); background.color = new Color(.95f, 1, .95f); background.raycastTarget = false;
            var border = message.gameObject.AddComponent<Outline>(); border.effectColor = new Color(.1f, .65f, .45f); border.effectDistance = new Vector2(3, -3);
            var text = TutorialRect(message, "Text", new Vector2(536, 66));
            tutorialMessage = text.gameObject.AddComponent<Text>(); tutorialMessage.font = noticeLabel.font;
            tutorialMessage.fontSize = 23; tutorialMessage.alignment = TextAnchor.MiddleCenter;
            tutorialMessage.color = new Color(.1f, .4f, .3f); tutorialMessage.raycastTarget = false;
        }

        static RectTransform TutorialRect(RectTransform parent, string name, Vector2 size)
        {
            var node = new GameObject(name, typeof(RectTransform)); node.layer = parent.gameObject.layer;
            var rect = (RectTransform)node.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.sizeDelta = size;
            return rect;
        }
    }
}
#endif
