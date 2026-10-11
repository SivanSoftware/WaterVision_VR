using UnityEngine;

public class LocalSpin : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 50f; // Degrees per second
    
    // Renamed to avoid conflicts with Unity's built-in keywords
    [SerializeField] private GameObject targetObject; 

    void Update()
    {
        // Check to make sure you actually dragged an object into the slot
        if (targetObject != null)
        {
            // Rotates the targeted GameObject around its Y-axis smoothly over time
            targetObject.transform.Rotate(Vector3.forward * spinSpeed * Time.deltaTime);
        }
        else
        {
            // Fallback: If the slot is empty, just spin the object this script is attached to
            transform.Rotate(Vector3.forward * spinSpeed * Time.deltaTime);
        }
    }
}