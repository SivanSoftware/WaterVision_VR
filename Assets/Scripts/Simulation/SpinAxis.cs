using UnityEngine;

/// <summary>
/// Rotates the GameObject around a configurable local axis at a constant speed.
/// Used to animate clarifier scraper bridges, screen rakes, etc.
/// </summary>
public class SpinAxis : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [SerializeField] private Axis axis = Axis.Y;
    [SerializeField] private float degreesPerSecond = 30f;

    Vector3 GetAxis() => axis switch
    {
        Axis.X => Vector3.right,
        Axis.Y => Vector3.up,
        Axis.Z => Vector3.forward,
        _ => Vector3.up,
    };

    void Update()
    {
        transform.Rotate(GetAxis() * degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
