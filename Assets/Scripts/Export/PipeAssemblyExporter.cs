using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Export;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Exports every spawned pipe (any MonoBehaviour whose type name is
/// "NetworkSpawnableModelVisual") to a single timestamped .glb file. Triggered
/// from the Models Room ExportStation button. Platform-aware save path
/// (Documents on Windows, persistentDataPath on Android/Quest). No code
/// changes required in the spawning pipeline.
///
/// Filter strategy: we identify spawned pipes by component type-name match
/// rather than a compile-time reference, so this script lives in its own
/// asmdef (WaterVision.Export) without depending on Assembly-CSharp.
/// </summary>
public class PipeAssemblyExporter : MonoBehaviour
{
    private const string PipeComponentTypeName = "NetworkSpawnableModelVisual";

    [Tooltip("Optional TMP text used to show the export status (\"Exporting…\" / \"Exported N pipes → file.glb\" / error).")]
    public TextMeshProUGUI statusText;

    [Tooltip("Windows only: open File Explorer to the export folder after a successful export.")]
    public bool openFolderAfterExport = true;

    [Tooltip("Optional button to auto-wire to ExportNow() in Awake. If null, will look for a child named 'Export_Button'.")]
    public UnityEngine.UI.Button exportButton;

    [Tooltip("Optional spatial filter: only pipes whose position is inside this Collider's bounds will be exported. Leave NULL to export every spawned pipe in the scene (default behavior).")]
    public Collider captureVolume;

    // ---------------------------------------------------------------------------------------------
    // Direction B · Blueprint visual state (ALL optional). When these refs are unset the exporter
    // behaves exactly as before (status-text only). Assigned by ThemeB_BuildExportPanel.
    // ---------------------------------------------------------------------------------------------
    [Header("Direction B visual state (optional)")]
    public Image buttonImage;
    public TextMeshProUGUI buttonLabel;
    public Image buttonIcon;
    public Image progressBar;
    public Sprite icoDownload, icoSpinner, icoCheck, icoRetry;
    public Color colAccent      = new Color(0.176f, 0.831f, 0.749f, 1f);
    public Color colBright      = new Color(0.498f, 0.914f, 0.855f, 1f);
    public Color colOnAccent    = new Color(0.012f, 0.169f, 0.153f, 1f);
    public Color colExportingBg = new Color(0.176f, 0.831f, 0.749f, 0.16f);
    public Color colSuccessBg   = new Color(0.498f, 0.914f, 0.855f, 0.18f);
    public Color colErrorBg     = new Color(0.937f, 0.416f, 0.322f, 1f);
    public Color colOnError     = new Color(0.165f, 0.051f, 0.031f, 1f);
    public bool  autoResetToIdle = true;
    public float autoResetDelay  = 5f;

    private enum ExportVisualState { Idle, Exporting, Success, Error }
    private const string IdleHint = "Press the button to export current pipes";
    private bool _spin;

    void Awake()
    {
        if (exportButton == null)
        {
            foreach (var b in GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (b.name == "Export_Button") { exportButton = b; break; }
            }
        }
        if (exportButton != null)
        {
            exportButton.onClick.RemoveListener(ExportNow);
            exportButton.onClick.AddListener(ExportNow);
        }
        if (buttonImage != null) SetState(ExportVisualState.Idle, IdleHint); // normalize Direction-B look
    }

    // Public, parameterless, void method — UnityEvent-friendly.
    public async void ExportNow()
    {
        try
        {
            await ExportAsync();
        }
        catch (Exception ex)
        {
            string msg = $"[PipeAssemblyExporter] Export failed: {ex.Message}";
            Debug.LogError(msg);
            SetState(ExportVisualState.Error, "Export failed", ex.Message);
        }
    }

    private async Task ExportAsync()
    {
        // 1) Collect all spawned pipes via type-name filter.
        var allMonos = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int totalFound = 0;
        var pipeRoots = new System.Collections.Generic.List<GameObject>();

        // Pre-compute the set of GameObjects whose physics colliders overlap
        // the capture volume (if assigned). OverlapBox is much more forgiving
        // than bounds.Contains(center) — a pipe is captured if ANY part of its
        // collider intersects the volume, not only when its origin point is
        // strictly inside the box.
        System.Collections.Generic.HashSet<GameObject> overlapping = null;
        if (captureVolume != null)
        {
            overlapping = new System.Collections.Generic.HashSet<GameObject>();
            Bounds b = captureVolume.bounds;
            Vector3 center = b.center;
            Vector3 halfExtents = b.extents;
            // Quaternion.identity because we use the world-aligned AABB bounds.
            var hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity,
                ~0, QueryTriggerInteraction.Collide);
            foreach (var c in hits)
            {
                // Walk up the hierarchy to find the root pipe (the one carrying
                // NetworkSpawnableModelVisual). c.attachedRigidbody.gameObject is
                // also a good shortcut when the pipe has a Rigidbody.
                Transform t = c.attachedRigidbody != null
                    ? c.attachedRigidbody.transform
                    : c.transform;
                overlapping.Add(t.gameObject);
                // Also include the immediate transform's GameObject as a safety
                // net for non-rigidbody children.
                overlapping.Add(c.gameObject);
            }
        }

        foreach (var mb in allMonos)
        {
            if (mb == null) continue;
            if (mb.GetType().Name != PipeComponentTypeName) continue;
            totalFound++;

            // Spatial filter: pipe is captured iff its GameObject (root) was
            // returned by the OverlapBox query above.
            if (captureVolume != null)
            {
                if (overlapping == null || !overlapping.Contains(mb.gameObject))
                    continue;
            }
            pipeRoots.Add(mb.gameObject);
        }

        if (pipeRoots.Count == 0)
        {
            string reason = (captureVolume != null && totalFound > 0)
                ? $"No pipes inside capture volume ({totalFound} found elsewhere in scene)."
                : "No pipes to export.";
            Debug.LogWarning($"[PipeAssemblyExporter] {reason}");
            SetState(ExportVisualState.Idle, reason);
            return;
        }

        string scopeNote = (captureVolume != null && totalFound != pipeRoots.Count)
            ? $" (filtered from {totalFound} total in scene)"
            : "";
        SetState(ExportVisualState.Exporting, $"Writing .glb… ({pipeRoots.Count} pipes{scopeNote})");

        // 2) Build throw-away export root with cloned MeshFilter + MeshRenderer
        //    at the world transforms. Pure geometry — no NGO / scripts.
        var exportRoot = new GameObject("ExportRoot_TEMP");

        int meshCount = 0;
        foreach (var pipeGo in pipeRoots)
        {
            if (pipeGo == null) continue;

            var srcFilter = pipeGo.GetComponentInChildren<MeshFilter>();
            var srcRenderer = pipeGo.GetComponentInChildren<MeshRenderer>();
            if (srcFilter == null || srcRenderer == null) continue;
            if (srcFilter.sharedMesh == null) continue;

            var child = new GameObject($"Pipe_{meshCount}_{pipeGo.name}");
            child.transform.SetParent(exportRoot.transform, worldPositionStays: false);

            var srcT = (srcFilter.transform != null) ? srcFilter.transform : pipeGo.transform;
            child.transform.position = srcT.position;
            child.transform.rotation = srcT.rotation;
            child.transform.localScale = srcT.lossyScale;

            var mf = child.AddComponent<MeshFilter>();
            mf.sharedMesh = srcFilter.sharedMesh;

            var mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterials = srcRenderer.sharedMaterials;

            meshCount++;
        }

        if (meshCount == 0)
        {
            UnityEngine.Object.Destroy(exportRoot);
            Debug.LogWarning("[PipeAssemblyExporter] Found pipe components but none had usable MeshFilter/MeshRenderer geometry.");
            SetState(ExportVisualState.Idle, "No usable geometry on spawned pipes.");
            return;
        }

        // 3) Resolve filename. GLB goes through DownloadsFolderWriter so it lands in the
        //    user's public Downloads folder (Quest: Internal Shared Storage › Download ›
        //    WaterVisionVR; Windows: %USERPROFILE%\Downloads\WaterVisionVR) with no
        //    permission popups. glTFast wants a file path, so we save to a temp file and
        //    then hand the bytes to the writer.
        string filename = $"WaterVisionVR_{DateTime.Now:yyyyMMdd_HHmmss}.glb";
        string tempPath = Path.Combine(Application.temporaryCachePath, filename);
        Directory.CreateDirectory(Application.temporaryCachePath);

        // 4) Run glTFast export. CRITICAL: Format=Binary writes a single .glb
        //    with textures + meshes embedded (default is Json which writes a
        //    .glb file containing JSON + side-car .bin and image files —
        //    incompatible with most online glTF viewers).
        var exportSettings = new ExportSettings
        {
            Format = GltfFormat.Binary,
            FileConflictResolution = FileConflictResolution.Overwrite,
            ImageDestination = ImageDestination.MainBuffer,
        };
        var export = new GameObjectExport(exportSettings);
        export.AddScene(new[] { exportRoot });
        bool ok = await export.SaveToFileAndDispose(tempPath);

        // 5) Cleanup throw-away root regardless of success.
        if (exportRoot != null)
        {
            UnityEngine.Object.Destroy(exportRoot);
        }

        if (!ok)
        {
            Debug.LogError($"[PipeAssemblyExporter] glTFast SaveToFileAndDispose returned false (path: {tempPath})");
            SetState(ExportVisualState.Error, "Export failed", "see console");
            return;
        }

        // 6) Move the GLB into the public Downloads folder via the cross-platform writer.
        byte[] glbBytes;
        try { glbBytes = File.ReadAllBytes(tempPath); }
        catch (Exception readEx)
        {
            Debug.LogError($"[PipeAssemblyExporter] Could not read temp GLB: {readEx.Message}");
            SetState(ExportVisualState.Error, "Export failed", "see console");
            return;
        }

        string visiblePath = DownloadsFolderWriter.WriteBytes(filename, glbBytes, "model/gltf-binary");
        try { File.Delete(tempPath); } catch { /* non-fatal */ }

        if (string.IsNullOrEmpty(visiblePath))
        {
            Debug.LogError("[PipeAssemblyExporter] DownloadsFolderWriter returned null — export did not land anywhere.");
            SetState(ExportVisualState.Error, "Export failed", "could not write to Downloads");
            return;
        }

        Debug.Log($"[PipeAssemblyExporter] Exported {meshCount} pipes → {visiblePath}");
        SetState(ExportVisualState.Success, "Saved to Downloads", filename);

        // 7) Windows only: open File Explorer for convenience.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (openFolderAfterExport)
        {
            try
            {
                string folderToOpen = Path.GetDirectoryName(visiblePath);
                if (!string.IsNullOrEmpty(folderToOpen))
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{folderToOpen}\"");
            }
            catch (Exception openEx)
            {
                Debug.LogWarning($"[PipeAssemblyExporter] Could not open folder: {openEx.Message}");
            }
        }
#endif
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    // Drives the Direction-B visuals (button colour/label/icon, progress bar, status text). Every
    // write is null-guarded, so with the optional refs unset this just updates the status text.
    private void SetState(ExportVisualState s, string mainStatus, string fileLine = null)
    {
        SetStatus(fileLine != null ? $"{mainStatus}\n{fileLine}" : mainStatus);

        if (buttonImage != null)
            buttonImage.color = s == ExportVisualState.Exporting ? colExportingBg
                              : s == ExportVisualState.Success   ? colSuccessBg
                              : s == ExportVisualState.Error     ? colErrorBg
                              :                                    colAccent;

        if (buttonLabel != null)
        {
            buttonLabel.text = s == ExportVisualState.Exporting ? "Exporting…"
                             : s == ExportVisualState.Success   ? "Done"
                             : s == ExportVisualState.Error     ? "Try again"
                             :                                    "Export 3D";
            buttonLabel.color = s == ExportVisualState.Error ? colOnError
                              : s == ExportVisualState.Idle  ? colOnAccent
                              :                                colBright;
        }

        if (buttonIcon != null)
        {
            Sprite sp = s == ExportVisualState.Exporting ? icoSpinner
                      : s == ExportVisualState.Success   ? icoCheck
                      : s == ExportVisualState.Error     ? icoRetry
                      :                                    icoDownload;
            if (sp != null) buttonIcon.sprite = sp;
            buttonIcon.color = s == ExportVisualState.Error ? colOnError
                             : s == ExportVisualState.Idle  ? colOnAccent
                             :                                colBright;
            if (s != ExportVisualState.Exporting) buttonIcon.rectTransform.localRotation = Quaternion.identity;
        }

        if (exportButton != null) exportButton.interactable = s != ExportVisualState.Exporting;
        if (progressBar != null) progressBar.gameObject.SetActive(s == ExportVisualState.Exporting);
        _spin = s == ExportVisualState.Exporting;

        CancelInvoke(nameof(AutoResetToIdle));
        if (autoResetToIdle && (s == ExportVisualState.Success || s == ExportVisualState.Error))
            Invoke(nameof(AutoResetToIdle), autoResetDelay);
    }

    private void AutoResetToIdle() => SetState(ExportVisualState.Idle, IdleHint);

    void Update()
    {
        if (!_spin) return;
        if (buttonIcon != null) buttonIcon.rectTransform.Rotate(0f, 0f, -240f * Time.deltaTime);
        if (progressBar != null) progressBar.fillAmount = Mathf.PingPong(Time.time * 1.1f, 1f);
    }
}
