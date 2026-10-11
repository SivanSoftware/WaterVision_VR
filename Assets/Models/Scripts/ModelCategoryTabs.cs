using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Filters the ModelSelectionPanel's pipe buttons by category. Tab buttons
/// in the panel hook their UnityEvents to ShowAll() / ShowCategory(int).
/// </summary>
public class ModelCategoryTabs : MonoBehaviour
{
    [Tooltip("Root of the pipe buttons (the Content GameObject of the ScrollRect).")]
    public Transform buttonsRoot;

    [Tooltip("Optional tab buttons to highlight when active.")]
    public Button[] tabButtons;

    [Tooltip("Color tint applied to the active tab.")]
    public Color activeTabColor = new Color(0.40f, 0.55f, 0.78f, 1f);

    [Tooltip("Color tint applied to inactive tabs.")]
    public Color inactiveTabColor = new Color(0.65f, 0.72f, 0.83f, 1f);

    private ModelButtonHandler[] _buttons;
    private int _currentCategory = 0;

    void Awake()
    {
        if (buttonsRoot == null)
        {
            // Try auto-find: look for a "Content" GameObject under the panel.
            var t = transform.parent;
            if (t != null)
            {
                var content = t.GetComponentsInChildren<Transform>(true);
                foreach (var c in content)
                    if (c.name == "Content") { buttonsRoot = c; break; }
            }
        }
        if (buttonsRoot != null)
            _buttons = buttonsRoot.GetComponentsInChildren<ModelButtonHandler>(true);
        else
            Debug.LogWarning("[ModelCategoryTabs] buttonsRoot is null and no Content found.");

        // Auto-wire child Tab_* button onClicks by name (no Inspector wiring needed).
        WireTabButtonsByName();
    }

    private void WireTabButtonsByName()
    {
        var childButtons = GetComponentsInChildren<Button>(true);
        // Also collect them into the tabButtons array (in name order) for highlight.
        var ordered = new System.Collections.Generic.List<Button>();
        foreach (var b in childButtons)
        {
            int cat = NameToCategory(b.name);
            if (cat < 0) continue;
            int catCaptured = cat;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => ShowCategory(catCaptured));
            // Ensure list is sized to hold this index.
            while (ordered.Count <= cat) ordered.Add(null);
            ordered[cat] = b;
        }
        if (tabButtons == null || tabButtons.Length == 0)
            tabButtons = ordered.ToArray();
    }

    private static int NameToCategory(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        if (name == "Tab_All")        return 0;
        if (name == "Tab_Pipes")      return 1;
        if (name == "Tab_Junctions")  return 2;
        if (name == "Tab_Multi")      return 3;
        if (name == "Tab_Equipment")  return 4;
        return -1;
    }

    void Start()
    {
        // Default to "All" on open.
        ShowCategory(0);
    }

    // Convenience wrappers so each tab button can hook a unique 0-arg method.
    public void ShowAll()        { ShowCategory(0); }
    public void ShowStraight()   { ShowCategory(1); }
    public void ShowJunctions()  { ShowCategory(2); }
    public void ShowMulti()      { ShowCategory(3); }
    public void ShowEquipment()  { ShowCategory(4); }

    /// <summary>
    /// 0 = All (show every button regardless of categoryIndex).
    /// 1+ = show only buttons whose categoryIndex matches.
    /// </summary>
    public void ShowCategory(int categoryIndex)
    {
        _currentCategory = categoryIndex;

        if (_buttons == null && buttonsRoot != null)
            _buttons = buttonsRoot.GetComponentsInChildren<ModelButtonHandler>(true);

        if (_buttons != null)
        {
            foreach (var b in _buttons)
            {
                if (b == null) continue;
                int cat = (b.categoryIndex > 0) ? b.categoryIndex : GetAutoCategory(b.modelIndex);
                bool show = (categoryIndex == 0) || (cat == categoryIndex);
                if (b.gameObject.activeSelf != show)
                    b.gameObject.SetActive(show);
            }
        }

        UpdateTabHighlights();
    }

    /// <summary>
    /// Maps a 0-based modelIndex (PipeSet_N where N = modelIndex+1) to a category.
    /// 1 = Straight & Bends, 2 = Junctions, 3 = Multi-pipe, 4 = Equipment.
    /// Override per-button by setting categoryIndex &gt; 0 in the Inspector.
    /// </summary>
    public static int GetAutoCategory(int modelIndex)
    {
        int pipeId = modelIndex + 1;
        switch (pipeId)
        {
            // Straight & Bends
            case 1: case 2: case 3: case 4: case 5:
            case 7: case 8: case 10: case 11: case 12: case 13:
            case 17: case 24: case 25: case 28: case 29: case 30: case 31:
            case 48: case 49: case 50:
                return 1;

            // Junctions / Forks
            case 9: case 14: case 18: case 19: case 20: case 21: case 23: case 27:
                return 2;

            // Multi-pipe / Arrays
            case 6: case 15: case 16: case 22: case 26:
                return 3;

            // Equipment & Valves (32-47)
            case 32: case 33: case 34: case 35: case 36: case 37: case 38:
            case 39: case 40: case 41: case 42: case 43: case 44: case 45:
            case 46: case 47:
                return 4;

            default:
                return 1; // Fallback bucket
        }
    }

    private void UpdateTabHighlights()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            var btn = tabButtons[i];
            if (btn == null) continue;
            var img = btn.GetComponent<Image>();
            if (img == null) continue;
            img.color = (i == _currentCategory) ? activeTabColor : inactiveTabColor;
        }
    }
}
