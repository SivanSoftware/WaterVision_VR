using System.Collections;
using UnityEngine;

/// <summary>
/// Captures the screen and saves a PNG to the device's public Downloads folder
/// (Quest: Internal Shared Storage > Download > WaterVisionVR; Windows: %USERPROFILE%\Downloads\WaterVisionVR)
/// via DownloadsFolderWriter — the same path the model/analytics exports use, so the user
/// can actually find the screenshot on the headset.
/// </summary>
public class ScreenshotHandler : MonoBehaviour
{
    // Button / UnityEvent friendly instance method (kept for any existing wiring).
    public void TakePicture() => CaptureToDownloads();

    /// <summary>
    /// Captures the screen and writes it to the public Downloads folder. Safe to call from a
    /// menu that closes itself the same frame — the capture runs on a throwaway
    /// DontDestroyOnLoad runner that survives the caller being deactivated.
    /// </summary>
    public static void CaptureToDownloads()
    {
        var go = new GameObject("~ScreenshotRunner");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotRunner>().Begin();
    }
}

/// <summary>Internal one-shot coroutine host for ScreenshotHandler.CaptureToDownloads().</summary>
public class ScreenshotRunner : MonoBehaviour
{
    public void Begin() => StartCoroutine(Run());

    private IEnumerator Run()
    {
        // Must wait for end of frame before grabbing the framebuffer (and so any menu that
        // closed this frame is gone from the shot).
        yield return new WaitForEndOfFrame();

        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] png = (tex != null) ? tex.EncodeToPNG() : null;
        if (tex != null) Object.Destroy(tex);

        if (png != null && png.Length > 0)
        {
            string filename = "Screenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
            string path = DownloadsFolderWriter.WriteBytes(filename, png, "image/png");
            Debug.Log("[ScreenshotHandler] Screenshot saved: " + (path ?? "FAILED"));
        }
        else
        {
            Debug.LogError("[ScreenshotHandler] Screenshot capture returned no data.");
        }

        Object.Destroy(gameObject);
    }
}
