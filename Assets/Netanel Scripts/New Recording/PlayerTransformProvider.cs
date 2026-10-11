using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

namespace WaterVision.Recording
{
    /// <summary>
    /// Resolves the head / leftHand / rightHand transforms and identity (name, color)
    /// for a networked player, regardless of whether the runtime player is the
    /// RPM variant or the stock VR Multiplayer Template variant.
    /// </summary>
    public static class PlayerTransformProvider
    {
        public struct PlayerHandles
        {
            public Transform head;
            public Transform leftHand;
            public Transform rightHand;
            public string displayName;
            public Color color;
        }

        /// <summary>
        /// Try to extract pose transforms and identity from a player's NetworkObject.
        /// Returns false if the player component or its transforms aren't ready yet
        /// (callers should skip this participant for the current frame and retry).
        /// </summary>
        public static bool TryGetHandles(NetworkObject playerObj, out PlayerHandles handles)
        {
            handles = default;
            if (playerObj == null) return false;

            // RPM variant is the canonical runtime player in this project.
            if (playerObj.TryGetComponent(out XRINetworkPlayer_RPM rpm))
            {
                if (rpm.head == null || rpm.leftHand == null || rpm.rightHand == null) return false;
                handles.head = rpm.head;
                handles.leftHand = rpm.leftHand;
                handles.rightHand = rpm.rightHand;
                handles.displayName = string.IsNullOrEmpty(rpm.playerName) ? playerObj.name : rpm.playerName;
                handles.color = rpm.playerColor;
                return true;
            }

            // Fallback to the stock template player.
            if (playerObj.TryGetComponent(out XRINetworkPlayer xri))
            {
                if (xri.head == null || xri.leftHand == null || xri.rightHand == null) return false;
                handles.head = xri.head;
                handles.leftHand = xri.leftHand;
                handles.rightHand = xri.rightHand;
                handles.displayName = string.IsNullOrEmpty(xri.playerName) ? playerObj.name : xri.playerName;
                handles.color = xri.playerColor;
                return true;
            }

            return false;
        }
    }
}
