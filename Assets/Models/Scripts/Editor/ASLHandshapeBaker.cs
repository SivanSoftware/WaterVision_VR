using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bakes ASL manual-alphabet handshape sprites by posing the XR-Hands RightHand model's finger bones
/// per a data table, rendering each to a transparent PNG (URP render-request, like ThemeC_RebakeThumbnails).
/// Output: Assets/Resources/ASL/A.png .. Z.png (+ _rest).
///
/// Workflow (the rig isn't ASL-authored, so this is iterative):
///   1. Tools/ASL/Dump Hand Bones        — list the skeleton.
///   2. Tools/ASL/Bake Calibration       — render rest from 4 dirs + curl tests around X/Y/Z, to pick
///                                          CAM_DIR (palm toward camera) and FLEX_AXIS/SPREAD_AXIS.
///   3. Tools/ASL/Bake Handshape Sprites  — render all 26 letters from the Shapes table; tune + re-bake.
/// </summary>
public static class ASLHandshapeBaker
{
    const string HAND_FBX = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/RightHand.fbx";
    const string OUT   = "Assets/Resources/ASL";
    const string CALIB = "Assets/Resources/ASL/_calib";
    const int    SIZE  = 256;

    // --- empirically-tuned conventions (adjust after calibration) ---
    static bool    PALM_SIDE  = true;                     // true = view the palm; false = back of hand
    static Vector3 ROOT_EULER = new Vector3(180f, 0f, 0f); // (calibration only)
    static Vector3 CAM_DIR    = new Vector3(0f, 0f, 1f);  // (calibration only)
    static Vector3 FLEX_AXIS  = new Vector3(1f, 0f, 0f);  // finger flexion (curl) local axis
    static Vector3 SPREAD_AXIS= new Vector3(0f, 1f, 0f);  // finger abduction (spread) local axis
    static float   FLEX_SIGN  = 1f;

    static readonly string[] Fingers = { "Index", "Middle", "Ring", "Little" };

    // ============================================================ bone rig
    class Rig
    {
        public Dictionary<string, Transform> bones = new();
        public Dictionary<Transform, Quaternion> rest = new();
        public Transform root;
        public Renderer renderer;

        public Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;
        public void Reset() { foreach (var kv in rest) kv.Key.localRotation = kv.Value; }
    }

    static Rig BuildRig()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HAND_FBX);
        var inst = (GameObject)Object.Instantiate(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        inst.transform.SetPositionAndRotation(new Vector3(0, 5000, 0), Quaternion.identity);
        var rig = new Rig { root = inst.transform };
        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
        {
            rig.bones[t.name] = t;
            rig.rest[t] = t.localRotation;
        }
        rig.renderer = inst.GetComponentInChildren<Renderer>(true);
        // CRITICAL: in edit-mode off-screen rendering the skin won't deform on bone rotation unless forced.
        if (rig.renderer is SkinnedMeshRenderer smr)
        {
            smr.updateWhenOffscreen = true;
            smr.forceMatrixRecalculationPerRender = true;
        }
        return rig;
    }

    // ============================================================ posing
    // curl 0..1 → flexion angle (deg). spread in deg (+ = splay out).
    static void Curl(Rig r, string finger, float prox, float inter, float dist, float spread = 0f)
    {
        SetFlex(r, $"R_{finger}Proximal", prox, spread);
        SetFlex(r, $"R_{finger}Intermediate", inter, 0f);
        SetFlex(r, $"R_{finger}Distal", dist, 0f);
    }

    static void SetFlex(Rig r, string bone, float curl01, float spreadDeg)
    {
        var t = r.B(bone); if (t == null) return;
        float ang = curl01 * 90f * FLEX_SIGN;
        Quaternion q = Quaternion.AngleAxis(ang, FLEX_AXIS) * Quaternion.AngleAxis(spreadDeg, SPREAD_AXIS);
        t.localRotation = r.rest[t] * q;
    }

    static void Thumb(Rig r, Vector3 metaEuler, float prox, float dist)
    {
        var m = r.B("R_ThumbMetacarpal"); if (m != null) m.localRotation = r.rest[m] * Quaternion.Euler(metaEuler);
        SetFlex(r, "R_ThumbProximal", prox, 0f);
        SetFlex(r, "R_ThumbDistal", dist, 0f);
    }

    // ============================================================ render
    static void Render(Rig r, Camera cam, RenderTexture rt, string path)
    {
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }

        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static (Camera, GameObject, GameObject) MakeStudio()
    {
        var camGO = new GameObject("__ASLCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.fieldOfView = 30f; cam.enabled = false; cam.allowHDR = false;
        var lightGO = new GameObject("__ASLLight") { hideFlags = HideFlags.HideAndDontSave };
        var li = lightGO.AddComponent<Light>(); li.type = LightType.Directional; li.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
        return (cam, camGO, lightGO);
    }

    // Deterministic palm-on view derived from the hand's own bones (independent of import orientation):
    // up = wrist→middle finger, palm-normal = up × (index→little). Fingers point up; palm faces camera.
    static void AimFromBones(Camera cam, Rig rig, bool palmSide, float margin)
    {
        Vector3 wrist = rig.B("R_Wrist").position;
        Vector3 mid   = rig.B("R_MiddleProximal").position;
        Vector3 idx   = rig.B("R_IndexProximal").position;
        Vector3 lit   = rig.B("R_LittleProximal").position;
        Vector3 up     = (mid - wrist).normalized;
        Vector3 across = (idx - lit).normalized;
        Vector3 normal = Vector3.Cross(up, across).normalized;
        if (!palmSide) normal = -normal;

        Bounds b = rig.renderer.bounds;
        Vector3 target = mid + up * (b.extents.magnitude * 0.25f); // look at finger region, skip the wrist
        float radius = b.extents.magnitude * margin;
        float dist = radius / Mathf.Sin(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
        cam.transform.position = target + normal * dist;
        cam.transform.LookAt(target, up);
        cam.nearClipPlane = 0.001f;
        cam.farClipPlane = dist + radius * 6f;
    }

    static void AimCamera(Camera cam, Bounds b, Vector3 dir, float margin = 1.15f, Vector3 lookOffset = default)
    {
        float radius = b.extents.magnitude * margin;
        float dist = radius / Mathf.Sin(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
        Vector3 target = b.center + lookOffset;
        cam.transform.position = target + dir.normalized * dist;
        cam.transform.LookAt(target);
        cam.nearClipPlane = Mathf.Max(0.001f, dist - radius * 3f);
        cam.farClipPlane = dist + radius * 6f;
    }

    // ============================================================ calibration
    [MenuItem("Tools/ASL/Bake Calibration")]
    public static void BakeCalibration()
    {
        Directory.CreateDirectory(CALIB);
        var rig = BuildRig();
        var (cam, camGO, lightGO) = MakeStudio();
        var rt = new RenderTexture(SIZE, SIZE, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        System.Action orient = () => rig.root.localRotation = Quaternion.Euler(ROOT_EULER);
        try
        {
            // rest pose (oriented) from front (palm) + side
            rig.Reset(); orient();
            AimCamera(cam, rig.renderer.bounds, new Vector3(0, 0, 1)); Render(rig, cam, rt, $"{CALIB}/rest_front.png");
            AimCamera(cam, rig.renderer.bounds, new Vector3(1, 0, 0)); Render(rig, cam, rt, $"{CALIB}/rest_side.png");

            // curl ONLY the index finger 80° around local X / Y / Z, viewed from the side (flexion visible) + front
            var axes = new (string, Vector3)[] { ("X", Vector3.right), ("Y", Vector3.up), ("Z", Vector3.forward) };
            foreach (var (n, ax) in axes)
            {
                rig.Reset(); orient();
                foreach (var lvl in new[] { "Proximal", "Intermediate", "Distal" })
                {
                    var t = rig.B($"R_Index{lvl}"); if (t) t.localRotation = rig.rest[t] * Quaternion.AngleAxis(80f, ax);
                }
                AimCamera(cam, rig.renderer.bounds, new Vector3(1, 0, 0)); Render(rig, cam, rt, $"{CALIB}/idx{n}_side.png");
                AimCamera(cam, rig.renderer.bounds, new Vector3(0, 0, 1)); Render(rig, cam, rt, $"{CALIB}/idx{n}_front.png");
            }
        }
        finally
        {
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO); Object.DestroyImmediate(rig.root.gameObject);
        }
        AssetDatabase.Refresh();
        Debug.Log($"[ASL] Calibration baked → {CALIB} (rest_*, curl_*)");
    }

    // ============================================================ full bake
    // curl 0..1 per finger (0=straight, 1=fist); thumb mode 0=across-palm, 1=side, 2=out.
    struct HS { public char c; public float i, m, r, l; public int thumb;
                public HS(char c, float i, float m, float r, float l, int t) { this.c=c; this.i=i; this.m=m; this.r=r; this.l=l; thumb=t; } }

    static readonly HS[] Shapes = {
        new('A',1,1,1,1,1), new('B',0,0,0,0,0), new('C',.5f,.5f,.5f,.5f,0), new('D',0,1,1,1,0),
        new('E',.85f,.85f,.85f,.85f,0), new('F',.7f,0,0,0,0), new('G',0,1,1,1,2), new('H',0,0,1,1,0),
        new('I',1,1,1,0,0), new('J',1,1,1,0,0), new('K',0,0,1,1,2), new('L',0,1,1,1,2),
        new('M',1,1,1,1,0), new('N',1,1,1,1,0), new('O',.6f,.6f,.6f,.6f,0), new('P',0,0,1,1,2),
        new('Q',0,1,1,1,2), new('R',0,0,1,1,0), new('S',1,1,1,1,0), new('T',1,1,1,1,0),
        new('U',0,0,1,1,0), new('V',0,0,1,1,0), new('W',0,0,0,1,0), new('X',.5f,1,1,1,0),
        new('Y',1,1,1,0,2), new('Z',0,1,1,1,0),
    };

    // Splays a finger sideways at its base (post-applied after curl). + and - separate adjacent fingers.
    static void SpreadFinger(Rig r, string finger, float deg)
    {
        var t = r.B($"R_{finger}Proximal"); if (t == null) return;
        t.localRotation = t.localRotation * Quaternion.AngleAxis(deg, SPREAD_AXIS);
    }

    static void ApplySpread(Rig r, char c)
    {
        switch (c)
        {
            case 'V': case 'K': case 'R': SpreadFinger(r, "Index", 16f); SpreadFinger(r, "Middle", -16f); break;
            case 'W': SpreadFinger(r, "Index", 20f); SpreadFinger(r, "Ring", -20f); break;
        }
    }

    static void ApplyThumb(Rig r, int mode)
    {
        switch (mode)
        {
            case 0: Thumb(r, new Vector3(0, 0, -50f), .4f, .3f); break; // across the palm
            case 2: Thumb(r, new Vector3(0, 0, 45f), 0f, 0f); break;    // out (L/Y/G)
            default: Thumb(r, Vector3.zero, 0f, 0f); break;             // side (rest)
        }
    }

    [MenuItem("Tools/ASL/Bake Handshape Sprites")]
    public static void Bake()
    {
        Directory.CreateDirectory(OUT);
        var rig = BuildRig();
        var (cam, camGO, lightGO) = MakeStudio();
        var rt = new RenderTexture(SIZE, SIZE, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        var written = new List<string>();
        try
        {
            // fixed palm-on camera from the open-hand bones, reused for every letter
            rig.Reset();
            AimFromBones(cam, rig, PALM_SIDE, 0.72f);

            // rest / neutral open hand (word gaps)
            rig.Reset(); ApplyThumb(rig, 1);
            Render(rig, cam, rt, $"{OUT}/_rest.png"); written.Add($"{OUT}/_rest.png");

            foreach (var s in Shapes)
            {
                rig.Reset();
                Curl(rig, "Index",  s.i, s.i, s.i);
                Curl(rig, "Middle", s.m, s.m, s.m);
                Curl(rig, "Ring",   s.r, s.r, s.r);
                Curl(rig, "Little", s.l, s.l, s.l);
                ApplySpread(rig, s.c);
                ApplyThumb(rig, s.thumb);
                string p = $"{OUT}/{s.c}.png";
                Render(rig, cam, rt, p); written.Add(p);
            }
        }
        finally
        {
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO); Object.DestroyImmediate(rig.root.gameObject);
        }
        AssetDatabase.Refresh();
        foreach (var p in written)
            if (AssetImporter.GetAtPath(p) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false; ti.SaveAndReimport();
            }
        Debug.Log($"[ASL] Baked {written.Count} handshape sprites → {OUT}");
    }
}
