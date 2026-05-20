using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SoftUiGraphic : MaskableGraphic
    {
        [SerializeField] public float cornerRadius = 18f;
        [SerializeField] public bool ellipse;
        [SerializeField] public bool verticalGradient;
        [SerializeField] public Color topColor = Color.white;
        [SerializeField] public Color bottomColor = Color.white;
        [SerializeField] public int segments = 10;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0.01f || rect.height <= 0.01f)
            {
                return;
            }

            var points = ellipse ? EllipsePoints(rect) : RoundedRectPoints(rect);
            AddFan(vh, rect, points);
        }

        private List<Vector2> RoundedRectPoints(Rect rect)
        {
            var radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            var count = Mathf.Max(3, segments);
            var points = new List<Vector2>(count * 4 + 4);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, count);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, count);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, count);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, count);
            return points;
        }

        private List<Vector2> EllipsePoints(Rect rect)
        {
            var count = Mathf.Max(24, segments * 4);
            var points = new List<Vector2>(count);
            var center = rect.center;
            var radiusX = rect.width * 0.5f;
            var radiusY = rect.height * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var radians = Mathf.PI * 2f * i / count;
                points.Add(new Vector2(center.x + Mathf.Cos(radians) * radiusX, center.y + Mathf.Sin(radians) * radiusY));
            }

            return points;
        }

        private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startDegrees, float endDegrees, int count)
        {
            for (var i = 0; i <= count; i++)
            {
                var degrees = Mathf.Lerp(startDegrees, endDegrees, i / (float)count);
                var radians = degrees * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + Mathf.Cos(radians) * radius, center.y + Mathf.Sin(radians) * radius));
            }
        }

        private void AddFan(VertexHelper vh, Rect rect, List<Vector2> points)
        {
            var centerIndex = 0;
            vh.AddVert(rect.center, VertexColor(rect.center.y, rect), Vector2.zero);
            for (var i = 0; i < points.Count; i++)
            {
                vh.AddVert(points[i], VertexColor(points[i].y, rect), Vector2.zero);
            }

            for (var i = 0; i < points.Count; i++)
            {
                var next = i + 1 == points.Count ? 1 : i + 2;
                vh.AddTriangle(centerIndex, i + 1, next);
            }
        }

        private Color VertexColor(float y, Rect rect)
        {
            var baseColor = verticalGradient
                ? Color.Lerp(bottomColor, topColor, Mathf.InverseLerp(rect.yMin, rect.yMax, y))
                : color;
            baseColor.a *= color.a;
            return baseColor;
        }
    }
}
