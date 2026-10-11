using UnityEngine;

public class Whiteboard : MonoBehaviour
{
    public Texture2D texture;
    public Vector2 textureSize = new Vector2(2048, 2048);

    void Start()
    {

        Debug.Log("Whiteboard Script Started properly!");

        var r = GetComponent<Renderer>();
        texture = new Texture2D((int)textureSize.x, (int)textureSize.y);


        Color[] pixels = texture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();



        r.material.mainTexture = texture;


        r.material.SetTexture("_BaseMap", texture);


        r.material.SetTexture("_MainTex", texture);


        r.material.SetColor("_BaseColor", Color.white);
    }
}