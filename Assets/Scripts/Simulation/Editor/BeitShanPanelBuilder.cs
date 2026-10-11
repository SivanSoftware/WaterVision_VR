using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

public static class BeitShanPanelBuilder
{
    const float PANEL_W = 2.5f;
    const float PANEL_H = 1.6f;
    const float TITLE_H = 0.35f;

    [MenuItem("MCP/Beit Shan/Build All Info Panels")]
    public static void BuildAll()
    {
        var area = GameObject.Find("BeitShanSimulationArea");
        if (area == null) { Debug.LogError("[BeitShanPanelBuilder] BeitShanSimulationArea not found."); return; }

        var manager = Object.FindFirstObjectByType<WastewaterPlantManager>();
        if (manager == null) { Debug.LogError("[BeitShanPanelBuilder] WastewaterPlantManager not found in scene."); return; }

        // Master panel at the LandingPoint
        var landing = GameObject.Find("Plant Entrance (LandingPoint)");
        Vector3 masterPos = landing != null
            ? landing.transform.position + new Vector3(0, 2.0f, 5f)
            : new Vector3(200, 2, 0);
        BuildPanel("MasterIntroPanel", area.transform, masterPos, Quaternion.Euler(0, 180, 0), isMaster: true, stationIndex: -1, manager: manager);

        // 4 station panels (stage E has no model)
        for (int i = 0; i < manager.stations.Count; i++)
        {
            var s = manager.stations[i];
            if (s.stationRoot == null) continue;

            // Position the panel ~ 8m in front of the station root, at eye height.
            // We don't know orientation, so just offset by Z+10 and Y+3 (rough).
            Vector3 rootPos = s.stationRoot.position;
            Vector3 panelPos = rootPos + new Vector3(0, 3, 10);
            BuildPanel($"StationPanel_{i}_{s.nameEnglish}", area.transform, panelPos, Quaternion.identity, isMaster: false, stationIndex: i, manager: manager);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BeitShanPanelBuilder] Built all panels.");
    }

    static GameObject BuildPanel(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, bool isMaster, int stationIndex, WastewaterPlantManager manager)
    {
        // --- root world-space Canvas ---
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var rt = (RectTransform)go.transform;
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1;

        rt.sizeDelta = new Vector2(PANEL_W * 1000f, PANEL_H * 1000f);
        rt.localScale = Vector3.one * 0.001f;
        rt.pivot = new Vector2(0.5f, 0.5f);

        // --- background ---
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRT = (RectTransform)bg.transform;
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
        bgImg.raycastTarget = false;

        // --- title ---
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(go.transform, false);
        var titleRT = (RectTransform)titleGO.transform;
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -20);
        titleRT.sizeDelta = new Vector2(-40, 400);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.fontSize = 180;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(0.85f, 0.95f, 1f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.text = isMaster ? "מט\"ש בית שאן" : manager.stations[stationIndex].nameHebrew;
        titleTMP.isRightToLeftText = true;

        // --- description ---
        var descGO = new GameObject("Description", typeof(RectTransform));
        descGO.transform.SetParent(go.transform, false);
        var descRT = (RectTransform)descGO.transform;
        descRT.anchorMin = new Vector2(0, 0);
        descRT.anchorMax = new Vector2(1, 1);
        descRT.pivot = new Vector2(0.5f, 0.5f);
        descRT.offsetMin = new Vector2(40, 200);   // leave room for button at bottom
        descRT.offsetMax = new Vector2(-40, -420); // leave room for title at top
        var descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.fontSize = 90;
        descTMP.color = Color.white;
        descTMP.alignment = TextAlignmentOptions.TopRight;
        descTMP.text = isMaster
            ? "מתקן טיפול בשפכים בקיבולת תכנונית של 18,000 מ\"ק ליממה. התהליך כולל 5 שלבים. לחץ Activate All לסיור מודרך, או התקרב לתחנה ולחץ על הלוח שלה לסיור ממוקד."
            : manager.stations[stationIndex].description;
        descTMP.isRightToLeftText = true;
        descTMP.textWrappingMode = TextWrappingModes.Normal;

        // --- Activate button ---
        var btnGO = new GameObject("ActivateButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(go.transform, false);
        var btnRT = (RectTransform)btnGO.transform;
        btnRT.anchorMin = new Vector2(0.5f, 0);
        btnRT.anchorMax = new Vector2(0.5f, 0);
        btnRT.pivot = new Vector2(0.5f, 0);
        btnRT.anchoredPosition = new Vector2(0, 30);
        btnRT.sizeDelta = new Vector2(600, 150);
        var btnImg = btnGO.GetComponent<Image>();
        btnImg.color = new Color(0.20f, 0.60f, 0.90f, 1f);

        var btnLabelGO = new GameObject("Label", typeof(RectTransform));
        btnLabelGO.transform.SetParent(btnGO.transform, false);
        var lblRT = (RectTransform)btnLabelGO.transform;
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lblTMP = btnLabelGO.AddComponent<TextMeshProUGUI>();
        lblTMP.text = isMaster ? "Activate All — סיור מלא" : "Activate — סיור בתחנה";
        lblTMP.fontSize = 80;
        lblTMP.color = Color.white;
        lblTMP.alignment = TextAlignmentOptions.Center;
        lblTMP.fontStyle = FontStyles.Bold;

        // --- StationInfoPanel script ---
        var sip = go.AddComponent<StationInfoPanel>();
        var so = new SerializedObject(sip);
        so.FindProperty("stationIndex").intValue = isMaster ? -1 : stationIndex;
        so.FindProperty("isMasterPanel").boolValue = isMaster;
        so.FindProperty("titleText").objectReferenceValue = titleTMP;
        so.FindProperty("descriptionText").objectReferenceValue = descTMP;
        so.FindProperty("activateButton").objectReferenceValue = btnGO.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        // Multiplayer sync goes through WastewaterPlantManager's NetworkVariable,
        // not through the button. No NetworkedButton needed here.

        Debug.Log($"[BeitShanPanelBuilder] Built {name}");
        return go;
    }
}
