using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;


public class WeatherGalleryManagerHarvard : MonoBehaviour
{
    [Header("Dependencies")]
    public Material pictureMat;
    public TextMeshProUGUI statusText;
    [SerializeField] string apiKey = "d54e083e-a267-40e4-8d55-f1259589be3b";

    const int WANT = 10, PAGE_SIZE = 100;

    public IEnumerator LoadWeatherGallery(string kw)
    {
        if (statusText) statusText.text = "Harvard Loading…";


        const string FIELDS = "primaryimageurl,secureimageurl,title";
        string baseURL = $"https://api.harvardartmuseums.org/object" +
                 $"?apikey={apiKey}&size={PAGE_SIZE}&hasimage=1" +
                 $"&q=title:{UnityWebRequest.EscapeURL(kw)}&fields={FIELDS}";


        RootHAM first = null;
        yield return StartCoroutine(GetJson<RootHAM>(baseURL+"&page=1", r=>first=r));
        if (first == null) yield break;

        int pages = Mathf.Max(1, first.info.pages);
        int rnd   = Random.Range(1, pages+1);

        RootHAM data = first;
        if (rnd != 1)
            yield return StartCoroutine(GetJson<RootHAM>(baseURL+$"&page={rnd}", r=>data=r));

        var list = data.records.Where(r=>!string.IsNullOrEmpty(r.primaryimageurl)).ToList();
        Shuffle(list); if(list.Count>WANT) list=list.GetRange(0,WANT);

        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrameHarvard")
                             .OrderBy(g=>g.name)
                             .Select(g=>g.GetComponent<ArtFrame>())
                             .Take(WANT)
                             .ToArray();

        /* ---------- 下载并贴图 ---------- */
for (int i = 0; i < frames.Length && i < list.Count; i++)
{
    // ① 取 HTTPS 链接；若 secureimageurl 为空再兜底 primaryimageurl
    string img = !string.IsNullOrEmpty(list[i].secureimageurl)
                 ? list[i].secureimageurl
                 : list[i].primaryimageurl;

    if (string.IsNullOrEmpty(img)) continue;

    // ② 万一还是 http: 强制替换为 https:
    if (img.StartsWith("http:"))
        img = "https:" + img.Substring(5);

    // ③ 降分辨率
    img = img.Replace("/full/full/0/", "/full/400,/0/");

    yield return StartCoroutine(SetTexture(frames[i], img));
    frames[i].title = list[i].title ?? "(object)";
}

        if (statusText) statusText.text = "Done";
    }

    public void ReloadFromPrefs()
    {
        StopAllCoroutines();
        string kw = PlayerPrefs.GetString("WeatherKeyword","sun");
        StartCoroutine(LoadWeatherGallery(kw));
    }

    /* -------- 工具同前 -------- */
    IEnumerator GetJson<T>(string url, System.Action<T> cb){
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if(req.result!=UnityWebRequest.Result.Success){Debug.Log(req.error);yield break;}
        cb(JsonUtility.FromJson<T>(req.downloadHandler.text));
    }
    IEnumerator SetTexture(ArtFrame f,string u){
        using UnityWebRequest r = UnityWebRequestTexture.GetTexture(u);
        yield return r.SendWebRequest();
        if(r.result!=UnityWebRequest.Result.Success) yield break;
        Texture t = DownloadHandlerTexture.GetContent(r);
        f.paintingRenderer.sharedMaterial = new Material(pictureMat);
        f.SetTexture(t); 
        f.hiResUrl=u;
        f.hiTex = t;   // 确保缓存更新
    }
    void Shuffle<T>(IList<T> a){for(int i=a.Count-1;i>0;i--){int j=Random.Range(0,i+1);(a[i],a[j])=(a[j],a[i]);}}

    /* --- JSON --- */
    [System.Serializable] public class Info   { public int pages; }
    [System.Serializable] public class Record
    {
        public string primaryimageurl;
        public string secureimageurl;
        public string title;
        }

    [System.Serializable] public class RootHAM{ public Info info; public List<Record> records; }
}
