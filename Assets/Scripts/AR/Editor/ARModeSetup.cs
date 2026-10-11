#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// One-click, idempotent setup for the in-room "Enter AR Mode" button, placed in the MODEL ROOM inside the
    /// real working scene (Assets/Scenes/SampleScene.unity). The model room is the "Room" GameObject that contains
    /// the model menu station + the player's teleport target (Teleport_Target_Models), at world ~(-3.7, 2.09, 56.4),
    /// floor y≈2.09 — a separate root from "Big Room" (a different area).
    ///
    /// It (1) ensures the cloned PokeButton prefab exists, (2) adds/configures an ARModeController to hide the
    /// MODEL ROOM (the teleport target's root) on AR toggle, and (3) builds a freestanding pokable kiosk anchored
    /// to the teleport landing spot (computed live, so no hardcoded coordinates), on the model-room floor, wired to
    /// ARModeController.ToggleARMode().
    ///
    /// The kiosk is a SCENE-ROOT object (NOT a child of the model room), so it stays visible after the room hides —
    /// otherwise you couldn't press it to exit AR. The sample PokeButton is poked along -Y (pressed straight DOWN),
    /// so it sits flat (cap up) on the pedestal. Re-runnable: find-or-create the controller, destroy+rebuild the kiosk.
    ///
    /// NOTE: hiding the whole model room also hides the model menu (a child of it). If the menu must stay usable in
    /// AR, switch the controller to hide only the room's wall/floor child instead — flagged after the first run.
    /// </summary>
    public static class ARModeSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string AnchorName = "Teleport_Target_Models";   // marks the model room + the player's stand point
        const string ModelRoomFallbackName = "Room";          // used only if the anchor can't be found
        const string PokeSamplePath = "Assets/Samples/XR Interaction Toolkit/3.3.1/Hands Interaction Demo/HandsDemoSceneAssets/Prefabs/PokeButton.prefab";
        const string PokeCopyDir = "Assets/Prefabs/AR";
        const string PokeCopyPath = "Assets/Prefabs/AR/ARRoomPokeButton.prefab";
        const string KioskName = "AR Room Button";

        const float FrontOffset = 0.8f;        // metres in front of the teleport landing spot
        static readonly Vector3 FallbackPosition = new Vector3(-5f, 2.09f, 63f); // near the model station, floor y
        const float FallbackYaw = 90f;
        const float PedestalHeight = 0.9f;     // post height (m)
        const float PedestalDiameter = 0.16f;
        const float ButtonScale = 2.5f;        // ~6.5 cm native → ~16 cm poke target
        const float ButtonLift = 0.05f;        // cap sits this far above the pedestal top

        [MenuItem("WaterVision/AR/Setup AR Room Button")]
        public static void Setup()
        {
            if (!OpenTargetScene()) return;
            EnsurePokeButtonAsset();
            var scene = EditorSceneManager.GetActiveScene();

            var anchor = GameObject.Find(AnchorName);
            var modelRoom = anchor != null ? anchor.transform.root.gameObject : GameObject.Find(ModelRoomFallbackName);

            var controller = EnsureController(modelRoom);
            BuildKiosk(scene, controller, anchor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ARModeSetup] Done. '" + KioskName + "' kiosk placed in the model room (" +
                      (modelRoom != null ? modelRoom.name : "<not found>") + ") and wired to ARModeController. " +
                      "Press DOWN on the cap to toggle AR. Drag the kiosk to fine-tune.");
        }

        static bool OpenTargetScene()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            Debug.LogError("[ARModeSetup] Could not open " + ScenePath + ".");
            return false;
        }

        // Clone the sample poke button into a stable project path (idempotent).
        static void EnsurePokeButtonAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PokeCopyPath) != null) return;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(PokeSamplePath);
            if (src == null)
            {
                Debug.LogError("[ARModeSetup] Sample PokeButton not found at " + PokeSamplePath +
                               " — import the XRIT 'Hands Interaction Demo' sample (Package Manager → XR Interaction Toolkit → Samples).");
                return;
            }
            EnsureFolder(PokeCopyDir);
            if (AssetDatabase.CopyAsset(PokeSamplePath, PokeCopyPath))
                Debug.Log("[ARModeSetup] Cloned PokeButton sample → " + PokeCopyPath);
            else
                Debug.LogError("[ARModeSetup] Failed to copy PokeButton sample to " + PokeCopyPath);
            AssetDatabase.Refresh();
        }

        // Find-or-create the ARModeController and point it at the MODEL ROOM (to hide for passthrough).
        static ARModeController EnsureController(GameObject modelRoom)
        {
            var ctrl = Object.FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (ctrl == null)
            {
                var go = new GameObject("AR Mode Controller");
                ctrl = go.AddComponent<ARModeController>();
                Debug.Log("[ARModeSetup] Created 'AR Mode Controller' in " + ScenePath + ".");
            }

            var so = new SerializedObject(ctrl);
            var list = so.FindProperty("m_EnvironmentRoots");
            list.arraySize = 0;
            if (modelRoom != null)
            {
                // Hide only the backdrop children (walls/doors/posters) + TrashBin — NEVER the whole Room (menus stay).
                foreach (var n in new[] { "walls", "doors", "posters", "TrashBin" })
                {
                    var child = modelRoom.transform.Find(n);
                    if (child == null) continue;
                    int i = list.arraySize;
                    list.arraySize = i + 1;
                    list.GetArrayElementAtIndex(i).objectReferenceValue = child.gameObject;
                }
                so.FindProperty("m_EnvironmentRootName").stringValue = modelRoom.name;
                Debug.Log("[ARModeSetup] AR will hide " + list.arraySize + " backdrop children of '" + modelRoom.name + "' (menus stay).");
            }
            else
            {
                so.FindProperty("m_EnvironmentRootName").stringValue = ModelRoomFallbackName;
                Debug.LogWarning("[ARModeSetup] Model room not found via '" + AnchorName + "' or '" + ModelRoomFallbackName +
                                 "' — assign the ARModeController's hide-list manually.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void BuildKiosk(Scene scene, ARModeController controller, GameObject anchor)
        {
            var existing = GameObject.Find(KioskName);
            if (existing != null) Object.DestroyImmediate(existing); // idempotent rebuild

            var kiosk = new GameObject(KioskName);

            // Place in the model room: in front of the teleport landing spot, on its floor. Unity computes the
            // anchor's world transform, so there are no hand-entered coordinates to get wrong. Label faces the player.
            if (anchor != null)
            {
                Vector3 fwd = anchor.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 pos = anchor.transform.position + fwd * FrontOffset;
                pos.y = anchor.transform.position.y; // model-room floor height
                kiosk.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up)); // non-mirrored facing (matches MenuController); kiosk is superseded by the UI button
            }
            else
            {
                kiosk.transform.SetPositionAndRotation(FallbackPosition, Quaternion.Euler(0f, FallbackYaw, 0f));
                Debug.LogWarning("[ARModeSetup] Anchor '" + AnchorName + "' not found — used a fallback position near the model station; drag the kiosk to the right spot.");
            }

            // Pedestal (decorative; collider stripped so ONLY the button's BoxCollider is interactive).
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(kiosk.transform, false);
            // Unity's cylinder is 2 units tall at scale 1 → scale.y = height/2; base at y0, top at PedestalHeight.
            pedestal.transform.localScale = new Vector3(PedestalDiameter, PedestalHeight * 0.5f, PedestalDiameter);
            pedestal.transform.localPosition = new Vector3(0f, PedestalHeight * 0.5f, 0f);
            var pcol = pedestal.GetComponent<Collider>();
            if (pcol != null) Object.DestroyImmediate(pcol);

            // The pokable button (cloned poke prefab) — flat, cap up, pressed straight down (-Y).
            XRSimpleInteractable interactable = null;
            var pokeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PokeCopyPath);
            if (pokeAsset != null)
            {
                var poke = (GameObject)PrefabUtility.InstantiatePrefab(pokeAsset, scene);
                poke.name = "PokeButton";
                poke.transform.SetParent(kiosk.transform, false);
                poke.transform.localPosition = new Vector3(0f, PedestalHeight + ButtonLift, 0f);
                poke.transform.localRotation = Quaternion.identity; // cap up; do NOT tilt (45° poke-angle threshold)
                poke.transform.localScale = Vector3.one * ButtonScale;
                interactable = poke.GetComponentInChildren<XRSimpleInteractable>(true);
                if (interactable == null)
                    Debug.LogWarning("[ARModeSetup] Cloned poke button has no XRSimpleInteractable — the wirer will retry at runtime.");
            }
            else
            {
                Debug.LogError("[ARModeSetup] Poke button prefab missing at " + PokeCopyPath + " — kiosk built WITHOUT a button.");
            }

            // 3D label on the pedestal front (auto-sized; faces the player via the kiosk rotation).
            var labelGo = new GameObject("Label", typeof(TextMeshPro));
            var tmp = labelGo.GetComponent<TextMeshPro>();
            labelGo.transform.SetParent(kiosk.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, PedestalHeight * 0.65f, PedestalDiameter * 0.5f + 0.01f);
            labelGo.transform.localRotation = Quaternion.identity;
            tmp.text = "Enter\nAR Mode";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.32f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = 6f;

            // Wirer on the kiosk root — pre-assign refs for determinism (it also re-resolves them at runtime).
            var wirer = kiosk.AddComponent<ARModeRoomButton>();
            var so = new SerializedObject(wirer);
            if (controller != null) so.FindProperty("m_Controller").objectReferenceValue = controller;
            if (interactable != null) so.FindProperty("m_Interactable").objectReferenceValue = interactable;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(kiosk);
            Debug.Log("[ARModeSetup] Built '" + KioskName + "' at " + kiosk.transform.position + ".");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
