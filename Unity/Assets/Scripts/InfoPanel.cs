using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class InfoPanel : MonoBehaviour
{
    public GameObject      canvasRoot;
    public RawImage        img;
    public AspectRatioFitter fitter;     // ✦ 新增：拖 ImgHolder 的组件
    public TextMeshProUGUI titleValue, dateValue, makerValue, placeValue, museumValue, loadingHint;

    static InfoPanel inst;
    void Awake() => inst = this;

    public static void Show(ArtFrame f) => inst?.StartCoroutine(inst.ShowRoutine(f));

    IEnumerator ShowRoutine(ArtFrame f)
    {
        canvasRoot.SetActive(true);
        loadingHint.gameObject.SetActive(true);
        img.texture = null;

        titleValue.text = f.title;
        dateValue.text  = f.date;
        makerValue.text = string.IsNullOrWhiteSpace(f.maker) ? "Unknown" : f.maker;
        placeValue.text = f.place;

         /* ★★ 判断 Tag 来显示来源馆 ★★ */
        if (museumValue)
        {
            string museum = f.gameObject.CompareTag("ArtFrameHarvard")
                            ? "Harvard Art Museums"
                            : "V&A Museum";
            museumValue.text = museum;
        }

        // ☆ 1. 下载（或用缓存）
        if (f.hiTex == null)
        {
            UnityWebRequest req = UnityWebRequestTexture.GetTexture(f.hiResUrl);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                f.hiTex = DownloadHandlerTexture.GetContent(req);
        }

        // ☆ 2. 应用
        if (f.hiTex)
        {
            img.texture = f.hiTex;

            // **关键** 3 行：把真实宽高比写进 ARF
            float ratio = (float)f.hiTex.width / f.hiTex.height;
            fitter.aspectRatio = ratio;                 // 让 Height 自动 = Width / ratio
        }

        loadingHint.gameObject.SetActive(false);
    }

    public void Hide()
{
    DebugHelper.Show("Close Triggered");
    canvasRoot.SetActive(false);
}

}
