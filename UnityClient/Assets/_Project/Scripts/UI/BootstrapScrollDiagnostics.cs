using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIEventSystem = UnityEngine.EventSystems.EventSystem;
using UIIScrollHandler = UnityEngine.EventSystems.IScrollHandler;
using UIPointerEventData = UnityEngine.EventSystems.PointerEventData;
using UIRaycastResult = UnityEngine.EventSystems.RaycastResult;
using UIScrollRect = UnityEngine.UI.ScrollRect;

namespace TokenForge.Client.UI
{
    [RequireComponent(typeof(UIScrollRect))]
    public sealed class BootstrapScrollDiagnostics : MonoBehaviour, UIIScrollHandler
    {
        private UIScrollRect scrollRect;

        private void Awake()
        {
            scrollRect = GetComponent<UIScrollRect>();
        }

        public void OnScroll(UIPointerEventData eventData)
        {
#if DEBUG || UNITY_EDITOR
            if (scrollRect == null)
            {
                scrollRect = GetComponent<UIScrollRect>();
            }

            var viewport = scrollRect != null ? scrollRect.viewport : null;
            var content = scrollRect != null ? scrollRect.content : null;
            var viewportHeight = viewport != null ? viewport.rect.height : 0f;
            var contentHeight = content != null ? LayoutUtility.GetPreferredHeight(content) : 0f;
            if (content != null)
            {
                contentHeight = Mathf.Max(contentHeight, content.rect.height);
            }

            Debug.Log("INFO [TokenForgeScroll] delta=" + eventData.scrollDelta
                + " normalized=" + (scrollRect != null ? scrollRect.normalizedPosition.ToString("F3") : "<missing>")
                + " viewportHeight=" + viewportHeight.ToString("0.##")
                + " contentHeight=" + contentHeight.ToString("0.##")
                + " contentOverflow=" + (contentHeight > viewportHeight + 0.5f)
                + " topRaycastTarget=" + FindTopRaycastTarget(eventData));
#endif
        }

#if DEBUG || UNITY_EDITOR
        private static string FindTopRaycastTarget(UIPointerEventData eventData)
        {
            if (UIEventSystem.current == null || eventData == null)
            {
                return "<none>";
            }

            var results = new List<UIRaycastResult>();
            UIEventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0 && results[0].gameObject != null
                ? results[0].gameObject.name
                : "<none>";
        }
#endif
    }
}
