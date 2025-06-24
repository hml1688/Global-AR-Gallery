using UnityEngine;
using TMPro;   
using UnityEngine.EventSystems;

[RequireComponent(typeof(Renderer))]
public class ArtFrame : MonoBehaviour, IPointerClickHandler
{
    public Renderer paintingRenderer;           // 留空则取自身
    [HideInInspector] public string title, date, maker, place;
    [HideInInspector] public string hiResUrl;
    [HideInInspector] public Texture hiTex;

    void Awake()
{
    if (!paintingRenderer)
        paintingRenderer = GetComponent<Renderer>();

    // ✅ 正确写法是等贴图加载之后再设置 wrapMode（在 SetTexture 里设置）
}


    /// <summary>贴图：保持比例，不拉伸；用材质 Tiling / Offset 实现信箱（黑边）效果</summary>
    public void SetTexture(Texture tex)
{
    var mat = paintingRenderer.material;        // 实例材质
    mat.mainTexture = tex;

    // ✅ 修正：设置的是贴图的 wrapMode
    tex.wrapMode = TextureWrapMode.Clamp;

    // ---- 计算宽高比 ----
    float texRatio   = (float)tex.width  / tex.height;
    float frameRatio = paintingRenderer.bounds.size.x /
                       paintingRenderer.bounds.size.y;

    Vector2 tiling  = Vector2.one;
    Vector2 offset  = Vector2.zero;

    if (texRatio > frameRatio)
    {
        float scaleY = frameRatio / texRatio;
        tiling.y = scaleY;
        offset.y = (1f - scaleY) / 2f;
    }
    else
    {
        float scaleX = texRatio / frameRatio;
        tiling.x = scaleX;
        offset.x = (1f - scaleX) / 2f;
    }

    mat.mainTextureScale  = tiling;
    mat.mainTextureOffset = offset;
}

    /* ---------- 点击事件：把自己送去 InfoPanel ---------- */
    public void OnPointerClick(PointerEventData eventData)
    {
        DebugHelper.Show($"Clicked {title}");     // ✅ 屏幕显示文字
        InfoPanel.Show(this);                     // ✅ 弹出信息面板
    }

}
