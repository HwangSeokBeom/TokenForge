using System;
using TokenForge.Client.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class CompanionMovementController : MonoBehaviour
    {
        [SerializeField] public RectTransform movementArea;
        [SerializeField] public RectTransform targetRoot;
        [SerializeField] public Image bodyImage;

        private readonly System.Random random = new System.Random(37);
        private float decisionTimer;
        private float phase;
        private float velocityX = 34f;
        private float hopTimer;
        private float motionScale = 1f;
        private CompanionStage stage = CompanionStage.Egg;

        public void Configure(RectTransform area, RectTransform target, Image body)
        {
            movementArea = area;
            targetRoot = target;
            bodyImage = body;
            ClampToBounds();
        }

        public void SetStage(CompanionStage value)
        {
            stage = value;
        }

        public void SetMotionReduced(bool reduced)
        {
            motionScale = reduced ? 0.25f : 1f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaSeconds)
        {
            if (movementArea == null || targetRoot == null || deltaSeconds <= 0f)
            {
                return;
            }

            decisionTimer -= deltaSeconds;
            phase += deltaSeconds * Mathf.Max(0.2f, motionScale);
            if (decisionTimer <= 0f)
            {
                ChooseNextMotion();
            }

            var anchored = targetRoot.anchoredPosition;
            anchored.x += velocityX * motionScale * deltaSeconds;
            var bob = Mathf.Sin(phase * 3.2f) * 4f;
            var hop = 0f;
            if (hopTimer > 0f)
            {
                hopTimer -= deltaSeconds;
                hop = Mathf.Sin(Mathf.Clamp01(1f - hopTimer) * Mathf.PI) * 18f;
            }

            if (stage == CompanionStage.Hatchling)
            {
                anchored.x += Mathf.Sin(phase * 18f) * 1.8f;
            }

            anchored.y = bob + hop;
            targetRoot.anchoredPosition = anchored;
            ClampToBounds();
            UpdateFacing();
        }

        public void ClampToBounds()
        {
            if (movementArea == null || targetRoot == null)
            {
                return;
            }

            var area = movementArea.rect;
            var target = targetRoot.rect;
            if (area.width <= 1f || area.height <= 1f)
            {
                return;
            }

            var halfWidth = Math.Max(8f, target.width * 0.5f);
            var halfHeight = Math.Max(8f, target.height * 0.5f);
            var minX = area.xMin + halfWidth;
            var maxX = area.xMax - halfWidth;
            var minY = area.yMin + halfHeight;
            var maxY = area.yMax - halfHeight;
            if (minX > maxX)
            {
                minX = maxX = 0f;
            }

            if (minY > maxY)
            {
                minY = maxY = 0f;
            }

            var position = targetRoot.anchoredPosition;
            var clampedX = Mathf.Clamp(position.x, minX, maxX);
            if (!Mathf.Approximately(clampedX, position.x))
            {
                velocityX *= -1f;
            }

            targetRoot.anchoredPosition = new Vector2(clampedX, Mathf.Clamp(position.y, minY, maxY));
        }

        public void SetVelocityForTest(Vector2 velocity)
        {
            velocityX = velocity.x;
        }

        private void ChooseNextMotion()
        {
            decisionTimer = 1.1f + (float)random.NextDouble() * 1.9f;
            var roll = random.NextDouble();
            if (roll < 0.28d)
            {
                velocityX = 0f;
            }
            else
            {
                var speed = 24f + (float)random.NextDouble() * 36f;
                velocityX = roll < 0.64d ? speed : -speed;
            }

            if (random.NextDouble() > 0.68d)
            {
                hopTimer = 1f;
            }
        }

        private void UpdateFacing()
        {
            if (bodyImage == null || Mathf.Approximately(velocityX, 0f))
            {
                return;
            }

            var scale = bodyImage.rectTransform.localScale;
            scale.x = velocityX < 0f ? -1f : 1f;
            bodyImage.rectTransform.localScale = scale;
        }

        private void OnRectTransformDimensionsChange()
        {
            ClampToBounds();
        }
    }
}
