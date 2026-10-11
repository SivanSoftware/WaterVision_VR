using UnityEngine;
using System.Linq;

public class Marker : MonoBehaviour
{
    [SerializeField] private Transform _tip;
    [SerializeField] private int _penSize = 20;
    [SerializeField] private Color _penColor = Color.blue;

    private Whiteboard _whiteboard;
    private WhiteboardSync _whiteboardSync;
    private RaycastHit _touch;
    private float _tipHeight;
    private bool _loggedSyncStatus = false;

    void Start()
    {
        _tipHeight = 0.01f;
    }

    void Update()
    {
        if (Physics.Raycast(_tip.position, _tip.up, out _touch, _tipHeight))
        {
            if (_touch.collider.CompareTag("Whiteboard"))
            {
                if (_whiteboard == null) _whiteboard = _touch.collider.GetComponent<Whiteboard>();
                if (_whiteboardSync == null) _whiteboardSync = _touch.collider.GetComponent<WhiteboardSync>();

                if (!_loggedSyncStatus)
                {
                    Debug.Log($"[Marker] First whiteboard hit on '{_touch.collider.name}'. Whiteboard={(_whiteboard != null)} WhiteboardSync={(_whiteboardSync != null)}");
                    _loggedSyncStatus = true;
                }

                Vector2 pos = _touch.textureCoord;
                int x = (int)(pos.x * _whiteboard.textureSize.x - (_penSize / 2));
                int y = (int)(pos.y * _whiteboard.textureSize.y - (_penSize / 2));

                if (y < 0 || y > _whiteboard.textureSize.y || x < 0 || x > _whiteboard.textureSize.x) return;

                if (_whiteboardSync != null)
                {
                    _whiteboardSync.DrawStroke(x, y, _penSize, _penColor);
                }
                else
                {
                    Debug.LogWarning("[Marker] No WhiteboardSync on whiteboard — drawing local-only.");
                    var colors = Enumerable.Repeat(_penColor, _penSize * _penSize).ToArray();
                    _whiteboard.texture.SetPixels(x, y, _penSize, _penSize, colors);
                    _whiteboard.texture.Apply();
                }
            }
        }
    }
}