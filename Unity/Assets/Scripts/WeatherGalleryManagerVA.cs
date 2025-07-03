using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class WeatherGalleryManagerVA : MonoBehaviour
{
    [Header("Dependencies")]
    public Material pictureMat;
    public TextMeshProUGUI statusText;   // 可选

    const int WANT = 10;
    const int PAGE_SIZE = 100;

    /* -------- 主入口协程 -------- */
    public IEnumerator LoadWeatherGallery(string keyword)
    {
        if (statusText) statusText.text = "V&A Loading…";

        /* 1️⃣ 先 page=1 确定总页数 */
        string baseURL = $"https://api.vam.ac.uk/v2/objects/search" +
                         $"?q={UnityWebRequest.EscapeURL(keyword)}" +
                         $"&image_exists=true&page_size={PAGE_SIZE}&responseGroup=full";

        RootVA page1 = null;
yield return StartCoroutine(YieldJson<RootVA>(baseURL + "&page=1", r => page1 = r));
if (page1 == null) yield break;

int total = page1.info.record_count;
int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PAGE_SIZE));

int rndPage = Random.Range(1, pages + 1);
RootVA data = page1;  // 默认 = page1
if (rndPage != 1)
{
    yield return StartCoroutine(YieldJson<RootVA>(baseURL + $"&page={rndPage}", r => data = r));
    if (data == null) yield break;
}


        /* 2️⃣ 过滤有图 → shuffle → 10 条 */
        var list = data.records.Where(r => !string.IsNullOrEmpty(r._primaryImageId)).ToList();
        Shuffle(list); if (list.Count > WANT) list = list.GetRange(0, WANT);

        /* 3️⃣ 直接按 Tag 找 10 个 ArtFrame */
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrame")
                             .OrderBy(g => g.name)
                             .Select(g => g.GetComponent<ArtFrame>())
                             .Take(WANT)
                             .ToArray();

        /* 4️⃣ 下载并贴图 */
        for (int i = 0; i < frames.Length && i < list.Count; i++)
        {
            string imgUrl = $"https://framemark.vam.ac.uk/collections/{list[i]._primaryImageId}/full/400,/0/default.jpg";
            yield return StartCoroutine(SetTexture(frames[i], imgUrl));

            frames[i].title =
                list[i]._primaryTitle ??
                list[i].title ??
                list[i]._primaryObjectName ??
                list[i].objectType ?? "(object)";
        }
        if (statusText) statusText.text = "Done";
    }

    /* ------------ Reload 按钮 ------------ */
    public void ReloadFromPrefs()
    {
        StopAllCoroutines();
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");
        StartCoroutine(LoadWeatherGallery(kw));
    }

    /* ------------ 工具 ------------ */
    IEnumerator YieldJson<T>(string url, System.Action<T> cb)
{
    using UnityWebRequest req = UnityWebRequest.Get(url);
    yield return req.SendWebRequest();
    if (req.result != UnityWebRequest.Result.Success)
    {
        Debug.Log(req.error);
        yield break;
    }
    cb(JsonUtility.FromJson<T>(req.downloadHandler.text));
}



    IEnumerator SetTexture(ArtFrame frame, string url)
    {
        using var r = UnityWebRequestTexture.GetTexture(url);
        yield return r.SendWebRequest();
        if (r.result != UnityWebRequest.Result.Success) yield break;

        Texture tex = DownloadHandlerTexture.GetContent(r);
        frame.paintingRenderer.sharedMaterial = new Material(pictureMat);
        frame.SetTexture(tex);
        // ✅ 同时刷新缓存
        frame.hiResUrl = url;
        frame.hiTex    = tex; 
    }

    void Shuffle<T>(IList<T> a){for(int i=a.Count-1;i>0;i--){int j=Random.Range(0,i+1); (a[i],a[j])=(a[j],a[i]);}}

    /* ---------- JSON ---------- */
    [System.Serializable] public class Info        { public int record_count; }
    [System.Serializable] public class RecordVA    {
        public string _primaryImageId,_primaryTitle,title,_primaryObjectName,objectType;
    }
    [System.Serializable] public class RootVA      { public Info info; public RecordVA[] records; }
}
