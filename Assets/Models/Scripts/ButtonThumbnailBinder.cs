using UnityEngine;
using UnityEngine.UI;

public class ButtonThumbnailBinder : MonoBehaviour
{
    public int modelIndex;                 // 0..49
    public int fileIndexOffset = 1;        // כדי ש-0 -> 1
    public Image iconImage;

    public string thumbnailsResourcesFolder = "Thumbnails";
    public string prefabBaseName = "PipeSet_";

    void Awake()
    {
        if (iconImage == null)
        {
            Debug.LogWarning($"{name}: iconImage is NULL (drag the Icon Image here)");
            return;
        }

        int fileIndex = modelIndex + fileIndexOffset; // 0->1, 49->50
        string spriteName = prefabBaseName + fileIndex;

        Debug.Log($"{name}: Load -> Resources/{thumbnailsResourcesFolder}/{spriteName}.png");

        Sprite s = Resources.Load<Sprite>($"{thumbnailsResourcesFolder}/{spriteName}");
        Debug.Log($"{name}: loaded? {(s != null)}");

        if (s != null)
        {
            Debug.Log($"{name}: sprite={s.name}, tex={s.texture.name}, size={s.rect.size}");
            iconImage.sprite = s;
        }
        else
        {
            Debug.LogWarning($"{name}: Sprite not found -> Assets/Resources/{thumbnailsResourcesFolder}/{spriteName}.png");
        }

    }
}
