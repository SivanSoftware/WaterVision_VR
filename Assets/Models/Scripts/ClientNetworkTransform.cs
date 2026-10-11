using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // משכנעים את ה-Netcode שהלקוח הוא זה שקובע את המיקום
    protected override bool OnIsServerAuthoritative()
    {
        return false; 
    }
}