using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class BootstrapScreenView : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
        private static readonly Color PanelColor = new Color(0.075f, 0.09f, 0.115f, 0.96f);
        private static readonly Color SectionColor = new Color(0.105f, 0.125f, 0.16f, 0.98f);
        private static readonly Color AccentColor = new Color(0.92f, 0.64f, 0.28f, 1f);
        private static readonly Color PrimaryTextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        private static readonly Color SecondaryTextColor = new Color(0.66f, 0.72f, 0.78f, 1f);
        private static readonly Color PositiveTextColor = new Color(0.48f, 0.9f, 0.67f, 1f);

        private Font defaultFont;

        public void Bind(LocalClientStatus status)
        {
            if (status == null)
            {
                return;
            }

            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Rebuild(status);
        }

        private void Rebuild(LocalClientStatus status)
        {
            ClearChildren();
            ConfigureRoot();

            var shell = CreateFrame("Bootstrap Shell", transform, PanelColor);
            ConfigureStretch(shell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1480f, 820f));
            AddLayout(shell.gameObject, TextAnchor.UpperLeft, 28f, 28f, 28f, 28f, 20f);

            var header = CreateFrame("Header", shell, new Color(0.055f, 0.07f, 0.095f, 1f));
            ConfigureLayoutElement(header.gameObject, -1f, 130f, -1f);
            AddLayout(header.gameObject, TextAnchor.MiddleLeft, 24f, 22f, 24f, 18f, 8f);

            CreateText("Title", header, "TokenForge", 44, FontStyle.Bold, PrimaryTextColor, TextAnchor.MiddleLeft);
            CreateText("Client Status", header, status.ClientStatus, 22, FontStyle.Bold, PositiveTextColor, TextAnchor.MiddleLeft);
            CreateText("Initialized At", header, $"Initialized {status.InitializedAt:yyyy-MM-dd HH:mm:ss zzz}", 16, FontStyle.Normal, SecondaryTextColor, TextAnchor.MiddleLeft);

            var content = CreateFrame("Dashboard Content", shell, new Color(0f, 0f, 0f, 0f));
            ConfigureLayoutElement(content.gameObject, -1f, -1f, 1f);
            AddGrid(content.gameObject);

            foreach (var section in status.Sections)
            {
                CreateSection(content, section);
            }

            var debugPanel = CreateFrame("Local Debug Status", shell, new Color(0.05f, 0.06f, 0.08f, 1f));
            ConfigureLayoutElement(debugPanel.gameObject, -1f, 116f, -1f);
            AddLayout(debugPanel.gameObject, TextAnchor.UpperLeft, 20f, 18f, 20f, 16f, 8f);
            CreateText("Debug Title", debugPanel, "Local Debug", 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Debug Lines", debugPanel, BuildDebugText(status), 15, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);
        }

        private void ConfigureRoot()
        {
            var rootRect = (RectTransform)transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            image.color = BackgroundColor;
        }

        private void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private RectTransform CreateFrame(string objectName, Transform parent, Color color)
        {
            var frame = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(parent, false);
            frame.GetComponent<Image>().color = color;
            return frame.GetComponent<RectTransform>();
        }

        private Text CreateText(string objectName, Transform parent, string content, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = defaultFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = Mathf.Max(size + 8f, 28f);
            layoutElement.flexibleWidth = 1f;

            return text;
        }

        private void CreateSection(Transform parent, BootstrapSectionStatus section)
        {
            var card = CreateFrame(section.Title, parent, SectionColor);
            AddLayout(card.gameObject, TextAnchor.UpperLeft, 22f, 20f, 22f, 20f, 14f);

            CreateText("Section Title", card, section.Title, 24, FontStyle.Bold, PrimaryTextColor, TextAnchor.MiddleLeft);
            CreateText("Section Status", card, section.Status, 18, FontStyle.Bold, AccentColor, TextAnchor.MiddleLeft);
            CreateText("Section Detail", card, section.Detail, 16, FontStyle.Normal, SecondaryTextColor, TextAnchor.UpperLeft);
        }

        private void ConfigureStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private void AddLayout(GameObject target, TextAnchor childAlignment, float left, float top, float right, float bottom, float spacing)
        {
            var layout = target.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = childAlignment;
            layout.padding = new RectOffset(Mathf.RoundToInt(left), Mathf.RoundToInt(right), Mathf.RoundToInt(top), Mathf.RoundToInt(bottom));
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void AddGrid(GameObject target)
        {
            var grid = target.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(698f, 214f);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
        }

        private void ConfigureLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleHeight)
        {
            var layoutElement = target.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layoutElement.preferredHeight = preferredHeight;
            }

            if (flexibleHeight >= 0f)
            {
                layoutElement.flexibleHeight = flexibleHeight;
            }
        }

        private string BuildDebugText(LocalClientStatus status)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < status.DebugLines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("   |   ");
                }

                builder.Append(status.DebugLines[i]);
            }

            return builder.ToString();
        }
    }
}
