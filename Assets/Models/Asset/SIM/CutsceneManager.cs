using System.Collections;
using UnityEngine;

public class CutsceneManager : MonoBehaviour
{
    [Header("Main Gameplay Camera")]
    [SerializeField] private Camera mainCamera; // Changed from GameObject to Camera component

    [Header("All Cutscenes in the Scene")]
    [SerializeField] private CameraMove[] cutscenes; // Make sure these objects have Camera components

    private int activeSequentialIndex = -1;

    void Start()
    {
        // CRITICAL FOR UNITY MULTI-DISPLAY: 
        // Display 2 is deactivated by default in built builds. This turns it on.
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

    /// <summary>
    /// Swaps the cutscene camera onto Display 1 without disabling the main camera.
    /// </summary>
    public void PlayCutscene(int index)
    {
        if (index < 0 || index >= cutscenes.Length)
        {
            Debug.LogWarning($"Cutscene index {index} out of bounds!");
            return;
        }

        StopAllActiveCutscenes();

        // Get the Camera component from the cutscene object
        Camera cutsceneCam = cutscenes[index].GetComponent<Camera>();
        if (cutsceneCam == null)
        {
            Debug.LogError($"GameObject {cutscenes[index].name} is missing a Camera component!");
            return;
        }

        // 1. Wake up the cutscene object
        cutscenes[index].gameObject.SetActive(true);

        // 2. Set Cutscene to show on Display 1 (Main monitor) 
        // and push Main Camera to Display 2 (or vice versa) so scripts stay alive!
        cutsceneCam.targetDisplay = 0; // 0 = Display 1
        if (mainCamera != null) mainCamera.targetDisplay = 1; // 1 = Display 2

        StartCoroutine(WaitForSingleCutscene(cutscenes[index], cutsceneCam));
    }

    public void PlayAllCutscenes()
    {
        if (cutscenes.Length == 0) return;
        StopAllActiveCutscenes();

        activeSequentialIndex = 0;
        StartCoroutine(PlaySequenceRoutine());
    }

    private IEnumerator WaitForSingleCutscene(CameraMove cutscene, Camera cutsceneCam)
    {
        yield return new WaitWhile(() => cutscene.gameObject.activeSelf);
        RestoreMainCamera(cutsceneCam);
    }

    private IEnumerator PlaySequenceRoutine()
    {
        while (activeSequentialIndex < cutscenes.Length)
        {
            CameraMove currentCutscene = cutscenes[activeSequentialIndex];
            Camera cutsceneCam = currentCutscene.GetComponent<Camera>();

            if (cutsceneCam != null)
            {
                currentCutscene.gameObject.SetActive(true);
                cutsceneCam.targetDisplay = 0;
                if (mainCamera != null) mainCamera.targetDisplay = 1;
            }

            yield return new WaitWhile(() => currentCutscene.gameObject.activeSelf);

            if (cutsceneCam != null) RestoreMainCamera(cutsceneCam);
            activeSequentialIndex++;
        }

        activeSequentialIndex = -1;
    }

    public void StopAllActiveCutscenes()
    {
        StopAllCoroutines();
        activeSequentialIndex = -1;
        
        foreach (CameraMove cutscene in cutscenes)
        {
            if (cutscene != null)
            {
                Camera c = cutscene.GetComponent<Camera>();
                if (c != null) RestoreMainCamera(c);
                cutscene.gameObject.SetActive(false);
            }
        }
    }

    private void RestoreMainCamera(Camera cutsceneCam)
    {
        // Push the cutscene camera away to Display 2
        if (cutsceneCam != null) cutsceneCam.targetDisplay = 1;

        // Bring the main gameplay camera back to Display 1
        if (mainCamera != null) mainCamera.targetDisplay = 0; 
    }
}