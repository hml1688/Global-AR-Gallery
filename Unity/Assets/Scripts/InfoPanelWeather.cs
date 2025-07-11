using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// WeatherGalleryScene 专用信息面板：
/// 仅显示作品图片、标题与来源博物馆
/// </summary>
public class InfoPanelWeather : MonoBehaviour
{
    [Header("UI References")]
    public GameObject      canvasRoot;     // 整个面板的根，用于显隐
    public RawImage        img;            // 图片
    public AspectRatioFitter fitter;       // 让图片自适应宽高比
    public TextMeshProUGUI titleValue;     // 作品标题
    public TextMeshProUGUI museumValue;    // 来源博物馆
    public TextMeshProUGUI loadingHint;    // “Loading…” 提示（可选）

    /* -------- 单例存储 -------- */
    static InfoPanelWeather inst;
    void Awake() => inst = this;

    /* -------- 对外调用接口 -------- */
    public static void Show(ArtFrame f)
    {
        if (inst != null) inst.StartCoroutine(inst.ShowRoutine(f));
    }

    /* -------- 显示协程 -------- */
    IEnumerator ShowRoutine(ArtFrame f)
    {
        canvasRoot.SetActive(true);
        if (loadingHint) loadingHint.gameObject.SetActive(true);
        img.texture = null;

        /* 标题与来源 */
        titleValue.text  = f.title;
        museumValue.text = f.gameObject.CompareTag("ArtFrameHarvard")
                           ? "Harvard Art Museums"
                           : "V&A Museum";

        /* ▲ 若后期还有其他馆，可再加 Tag 判断 */

        /* —— 下载高清纹理（或使用缓存） —— */
        if (f.hiTex == null)
        {
            using UnityWebRequest req = UnityWebRequestTexture.GetTexture(f.hiResUrl);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                f.hiTex = DownloadHandlerTexture.GetContent(req);
        }

        /* —— 应用纹理 + 适配宽高比 —— */
        if (f.hiTex)
        {
            img.texture = f.hiTex;
            fitter.aspectRatio = (float)f.hiTex.width / f.hiTex.height;
        }

        if (loadingHint) loadingHint.gameObject.SetActive(false);
    }

    /* -------- 关闭按钮回调 -------- */
    public void Hide() => canvasRoot.SetActive(false);
}
