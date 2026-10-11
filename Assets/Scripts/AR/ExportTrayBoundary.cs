using UnityEngine;

namespace WaterVision.AR
{
    /// <summary>
    /// Draws a cyan wireframe box around the ExportTray's BoxCollider trigger volume so the user can see where to
    /// place pipes for export. Visible only in AR (subscribes to ARModeController.ARModeChanged). Geometry is built
    /// once in Awake → zero per-frame cost. Lives as a child of ExportTray; added by the AR setup tool, which also
    /// adds the LineRenderer. Self-resolves the tray collider + controller if not wired.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ExportTrayBoundary : MonoBehaviour
    {
        [Tooltip("The ExportTray trigger volume. Resolved from the parent if left null.")]
        [SerializeField] private BoxCollider m_Volume;
        [Tooltip("For AR-only visibility. Resolved from the scene if left null.")]
        [SerializeField] private ARModeController m_Controller;
        [SerializeField] private Color m_Color = new Color(0.1f, 0.9f, 1f, 1f);
        [SerializeField] private float m_Width = 0.02f;

        private LineRenderer m_Line;

        private void Awake()
        {
            m_Line = GetComponent<LineRenderer>();
            if (m_Volume == null) m_Volume = GetComponentInParent<BoxCollider>();
            BuildBox();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (m_Controller == null) m_Controller = FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (m_Controller != null)
            {
                m_Controller.ARModeChanged += SetVisible;
                SetVisible(m_Controller.IsARModeActive);
            }
        }

        private void OnDisable()
        {
            if (m_Controller != null) m_Controller.ARModeChanged -= SetVisible;
        }

        private void SetVisible(bool on)
        {
            if (m_Line != null) m_Line.enabled = on;
        }

        private void BuildBox()
        {
            if (m_Line == null || m_Volume == null) return;

            // Drawn in LOCAL space as a child of ExportTray (tray scale = 1), so the collider's local center/size map
            // 1:1 onto our local space.
            Vector3 c = m_Volume.center;
            Vector3 e = m_Volume.size * 0.5f;

            var k = new Vector3[8];
            k[0] = c + new Vector3(-e.x, -e.y, -e.z);
            k[1] = c + new Vector3( e.x, -e.y, -e.z);
            k[2] = c + new Vector3( e.x, -e.y,  e.z);
            k[3] = c + new Vector3(-e.x, -e.y,  e.z);
            k[4] = c + new Vector3(-e.x,  e.y, -e.z);
            k[5] = c + new Vector3( e.x,  e.y, -e.z);
            k[6] = c + new Vector3( e.x,  e.y,  e.z);
            k[7] = c + new Vector3(-e.x,  e.y,  e.z);

            // Single strip tracing all 12 edges (4 edges retraced; harmless overdraw): bottom loop, up, top loop,
            // then the 3 remaining verticals via short backtracks.
            var path = new[]
            {
                k[0], k[1], k[2], k[3], k[0], // bottom
                k[4], k[5], k[6], k[7], k[4], // up + top
                k[5], k[1],                   // vertical 1-5
                k[2], k[6],                   // vertical 2-6
                k[7], k[3],                   // vertical 3-7
            };

            m_Line.useWorldSpace = false;
            m_Line.loop = false;
            m_Line.positionCount = path.Length;
            m_Line.SetPositions(path);
            m_Line.widthMultiplier = m_Width;
            m_Line.numCornerVertices = 0;
            m_Line.numCapVertices = 0;
            m_Line.alignment = LineAlignment.View;
            m_Line.textureMode = LineTextureMode.Stretch;

            if (m_Line.sharedMaterial == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (sh != null) m_Line.material = new Material(sh);
            }
            m_Line.startColor = m_Line.endColor = m_Color;
            if (m_Line.material != null) m_Line.material.color = m_Color;
        }
    }
}
