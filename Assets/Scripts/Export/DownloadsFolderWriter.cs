using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Cross-platform writer that puts files into the user's public Downloads folder so
/// they are easy to find from the OS's standard file manager - no special permissions,
/// no runtime popups.
///
/// Editor / Windows : %USERPROFILE%\Downloads\WaterVisionVR\&lt;fileName&gt;
/// Android / Quest  : Internal Shared Storage > Download > WaterVisionVR > &lt;fileName&gt;
///                    via the MediaStore.Downloads ContentResolver (API 29+). No
///                    WRITE_EXTERNAL_STORAGE permission required.
///
/// If the public write fails (older Android, locked-down OEM, etc.), the writer falls back
/// to Application.persistentDataPath/Exports/&lt;fileName&gt; and the returned path reflects
/// the actual location written, so the UI can show the user exactly where the file went.
/// </summary>
public static class DownloadsFolderWriter
{
    /// <summary>Sub-folder created under the platform Downloads folder.</summary>
    public const string SubFolder = "WaterVisionVR";

    /// <summary>The platform-relative Downloads root used by MediaStore on Android.</summary>
    const string AndroidRelativePath = "Download/" + SubFolder;

    /// <summary>
    /// Write a UTF-8 text file into the Downloads folder. Returns the visible path
    /// on success, or null on failure.
    /// </summary>
    public static string WriteText(string fileName, string text, string mimeType)
    {
        if (string.IsNullOrEmpty(fileName) || text == null) return null;
        return WriteBytes(fileName, Encoding.UTF8.GetBytes(text), mimeType);
    }

    /// <summary>
    /// Write a binary file into the Downloads folder. Returns the visible path on
    /// success, or null on failure.
    /// </summary>
    public static string WriteBytes(string fileName, byte[] data, string mimeType)
    {
        if (string.IsNullOrEmpty(fileName) || data == null) return null;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            string androidPath = WriteToMediaStoreDownloads(fileName, data, mimeType);
            if (!string.IsNullOrEmpty(androidPath)) return androidPath;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DownloadsFolderWriter] MediaStore write failed: " + ex.Message + ". Falling back to persistentDataPath.");
        }

        return WriteToPersistentDataFallback(fileName, data);
#else
        try
        {
            string folder = GetDesktopDownloadsFolder();
            Directory.CreateDirectory(folder);
            string fullPath = Path.Combine(folder, fileName);
            File.WriteAllBytes(fullPath, data);
            return fullPath;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DownloadsFolderWriter] Desktop write failed: " + ex.Message);
            return null;
        }
#endif
    }

    /// <summary>The visible folder we save to on the current platform - useful for UI captions.</summary>
    public static string GetVisibleFolderHint()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return AndroidRelativePath;
#else
        return GetDesktopDownloadsFolder();
#endif
    }

    static string GetDesktopDownloadsFolder()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) home = Environment.GetEnvironmentVariable("HOME") ?? ".";
        return Path.Combine(home, "Downloads", SubFolder);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static string WriteToMediaStoreDownloads(string fileName, byte[] data, string mimeType)
    {
        using (var unityPlayer  = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity     = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var resolver     = activity.Call<AndroidJavaObject>("getContentResolver"))
        using (var values       = new AndroidJavaObject("android.content.ContentValues"))
        {
            // Insert with IS_PENDING=1 so MediaStore does NOT scan / publish the file until
            // we've finished writing it. Without this, Android may scan or even truncate the
            // file mid-write (especially for non-image MIME types like model/gltf-binary),
            // producing a corrupted file that fails to parse in viewers like gltf-viewer.
            values.Call("put", "_display_name", fileName);
            if (!string.IsNullOrEmpty(mimeType)) values.Call("put", "mime_type", mimeType);
            values.Call("put", "relative_path", AndroidRelativePath);
            // Box int 1 as java.lang.Integer so ContentValues.put picks the (String, Integer) overload.
            using (var pendingOne = new AndroidJavaObject("java.lang.Integer", 1))
                values.Call("put", "is_pending", pendingOne);

            AndroidJavaObject targetUri;
            using (var downloadsCls = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
            using (var collection   = downloadsCls.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            {
                targetUri = resolver.Call<AndroidJavaObject>("insert", collection, values);
            }
            if (targetUri == null) return null;

            // Chunked write — avoids passing one huge byte[] across the JNI boundary in a single
            // call (some Android builds drop or mis-marshal large arrays). Matches the standard
            // Android InputStream→OutputStream copy idiom.
            const int CHUNK = 64 * 1024;
            int totalWritten = 0;
            using (var pfd = resolver.Call<AndroidJavaObject>("openFileDescriptor", targetUri, "w"))
            {
                if (pfd == null) return null;
                using (var fd = pfd.Call<AndroidJavaObject>("getFileDescriptor"))
                using (var fos = new AndroidJavaObject("java.io.FileOutputStream", fd))
                {
                    int offset = 0;
                    byte[] buf = new byte[System.Math.Min(CHUNK, data.Length)];
                    while (offset < data.Length)
                    {
                        int n = System.Math.Min(CHUNK, data.Length - offset);
                        if (n != buf.Length) buf = new byte[n];
                        System.Buffer.BlockCopy(data, offset, buf, 0, n);
                        fos.Call("write", buf);
                        offset += n;
                        totalWritten += n;
                    }
                    fos.Call("flush");
                    fos.Call("close");
                }
                pfd.Call("close");
            }

            // Mark IS_PENDING=0 so MediaStore publishes the finished file. Without this the file
            // stays hidden / partially-indexed.
            using (var doneValues = new AndroidJavaObject("android.content.ContentValues"))
            using (var pendingZero = new AndroidJavaObject("java.lang.Integer", 0))
            {
                doneValues.Call("put", "is_pending", pendingZero);
                resolver.Call<int>("update", targetUri, doneValues, null, null);
            }

            Debug.Log("[DownloadsFolderWriter] MediaStore write OK: " + totalWritten + " bytes → " + AndroidRelativePath + "/" + fileName);
            return AndroidRelativePath + "/" + fileName;
        }
    }

    static string WriteToPersistentDataFallback(string fileName, byte[] data)
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(folder);
            string fullPath = Path.Combine(folder, fileName);
            File.WriteAllBytes(fullPath, data);
            Debug.LogWarning("[DownloadsFolderWriter] Wrote to sandbox fallback: " + fullPath);
            return fullPath;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DownloadsFolderWriter] Sandbox fallback also failed: " + ex.Message);
            return null;
        }
    }
#endif
}
