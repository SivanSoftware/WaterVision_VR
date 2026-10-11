using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click creation of the <c>ASLBubbleSettings.asset</c> at the canonical
/// Resources path so <see cref="ASLSigningCharacterManager"/> finds it via
/// <c>Resources.Load</c>.
///
/// Run: Tools → ASL → Create Bubble Settings.
/// If the asset already exists, opens it in the Inspector instead of overwriting.
/// </summary>
public static class ASL_CreateBubbleSettings
{
    const string MENU = "Tools/ASL/Create Bubble Settings";
    const string PATH = "Assets/Resources/ASL/ASLBubbleSettings.asset";

    [MenuItem(MENU)]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/ASL"))
            AssetDatabase.CreateFolder("Assets/Resources", "ASL");

        var existing = AssetDatabase.LoadAssetAtPath<ASLBubbleSettings>(PATH);
        if (existing != null)
        {
            Debug.Log($"[ASL-Settings] ASLBubbleSettings already exists at {PATH}. " +
                      "Opening in Inspector.");
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var so = ScriptableObject.CreateInstance<ASLBubbleSettings>();
        AssetDatabase.CreateAsset(so, PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = so;
        EditorGUIUtility.PingObject(so);
        Debug.Log($"[ASL-Settings] ✓ Created {PATH}. " +
                  "Edit fields in the Inspector — they apply at next Play.");
    }
}
