// Debug-only Gizmos for the wrist menu button. Draws a wireframe cube at the button's
// world position whenever the GameObject is selected in the Scene view (OnDrawGizmosSelected)
// or always (OnDrawGizmos, toggleable). Lets us visually verify in the Editor where the
// button lands relative to the avatar's hand mesh — essential for tuning watchLocalPosition
// without having to put on the headset every iteration.
//
// Attach to the spawned WristMenuButton GameObject (root) or to its WristMenuController owner.
// Gizmos only draw in the Editor; this script has zero runtime cost in a build.

using UnityEngine;

[ExecuteAlways]
public class WristButtonGizmo : MonoBehaviour
{
    [SerializeField, Tooltip("Always draw gizmos (vs only when selected). Useful when iterating wrist " +
                              "placement without selecting the button each time.")]
    bool m_AlwaysDraw = true;

    [SerializeField, Tooltip("Color of the wireframe box drawn at the button position.")]
    Color m_WireColor = new Color(0.2f, 1f, 0.4f, 0.9f);

    [SerializeField, Tooltip("Size of the wireframe cube — should match the BoxCollider on the button.")]
    Vector3 m_BoxSize = new Vector3(0.02f, 0.02f, 0.01f);

    [SerializeField, Tooltip("Also draw a line from world origin to the button — helps find off-screen buttons.")]
    bool m_DrawOriginLine = false;

    void OnDrawGizmos()
    {
        if (m_AlwaysDraw) DrawGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (!m_AlwaysDraw) DrawGizmo();
    }

    void DrawGizmo()
    {
        Gizmos.color = m_WireColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, m_BoxSize);

        // Draw a small forward-axis arrow so we know which way is the front face.
        Gizmos.color = new Color(m_WireColor.r, m_WireColor.g, m_WireColor.b, 0.5f);
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * (m_BoxSize.z * 1.5f));

        Gizmos.matrix = Matrix4x4.identity;
        if (m_DrawOriginLine)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(Vector3.zero, transform.position);
        }
    }
}
