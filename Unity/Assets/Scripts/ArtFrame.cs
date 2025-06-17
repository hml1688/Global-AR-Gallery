using UnityEngine;

public class ArtFrame : MonoBehaviour
{
    public Renderer paintingRenderer;

    void Awake()
    {
        // If no renderer is specified, the system will automatically search for itself.
        if (!paintingRenderer)
            paintingRenderer = GetComponent<Renderer>();
    }

    public void SetTexture(Texture tex)
    {
        if (paintingRenderer && paintingRenderer.material)
            paintingRenderer.material.mainTexture = tex;
    }
}
