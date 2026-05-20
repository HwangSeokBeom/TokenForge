using System;
using TokenForge.Client.Domain;
using TokenForge.Client.Platform;
using UnityEngine;

namespace TokenForge.Client.UI
{
    public sealed class CompanionDesktopMovementController
    {
        private readonly IDesktopCompanionOverlayService overlayService;
        private readonly System.Random random = new System.Random(173);
        private Vector2 position = new Vector2(120f, 160f);
        private Vector2 velocity = new Vector2(38f, 0f);
        private Vector2 size = new Vector2(96f, 96f);
        private float decisionTimer;
        private float hopTimer;
        private float frameTimer;
        private bool facingLeft;
        private bool walkFrame;
        private bool tickStartedLogged;
        private bool pauseLogged;
        private float positionLogTimer;
        private float dragCooldownRemaining;

        public CompanionDesktopMovementController(IDesktopCompanionOverlayService overlayService)
        {
            this.overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
        }

        public Vector2 Position => position;

        public void SetPosition(Vector2 value)
        {
            position = value;
            ClampToVisibleBounds();
            velocity = Vector2.zero;
            dragCooldownRemaining = 1.0f;
            overlayService.SetPosition(position);
            Debug.Log("INFO [CompanionMotion] idleResumed anchor=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ")");
        }

        public void ResetPosition()
        {
            var bounds = VisibleBounds();
            position = new Vector2(bounds.xMin + bounds.width * 0.22f, bounds.yMin + bounds.height * 0.25f);
            overlayService.SetPosition(position);
        }

        public void Tick(float deltaSeconds, CompanionState companionState, DesktopCompanionSettings settings)
        {
            if (deltaSeconds <= 0f || settings == null || !settings.IsDesktopCompanionEnabled ||
                (overlayService.State != CompanionDesktopOverlayState.Active && overlayService.State != CompanionDesktopOverlayState.Fallback))
            {
                return;
            }

            companionState = CompanionProgressionRules.Normalize(companionState);
            if (!tickStartedLogged)
            {
                tickStartedLogged = true;
                Debug.Log("INFO [DesktopCompanion] idle/wander tick started");
            }

            if (overlayService.IsNativeOverlay)
            {
                var profile = CompanionVisualProfileResolver.Resolve(companionState, settings.MotionMode);
                overlayService.SetClickThrough(settings.IsClickThroughEnabled);
                overlayService.SetMotionProfile(profile);
                overlayService.SetVisualState(companionState.Stage, companionState.Archetype, profile.IdleAnimation, false);
                return;
            }

            if (overlayService.IsDragging || dragCooldownRemaining > 0f)
            {
                if (!pauseLogged)
                {
                    pauseLogged = true;
                    Debug.Log("INFO [CompanionMotion] idlePaused reason=" + (overlayService.IsDragging ? "drag" : "dragCooldown"));
                }

                dragCooldownRemaining = Mathf.Max(0f, dragCooldownRemaining - deltaSeconds);
                return;
            }

            if (pauseLogged)
            {
                pauseLogged = false;
                Debug.Log("INFO [CompanionMotion] idleResumed anchor=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ")");
            }

            var motionScale = MotionScale(settings.MotionMode);
            decisionTimer -= deltaSeconds;
            frameTimer -= deltaSeconds;
            if (decisionTimer <= 0f)
            {
                ChooseNextMotion(settings.MotionMode);
            }

            position += velocity * motionScale * deltaSeconds;
            var visualPosition = position;
            if (hopTimer > 0f)
            {
                hopTimer -= deltaSeconds;
                visualPosition.y += Mathf.Sin(Mathf.Clamp01(1f - hopTimer) * Mathf.PI) * 18f;
            }

            ClampToVisibleBounds();
            if (frameTimer <= 0f)
            {
                frameTimer = 0.28f;
                walkFrame = !walkFrame;
            }

            overlayService.SetSize(size);
            overlayService.SetPosition(visualPosition);
            overlayService.SetClickThrough(settings.IsClickThroughEnabled);
            overlayService.SetVisualState(
                companionState.Stage,
                companionState.Archetype,
                walkFrame ? CompanionAnimationState.Wander : CompanionAnimationState.Idle,
                facingLeft);
            positionLogTimer -= deltaSeconds;
            if (positionLogTimer <= 0f)
            {
                positionLogTimer = 2.0f;
                Debug.Log("INFO [DesktopCompanion] idle/wander position updated " + visualPosition.x.ToString("0.##") + "," + visualPosition.y.ToString("0.##"));
            }
        }

        private void ChooseNextMotion(CompanionDesktopMotionMode mode)
        {
            var delayMin = mode == CompanionDesktopMotionMode.Calm ? 3.5f : mode == CompanionDesktopMotionMode.Playful ? 0.8f : 1.6f;
            var delayRange = mode == CompanionDesktopMotionMode.Calm ? 3.5f : mode == CompanionDesktopMotionMode.Playful ? 1.2f : 2.1f;
            decisionTimer = delayMin + (float)random.NextDouble() * delayRange;
            var roll = random.NextDouble();
            var idleChance = mode == CompanionDesktopMotionMode.Calm ? 0.62d : mode == CompanionDesktopMotionMode.Playful ? 0.18d : 0.34d;
            if (roll < idleChance)
            {
                velocity = Vector2.zero;
                return;
            }

            var speed = mode == CompanionDesktopMotionMode.Playful ? 84f : mode == CompanionDesktopMotionMode.Calm ? 24f : 48f;
            var direction = random.NextDouble() < 0.5d ? -1f : 1f;
            velocity = new Vector2(speed * direction, 0f);
            facingLeft = direction < 0f;
            if (random.NextDouble() > (mode == CompanionDesktopMotionMode.Playful ? 0.45d : 0.72d))
            {
                hopTimer = 1f;
            }
        }

        private void ClampToVisibleBounds()
        {
            var bounds = VisibleBounds();
            var clampedX = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax - size.x);
            if (!Mathf.Approximately(clampedX, position.x))
            {
                velocity.x *= -1f;
                facingLeft = velocity.x < 0f;
            }

            position.x = clampedX;
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax - size.y);
        }

        private static Rect VisibleBounds()
        {
            var width = Mathf.Max(640, Screen.currentResolution.width > 0 ? Screen.currentResolution.width : Screen.width);
            var height = Mathf.Max(480, Screen.currentResolution.height > 0 ? Screen.currentResolution.height : Screen.height);
            const float menuBarMargin = 32f;
            const float dockMargin = 84f;
            return new Rect(0f, dockMargin, width, Mathf.Max(240f, height - menuBarMargin - dockMargin));
        }

        private static float MotionScale(CompanionDesktopMotionMode mode)
        {
            switch (mode)
            {
                case CompanionDesktopMotionMode.Calm: return 0.45f;
                case CompanionDesktopMotionMode.Playful: return 1.35f;
                default: return 1f;
            }
        }
    }
}
