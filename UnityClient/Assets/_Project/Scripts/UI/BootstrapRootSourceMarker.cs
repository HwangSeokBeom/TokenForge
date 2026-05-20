using UnityEngine;

namespace TokenForge.Client.UI
{
    public sealed class BootstrapRootSourceMarker : MonoBehaviour
    {
        public const string CurrentUiVersion = "product-game-ui-v3";
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        [SerializeField] private string source = "prefab";
        [SerializeField] private string prefabPath = PrefabPath;
        [SerializeField] private string uiVersion = CurrentUiVersion;

        public string Source => source;
        public string PrefabPathValue => prefabPath;
        public string UiVersion => uiVersion;

        public void SetSource(string sourceValue, string prefabPathValue, string uiVersionValue)
        {
            source = string.IsNullOrWhiteSpace(sourceValue) ? "prefab" : sourceValue;
            prefabPath = string.IsNullOrWhiteSpace(prefabPathValue) ? PrefabPath : prefabPathValue;
            uiVersion = string.IsNullOrWhiteSpace(uiVersionValue) ? CurrentUiVersion : uiVersionValue;
        }
    }
}
