using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Renderer))]
public class ArtFrame : MonoBehaviour, IPointerClickHandler
{
    public Renderer paintingRenderer;
    [HideInInspector] public string title, date, maker, place;
    [HideInInspector] public string hiResUrl;
    [HideInInspector] public Texture hiTex;

    void Awake()
    {
        // Automatically assign Renderer if not set manually
        if (!paintingRenderer)
            paintingRenderer = GetComponent<Renderer>();
    }


    // Apply a texture to the frame while maintaining its aspect ratio. Prevents stretching or distortion.
    public void SetTexture(Texture tex)
    {
        var mat = paintingRenderer.material;
        mat.mainTexture = tex;

        // If no texture provided (e.g., after ClearTexture or load failure), reset material tiling and offset
        if (tex == null)
        {
            mat.mainTextureScale = Vector2.one;
            mat.mainTextureOffset = Vector2.zero;
            return;
        }

        // Set wrap mode to Clamp to prevent tiling artifacts
        tex.wrapMode = TextureWrapMode.Clamp;

        // Aspect Ratio Calculation
        float texRatio = (float)tex.width / tex.height;
        float frameRatio = paintingRenderer.bounds.size.x /
                           paintingRenderer.bounds.size.y;

        Vector2 tiling = Vector2.one;
        Vector2 offset = Vector2.zero;

        // If the texture is wider than the frame
        if (texRatio > frameRatio)
        {
            float scaleY = frameRatio / texRatio;
            tiling.y = scaleY;
            offset.y = (1f - scaleY) / 2f;
        }
        else
        {
            // If the texture is taller than the frame
            float scaleX = texRatio / frameRatio;
            tiling.x = scaleX;
            offset.x = (1f - scaleX) / 2f;
        }

        mat.mainTextureScale = tiling;
        mat.mainTextureOffset = offset;
    }

    //Handle click events on the artwork. Sends data to InfoPanel for display (Weather or Explore panel).
    public void OnPointerClick(PointerEventData eventData)
    {
        DebugHelper.Show($"Clicked {title}");     // Show debug message on screen

        //First try to show the weather version of InfoPanel (if present)
        if (InfoPanelWeatherExists())
        {
            InfoPanelWeather.Show(this);      // Used in WeatherGalleryScene
        }
        else
        {
            InfoPanel.Show(this);             // Used in Global Gallery
        }
    }

    // Clear texture and release memory to avoid accumulation after repeated refreshes.
    public void ClearTexture()
    {
        if (hiTex != null)
        {
            Destroy(hiTex);       // Release texture memory
            hiTex = null;
        }

        SetTexture(null);         // Remove texture from display
        title = date = maker = place = "";
        hiResUrl = "";
    }

    // Check whether the Weather InfoPanel is present and active in the scene
    bool InfoPanelWeatherExists()
    {
        return FindObjectOfType<InfoPanelWeather>(includeInactive: false) != null;
    }


}
