using UnityEngine;

/// <summary>
/// Tracks the most-recently-grabbed pipe so the AR toolbar's "Delete" can remove it without the user having to
/// drop it into a trash bin. Set by NetworkXRGrabSync on grab; read by ARUtilityToolbar.DeleteCurrentPipe().
/// Lives in the Models assembly so both the grab sync (same assembly) and the AR toolbar (Assembly-CSharp, which
/// references this) can see it. Holds the pipe ROOT GameObject; deletion is done via BroadcastMessage so no
/// type reference to NetworkSpawnableModelVisual is needed.
/// </summary>
public static class ActivePipeRegistry
{
    /// <summary>The pipe root last grabbed (cleared when that pipe despawns). Null if none.</summary>
    public static GameObject Current { get; private set; }

    public static void Set(GameObject pipeRoot) => Current = pipeRoot;

    public static void ClearIf(GameObject pipeRoot)
    {
        if (Current == pipeRoot) Current = null;
    }
}
