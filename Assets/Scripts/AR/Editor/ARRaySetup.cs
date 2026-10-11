#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// Tunes the rig's ray interactors for better REACH + target acquisition in AR (both hands + controllers):
    /// a fat SphereCast hit-detection + a longer visual line so the ray reaches small/far targets (the menus' grab
    /// handles, UI buttons). NO smoothing / reticle — keeps the ray 1:1 with the hand (per the user).
    ///
    /// Found BY NAME via reflection (no hard XRIT type/namespace dependency) so it works whether the rig uses
    /// XRRayInteractor or NearFarInteractor, and configures every match. Fields are set via SerializedObject and
    /// silently skipped if absent. Idempotent.
    /// </summary>
    public static class ARRaySetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const int SphereCast = 1;       // XRRayInteractor.HitDetectionType: Raycast=0, SphereCast=1, ConeCast=2
        const float SphereRadius = 0.2f; // fat ray → forgiving on small targets
        const float LineLength = 25f;    // longer visible line so it reaches far targets
        const float CastDistance = 25f;  // NearFar far-caster reach

        [MenuItem("WaterVision/AR/Setup AR Ray")]
        public static void Setup()
        {
            if (!OpenScene()) return;
            var scene = EditorSceneManager.GetActiveScene();
            int rays = 0, nearfar = 0, visuals = 0, casters = 0;

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var t = mb.GetType().Name;
                switch (t)
                {
                    case "XRRayInteractor":
                    {
                        var so = new SerializedObject(mb);
                        SetEnum(so, "m_HitDetectionType", SphereCast);
                        SetFloat(so, "m_SphereCastRadius", SphereRadius);
                        Apply(so, mb);
                        rays++;
                        break;
                    }
                    case "NearFarInteractor":
                    {
                        var so = new SerializedObject(mb);
                        SetBool(so, "m_EnableFarCasting", true);
                        Apply(so, mb);
                        nearfar++;
                        break;
                    }
                    case "XRInteractorLineVisual":
                    {
                        var so = new SerializedObject(mb);
                        SetBool(so, "m_OverrideInteractorLineLength", true);
                        SetFloat(so, "m_LineLength", LineLength);
                        SetBool(so, "m_SmoothMovement", false); // 1:1 responsiveness, no smoothing
                        Apply(so, mb);
                        visuals++;
                        break;
                    }
                    default:
                        // Casters used by NearFarInteractor — bump reach + radius best-effort.
                        if (t.Contains("InteractionCaster"))
                        {
                            var so = new SerializedObject(mb);
                            SetFloat(so, "m_CastDistance", CastDistance);
                            SetFloat(so, "m_CastRadius", SphereRadius);
                            SetFloat(so, "m_SphereCastRadius", SphereRadius);
                            Apply(so, mb);
                            casters++;
                        }
                        break;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARRaySetup] Configured " + rays + " XRRayInteractor (SphereCast r=" + SphereRadius + "), " +
                      nearfar + " NearFarInteractor, " + casters + " casters, " + visuals + " line visuals (len=" + LineLength + ").");
        }

        static void Apply(SerializedObject so, Object target)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetEnum(SerializedObject so, string field, int v)
        {
            var p = so.FindProperty(field);
            if (p != null) p.enumValueIndex = v;
        }

        static void SetFloat(SerializedObject so, string field, float v)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = v;
        }

        static void SetBool(SerializedObject so, string field, bool v)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = v;
        }

        static bool OpenScene()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return EditorSceneManager.GetActiveScene().path == ScenePath;
        }
    }
}
#endif
