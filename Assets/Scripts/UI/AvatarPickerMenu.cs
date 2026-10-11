using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

namespace WaterVision
{
    /// <summary>
    /// Lobby avatar picker. Builds a grid of buttons (one per <see cref="AvatarCatalog"/> entry) at
    /// runtime under this GameObject; clicking one sets
    /// <see cref="XRINetworkGameManager.LocalPlayerAvatarIndex"/>, which AvatarSpawnController feeds to
    /// NGO when the networked player spawns. Mirrors the subscribe / prime / unsubscribe pattern of
    /// the stock color picker (Assets/VRMPAssets/Scripts/Player/PlayerAppearanceMenu.cs).
    ///
    /// Put this on an empty UI GameObject (with a RectTransform) inside the customization panel.
    /// Each button shows the baked thumbnail if present, otherwise the display name as a fallback.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AvatarPickerMenu : MonoBehaviour
    {
        [SerializeField] AvatarCatalog m_Catalog;

        [Header("Grid")]
        [SerializeField] Vector2 m_CellSize = new Vector2(70, 70);
        [SerializeField] Vector2 m_Spacing = new Vector2(8, 8);
        [SerializeField] int m_Columns = 3;

        [Header("Selection tint")]
        [SerializeField] Color m_Normal = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color m_Selected = new Color(0.16f, 0.74f, 1f, 1f);

        readonly List<Image> m_Frames = new();
        bool m_Built;

        void Awake()
        {
            if (m_Catalog == null) m_Catalog = Resources.Load<AvatarCatalog>("AvatarCatalog");
            BuildGrid();
        }

        void OnEnable()
        {
            XRINetworkGameManager.LocalPlayerAvatarIndex.Subscribe(OnIndexChanged);
        }

        void OnDisable()
        {
            XRINetworkGameManager.LocalPlayerAvatarIndex.Unsubscribe(OnIndexChanged);
        }

        void BuildGrid()
        {
            if (m_Built || m_Catalog == null) return;
            m_Built = true;

            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null) grid = gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = m_CellSize;
            grid.spacing = m_Spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, m_Columns);
            grid.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < m_Catalog.Count; i++)
            {
                int idx = i;

                var btnGo = new GameObject($"Avatar_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(transform, false);
                var frame = btnGo.GetComponent<Image>();
                frame.color = m_Normal;
                m_Frames.Add(frame);

                var thumb = m_Catalog.GetThumbnail(i);
                if (thumb != null)
                {
                    var go = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(btnGo.transform, false);
                    var img = go.GetComponent<Image>();
                    img.sprite = thumb;
                    img.preserveAspect = true;
                    Stretch(img.rectTransform, 4f);
                }
                else
                {
                    var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(btnGo.transform, false);
                    var txt = go.GetComponent<TextMeshProUGUI>();
                    txt.text = m_Catalog.GetName(i);
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.enableAutoSizing = true;
                    txt.fontSizeMin = 8; txt.fontSizeMax = 16;
                    txt.color = Color.white;
                    Stretch(txt.rectTransform, 2f);
                }

                btnGo.GetComponent<Button>().onClick.AddListener(
                    () => XRINetworkGameManager.LocalPlayerAvatarIndex.Value = idx);
            }

            OnIndexChanged(XRINetworkGameManager.LocalPlayerAvatarIndex.Value);
        }

        static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        void OnIndexChanged(int idx)
        {
            for (int i = 0; i < m_Frames.Count; i++)
                if (m_Frames[i] != null)
                    m_Frames[i].color = (i == idx) ? m_Selected : m_Normal;
        }
    }
}
