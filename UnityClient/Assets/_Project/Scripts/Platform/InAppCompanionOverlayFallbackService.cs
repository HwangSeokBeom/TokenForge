using TokenForge.Client.Domain;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TokenForge.Client.UI;

namespace TokenForge.Client.Platform
{
    public sealed class InAppCompanionOverlayFallbackService : IDesktopCompanionOverlayService
    {
        private const string LogPrefix = "[DesktopCompanion]";
        public event Action Clicked;
        public event Action DoubleClicked;
        public event Action<Vector2> DragEnded;
        public bool IsAvailable => false;
        public bool IsNativeOverlay => false;
        public bool IsAnyOverlayDragging()
        {
            return view != null && view.IsDragging;
        }

        public bool IsOverlayDragging(string repositoryId)
        {
            return IsAnyOverlayDragging();
        }
        public CompanionDesktopOverlayState State { get; private set; } = CompanionDesktopOverlayState.Fallback;
        public string StatusMessage { get; private set; } = "Editor preview mode. Native desktop overlay is available only in macOS player builds.";
        private FallbackCompanionView view;
        private Vector2 position = new Vector2(120f, 120f);
        private Vector2 size = new Vector2(84f, 84f);
        private CompanionState visualState = CompanionState.CreateDefault();
        private bool clickThrough;

        public bool Create()
        {
            State = CompanionDesktopOverlayState.Fallback;
            StatusMessage = "Editor preview mode. Native desktop overlay is unavailable in this runtime.";
            Debug.Log("INFO " + LogPrefix + " fallback companion active");
            return false;
        }

        public void Show()
        {
            State = CompanionDesktopOverlayState.Fallback;
            EnsureView();
            if (view != null)
            {
                view.SetVisible(true);
            }
            Debug.Log("INFO " + LogPrefix + " fallback companion active");
        }

        public void Hide()
        {
            State = CompanionDesktopOverlayState.Disabled;
            if (view != null)
            {
                view.SetVisible(false);
            }
        }

        public void SetPosition(Vector2 position)
        {
            this.position = ClampOrigin(position);
            if (view != null && !view.IsDragging)
            {
                view.SetPosition(this.position);
            }
        }

        public void SetSize(Vector2 size)
        {
            this.size = new Vector2(Mathf.Max(24f, size.x), Mathf.Max(24f, size.y));
            if (view != null)
            {
                view.SetSize(this.size);
                view.SetPosition(ClampOrigin(position));
            }
        }

        public void SetVisualTheme(string visualThemeId)
        {
        }

        public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
        {
            visualState.Stage = stage;
            visualState.Archetype = archetype;
            if (view != null)
            {
                view.SetVisualState(visualState, animationState, facingLeft);
            }
        }

        public void SetMotionProfile(CompanionVisualProfile profile)
        {
        }

        public void TriggerReaction(CompanionReaction reaction, string speechText)
        {
            EnsureView();
            if (view != null)
            {
                view.TriggerReaction(reaction, string.IsNullOrWhiteSpace(speechText) ? "Ready to grow!" : speechText);
            }
            Debug.Log("INFO " + LogPrefix + " fallback reaction triggered");
        }

        public void ResetPosition()
        {
            position = new Vector2(120f, 120f);
            if (view != null)
            {
                view.SetPosition(ClampOrigin(position));
            }
        }

        public void SetClickEnabled(bool enabled)
        {
            if (view != null)
            {
                view.SetInputEnabled(enabled && !clickThrough);
            }
        }

        public void SetClickThrough(bool clickThrough)
        {
            this.clickThrough = clickThrough;
            if (view != null)
            {
                view.SetInputEnabled(!clickThrough);
            }

            Debug.Log("INFO " + LogPrefix + " " + (clickThrough ? "click-through enabled" : "click-through disabled"));
        }

        public void SetCompanionFarmSnapshots(DesktopCompanionFarmState farmState)
        {
            if ((farmState?.overlays?.Length ?? 0) == 0)
            {
                Hide();
                Debug.Log("INFO [OverlayFarm][SNAPSHOT_APPLY] count=0 source=fallback");
                Debug.Log("INFO [OverlayFarm][VISIBLE_COUNT] count=0");
            }
        }

        public void ShowAllRepositoryCompanions(string source = "csharp.showAll")
        {
            Show();
        }

        public void HideAllRepositoryCompanions(string source = "csharp.hideAll")
        {
            Hide();
        }

        public void SetOverlayFrame(string repositoryId, Rect frame, string source = "csharp.setFrame")
        {
            SetPosition(new Vector2(frame.x, frame.y));
            SetSize(new Vector2(frame.width, frame.height));
        }

        public void Destroy()
        {
            State = CompanionDesktopOverlayState.Disabled;
            if (view != null)
            {
                UnityEngine.Object.Destroy(view.gameObject);
                view = null;
            }
        }

        public void RaiseDoubleClickForTest()
        {
            DoubleClicked?.Invoke();
        }

        public void RaiseDragEndedForTest(Vector2 position)
        {
            DragEnded?.Invoke(position);
        }

        private void EnsureView()
        {
            if (view != null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            var existingCanvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            GameObject root;
            if (existingCanvas != null)
            {
                root = new GameObject("TokenForge Fallback Desktop Companion", typeof(RectTransform), typeof(Image), typeof(FallbackCompanionView));
                root.transform.SetParent(existingCanvas.transform, false);
            }
            else
            {
                var canvasHost = new GameObject("TokenForge Fallback Desktop Companion Canvas", typeof(RectTransform));
                UnityEngine.Object.DontDestroyOnLoad(canvasHost);
                var canvas = canvasHost.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                canvasHost.AddComponent<GraphicRaycaster>();
                var scaler = canvasHost.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                root = new GameObject("TokenForge Fallback Desktop Companion", typeof(RectTransform), typeof(Image), typeof(FallbackCompanionView));
                root.transform.SetParent(canvasHost.transform, false);
            }

            view = root.GetComponent<FallbackCompanionView>();
            view.Initialize(
                () => Clicked?.Invoke(),
                () => DoubleClicked?.Invoke(),
                draggedPosition =>
                {
                    position = ClampOrigin(draggedPosition);
                    Debug.Log("INFO [CompanionDrag] mouseUp final=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ") callback=true");
                    DragEnded?.Invoke(position);
                });
            view.SetSize(size);
            view.SetVisualState(visualState, CompanionAnimationState.Idle, false);
            view.SetPosition(ClampOrigin(position));
            view.SetInputEnabled(!clickThrough);
        }

        private Vector2 ClampOrigin(Vector2 value)
        {
            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);
            value.x = Mathf.Clamp(value.x, 0f, Mathf.Max(0f, width - size.x));
            value.y = Mathf.Clamp(value.y, 0f, Mathf.Max(0f, height - size.y));
            return value;
        }

        private static void Stretch(RectTransform target, float inset)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = new Vector2(inset, inset);
            target.offsetMax = new Vector2(-inset, -inset);
        }

        private sealed class FallbackCompanionView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
        {
            private const float DragThreshold = 4f;
            private RectTransform rectTransform;
            private Canvas canvas;
            private Image hitTargetImage;
            private Image bodyImage;
            private Text speechText;
            private Image glowImage;
            private Action clicked;
            private Action doubleClicked;
            private Action<Vector2> dragEnded;
            private Vector2 size = new Vector2(84f, 84f);
            private Vector2 origin = new Vector2(120f, 120f);
            private Vector2 pointerStart;
            private Vector2 dragStartOrigin;
            private float reactionUntil;
            private float motionPhase;
            private bool inputEnabled = true;
            private bool pointerDown;
            private bool dragExceeded;

            public bool IsDragging => pointerDown && dragExceeded;

            public void Initialize(Action clicked, Action doubleClicked, Action<Vector2> dragEnded)
            {
                this.clicked = clicked;
                this.doubleClicked = doubleClicked;
                this.dragEnded = dragEnded;
                rectTransform = GetComponent<RectTransform>();
                canvas = GetComponentInParent<Canvas>();
                hitTargetImage = GetComponent<Image>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.zero;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (hitTargetImage != null)
                {
                    hitTargetImage.color = new Color(0f, 0f, 0f, 0f);
                    hitTargetImage.raycastTarget = true;
                }

                glowImage = new GameObject("Reaction Glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                glowImage.transform.SetParent(transform, false);
                glowImage.color = new Color(0.42f, 0.82f, 0.95f, 0.0f);
                glowImage.raycastTarget = false;
                Stretch(glowImage.rectTransform, -10f);

                bodyImage = new GameObject("Fallback Companion Visual", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bodyImage.transform.SetParent(transform, false);
                bodyImage.preserveAspect = true;
                bodyImage.raycastTarget = false;
                Stretch(bodyImage.rectTransform, 0f);

                var bubbleObject = new GameObject("Reaction Bubble", typeof(RectTransform), typeof(Image));
                bubbleObject.transform.SetParent(transform, false);
                var bubble = bubbleObject.GetComponent<Image>();
                bubble.color = new Color(1f, 0.96f, 0.82f, 0.94f);
                bubble.raycastTarget = false;
                var speechRect = bubbleObject.GetComponent<RectTransform>();
                speechRect.anchorMin = new Vector2(0.5f, 1f);
                speechRect.anchorMax = new Vector2(0.5f, 1f);
                speechRect.pivot = new Vector2(0.5f, 0f);
                speechRect.sizeDelta = new Vector2(150f, 34f);
                speechRect.anchoredPosition = new Vector2(0f, 6f);

                speechText = new GameObject("Reaction Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                speechText.transform.SetParent(bubbleObject.transform, false);
                speechText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                speechText.fontSize = 12;
                speechText.fontStyle = FontStyle.Bold;
                speechText.alignment = TextAnchor.MiddleCenter;
                speechText.color = new Color(0.12f, 0.14f, 0.18f, 1f);
                speechText.horizontalOverflow = HorizontalWrapMode.Wrap;
                speechText.verticalOverflow = VerticalWrapMode.Truncate;
                speechText.raycastTarget = false;
                Stretch(speechText.rectTransform, 4f);
                bubbleObject.SetActive(false);
            }

            public void SetVisible(bool visible)
            {
                gameObject.SetActive(visible);
            }

            public void SetInputEnabled(bool enabled)
            {
                inputEnabled = enabled;
                if (bodyImage != null)
                {
                    bodyImage.raycastTarget = false;
                }

                if (hitTargetImage != null)
                {
                    hitTargetImage.raycastTarget = enabled;
                }
            }

            public void SetSize(Vector2 value)
            {
                size = value;
                rectTransform.sizeDelta = size / CanvasScaleFactor();
            }

            public void SetPosition(Vector2 value)
            {
                origin = ClampOrigin(value);
                rectTransform.anchoredPosition = ScreenOriginToAnchoredPosition(origin);
            }

            public void SetVisualState(CompanionState state, CompanionAnimationState animationState, bool facingLeft)
            {
                state = CompanionProgressionRules.Normalize(state);
                bodyImage.sprite = CompanionPixelArtFactory.GetSprite(state, animationState == CompanionAnimationState.Wander);
                bodyImage.color = Color.white;
                var scale = bodyImage.rectTransform.localScale;
                scale.x = facingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                bodyImage.rectTransform.localScale = scale;
            }

            public void TriggerReaction(CompanionReaction reaction, string speech)
            {
                reactionUntil = Time.unscaledTime + 1.6f;
                speechText.text = speech;
                speechText.transform.parent.gameObject.SetActive(true);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (!inputEnabled)
                {
                    return;
                }

                pointerDown = true;
                dragExceeded = false;
                pointerStart = eventData.position;
                dragStartOrigin = origin;
                Debug.Log("INFO [CompanionDrag] mouseDown screen=(" + pointerStart.x.ToString("0.##") + "," + pointerStart.y.ToString("0.##") + ") window=(" + origin.x.ToString("0.##") + "," + origin.y.ToString("0.##") + ")");
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (!inputEnabled || !pointerDown)
                {
                    return;
                }

                var delta = eventData.position - pointerStart;
                if (!dragExceeded && delta.magnitude >= DragThreshold)
                {
                    dragExceeded = true;
                    Debug.Log("INFO [CompanionDrag] thresholdExceeded");
                    Debug.Log("INFO [CompanionMotion] idlePaused reason=drag");
                }

                if (dragExceeded)
                {
                    var oldOrigin = origin;
                    SetPosition(dragStartOrigin + delta);
                    Debug.Log("INFO [CompanionDrag] setFrameOrigin old=(" + oldOrigin.x.ToString("0.##") + "," + oldOrigin.y.ToString("0.##") + ") new=(" + origin.x.ToString("0.##") + "," + origin.y.ToString("0.##") + ")");
                }
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (!inputEnabled)
                {
                    pointerDown = false;
                    return;
                }

                Debug.Log("INFO [CompanionDrag] mouseUp screen=(" + eventData.position.x.ToString("0.##") + "," + eventData.position.y.ToString("0.##") + ")");
                if (dragExceeded)
                {
                    dragEnded?.Invoke(origin);
                    Debug.Log("INFO [CompanionMotion] idleResumed anchor=(" + origin.x.ToString("0.##") + "," + origin.y.ToString("0.##") + ")");
                }

                pointerDown = false;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (!inputEnabled || dragExceeded)
                {
                    return;
                }

                if (eventData.clickCount >= 2)
                {
                    Debug.Log("INFO [DesktopCompanion] double click dashboard restore requested");
                    doubleClicked?.Invoke();
                    return;
                }

                Debug.Log("INFO [DesktopCompanion] single click reaction triggered");
                clicked?.Invoke();
            }

            private void Update()
            {
                motionPhase += Time.unscaledDeltaTime;
                var reacting = Time.unscaledTime < reactionUntil;
                var bounce = reacting ? Mathf.Abs(Mathf.Sin(motionPhase * 18f)) * 12f : Mathf.Sin(motionPhase * 2.6f) * 3f;
                var shake = reacting ? Mathf.Sin(motionPhase * 38f) * 4f : 0f;
                bodyImage.rectTransform.anchoredPosition = new Vector2(shake, bounce);
                var pulse = reacting ? 0.18f + Mathf.Abs(Mathf.Sin(motionPhase * 12f)) * 0.22f : 0f;
                glowImage.color = new Color(0.42f, 0.82f, 0.95f, pulse);
                if (speechText.transform.parent.gameObject.activeSelf && Time.unscaledTime >= reactionUntil)
                {
                    speechText.transform.parent.gameObject.SetActive(false);
                }
            }

            private Vector2 ClampOrigin(Vector2 value)
            {
                value.x = Mathf.Clamp(value.x, 0f, Mathf.Max(0f, Screen.width - size.x));
                value.y = Mathf.Clamp(value.y, 0f, Mathf.Max(0f, Screen.height - size.y));
                return value;
            }

            private Vector2 ScreenOriginToAnchoredPosition(Vector2 screenOrigin)
            {
                var scale = CanvasScaleFactor();
                return screenOrigin / scale + (size / scale) * 0.5f;
            }

            private float CanvasScaleFactor()
            {
                return canvas != null && canvas.scaleFactor > 0.001f ? canvas.scaleFactor : 1f;
            }

            private static void Stretch(RectTransform target, float inset)
            {
                target.anchorMin = Vector2.zero;
                target.anchorMax = Vector2.one;
                target.offsetMin = new Vector2(inset, inset);
                target.offsetMax = new Vector2(-inset, -inset);
            }
        }
    }
}
