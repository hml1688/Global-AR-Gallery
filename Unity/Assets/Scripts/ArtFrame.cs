using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ArtFrame : MonoBehaviour
{
    public Renderer paintingRenderer;   // 留空自动取自身
    [HideInInspector] public string hiResUrl; // 供点击时使用

    void Awake()
    {
        if (!paintingRenderer)
            paintingRenderer = GetComponent<Renderer>();
    }

    public void SetTexture(Texture tex) =>
        paintingRenderer.material.mainTexture = tex;

    // ★ 可选：点画放大
    void OnMouseDown()
    {
        if (string.IsNullOrEmpty(hiResUrl)) return;
        Application.OpenURL(hiResUrl);  // 手机直接调系统浏览器
    }
}
