using UnityEditor;
using UnityEngine;

public static class BeitShanImporter
{
    [MenuItem("MCP/Beit Shan/Import Package")]
    public static void ImportBeitShanPackage()
    {
        string pkgPath = @"C:\Users\sivan\Downloads\Beit Shan_1-0-0.unitypackage";
        if (!System.IO.File.Exists(pkgPath))
        {
            Debug.LogError("[BeitShanImporter] Package not found: " + pkgPath);
            return;
        }
        Debug.Log("[BeitShanImporter] Importing: " + pkgPath);
        AssetDatabase.ImportPackage(pkgPath, false);
    }
}
