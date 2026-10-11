using UnityEngine;

namespace WaterVision
{
    /// <summary>
    /// Single source of truth mapping an avatar index → its networked player prefab + display data.
    /// Used by BOTH the spawn hook (AvatarSpawnController, which feeds NGO's
    /// OnFetchLocalPlayerPrefabToSpawn) and the lobby UI (AvatarPickerMenu).
    ///
    /// Index 0 must be the base/default avatar (Ch08, the NetworkManager's PlayerPrefab) so the
    /// default selection reproduces the original behavior. 1..N are the variant prefabs.
    ///
    /// Lives in a Resources folder (Assets/Resources/AvatarCatalog.asset) so it can be loaded
    /// without a scene reference: Resources.Load&lt;AvatarCatalog&gt;("AvatarCatalog").
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarCatalog", menuName = "WaterVision/Avatar Catalog")]
    public class AvatarCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Shown in the picker UI.")]
            public string displayName;

            [Tooltip("The networked player prefab spawned for this choice. Index 0 = base PlayerPrefab.")]
            public GameObject prefab;

            [Tooltip("Baked preview image for the picker grid (filled by Tools ▸ WaterVision ▸ Bake Avatar Thumbnails).")]
            public Sprite thumbnail;
        }

        public Entry[] entries;

        public int Count => entries != null ? entries.Length : 0;

        public GameObject GetPrefab(int i) => InRange(i) ? entries[i].prefab : null;
        public string GetName(int i) => InRange(i) ? entries[i].displayName : string.Empty;
        public Sprite GetThumbnail(int i) => InRange(i) ? entries[i].thumbnail : null;

        bool InRange(int i) => entries != null && i >= 0 && i < entries.Length;
    }
}
