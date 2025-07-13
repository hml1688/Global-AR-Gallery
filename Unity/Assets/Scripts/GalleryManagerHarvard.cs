using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Linq;
using TMPro;
using System;
using System.Text.RegularExpressions;

public class GalleryManagerHarvard : MonoBehaviour
{
    [Header("Picture material (Unlit/Texture)")]
    public Material pictureMat;

    [Header("Loading text (TMP) – optional")]
    public TextMeshProUGUI statusText;

    // Harvard API Key
    const string APIKEY = "d54e083e-a267-40e4-8d55-f1259589be3b";

    // 只要 10 幅
    const int WANT = 10;
    const int PAGE_SIZE = 100;
    const int MAX_COUNTRY = 30;
    // 并发抓取的国家数（不要太大，防止 API 限速）
    const int PARALLEL_COUNTRIES = 6;
    int activeJobs = 0;

    // 与 JS 相同的区域字典（可精简）
    readonly Dictionary<string, string[]> REGION = new()
    {
        ["Europe"] = new[]{"France","Germany","Italy","United Kingdom","England",
                           "Netherlands","Spain","Sweden","Russia","Greece","Austria",
                           "Belgium","Denmark","Ireland","Malta","Norway","Portugal","Switzerland"},
        ["North and central America"] = new[]{"United States","USA","Mexico","Canada",
                           "Guatemala","Cuba","Costa Rica","Panama","Greenland"},
        ["Asia"] = new[]{"China","Japan","India","Korea","Iran","Turkey","Thailand",
                         "Indonesia","Afghanistan","Armenia","Azerbaijan","Caucasus",
                         "Dagestan","Georgia","Uzbekistan","Mongolia","Iraq","Palestine",
                         "Syria","Nepal","Pakistan","Sri Lanka","Cambodia","Burma",
                         "Vietnam","Philippines","Malaysia"},
        ["Latin America"] = new[]{"Brazil","Argentina","Peru","Chile",
                                  "Colombia","Ecuador","Bolivia"},
        ["Africa & Oceania"] = new[]{"Nigeria","Egypt","South Africa","North Africa",
                                     "Algeria","Congo","Ethiopia","Morocco","Sudan",
                                     "Uganda","Kenya","Ghana","Australia","New Zealand",
                                     "Fiji","Papua New Guinea","Tahiti","Marquesas Islands"}
    };

    void Start() => StartCoroutine(LoadGallery());

    IEnumerator LoadGallery()
    {
        string region = PlayerPrefs.GetString("region", "Europe");
        int fromY = PlayerPrefs.GetInt("yearFrom", -800);
        int toY   = PlayerPrefs.GetInt("yearTo",   1300);

        // 1️⃣ 开头：显示 Loading
        if (statusText) {
            statusText.gameObject.SetActive(true);
            statusText.text = $"HAM Loading {region} {fromY}–{toY} …";
            }


        // 1. 找到属于我的 10 个画框
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrameHarvard")
                                      .OrderBy(g => g.name)
                                      .Select(g => g.GetComponent<ArtFrame>())
                                      .ToArray();
        foreach (var f in frames) f.hiTex = null;

        // 2. 拉取 10 条数据
        List<string> countries = new List<string>(REGION[region]);
    Shuffle(countries);                         // ★ 打乱国家顺序（保持随机）

    List<Coroutine> running = new List<Coroutine>();
    List<JToken>    artworks = new List<JToken>();

    foreach (string c in countries.Take(MAX_COUNTRY))
        {
            while (activeJobs >= PARALLEL_COUNTRIES)
                yield return null;

            activeJobs++;
            StartCoroutine(FetchOneCountry(c, fromY, toY, artworks));

            if (artworks.Count >= WANT) break;
        }

    /* —— 等待所有仍在跑的国家协程结束，或已经拼够 10 幅 —— */
    while (activeJobs > 0 && artworks.Count < WANT)
            yield return null;

        // 3. 绑定到画框
        // 先初始化 loaded 计数
        int loaded = 0;
        for (int i = 0; i < frames.Length && i < artworks.Count; i++)
        {
            var rec = artworks[i];

            string thumb = rec["primaryimageurl"]?.ToString() ??
                           rec["images"]?[0]?["baseimageurl"]?.ToString();
            if (string.IsNullOrEmpty(thumb)) continue;

            string iiif  = rec["images"]?[0]?["iiifbaseuri"]?.ToString();
            string hiUrl = !string.IsNullOrEmpty(iiif) ? $"{iiif}/full/!1024,1024/0/default.jpg" : thumb;

            // 下载缩略图
            UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
            yield return texReq.SendWebRequest();
            if (texReq.result != UnityWebRequest.Result.Success) continue;

            Texture tex = DownloadHandlerTexture.GetContent(texReq);
            frames[i].paintingRenderer.sharedMaterial = new Material(pictureMat);
            frames[i].SetTexture(tex);

            frames[i].hiResUrl = hiUrl;
            frames[i].title    = rec["title"]?.ToString() ?? "(object)";
            frames[i].date     = rec["dated"]?.ToString() ?? "";
            frames[i].maker    = rec["people"]?[0]?["displayname"]?.ToString() ?? "";
            frames[i].place    = rec["place"]?.ToString() ??
                                 rec["places"]?[0]?["displayname"]?.ToString() ?? "Unknown";
            frames[i].hiTex    = tex; // 缓存缩略图

            // —— 在这里递增 loaded 并更新文本 —— 
            loaded++;
            if (statusText) {
            statusText.text = $"HAM Loaded {loaded}/{WANT}";
            }
        }

        if (statusText) {
    statusText.text = "Done";
    statusText.gameObject.SetActive(false);
}

    }

    // ---------- 协程：请求一个国家 ----------
    /* 把一整个国家能贡献的作品尽量塞进 store，直到：
   ▶ store ≥ WANT，或 ▶ 用时 >7 秒 */
IEnumerator FetchOneCountry(string country, int fromY, int toY, List<JToken> store)
{
    // ◆ 进入协程就打日志，并把 activeJobs++ 已在 LoadGallery 里做了 ◆
    DebugHelper.Show($"▶ {country}: start", 3f);

    // 为了保证无论如何都能 activeJobs--，用 try/finally
    try
    {
        // —— 0. placeId
        string pid = null;
        string urlPlace = $"https://api.harvardartmuseums.org/place?apikey={APIKEY}"
                        + $"&size=1&q={UnityWebRequest.EscapeURL(country)}";
        using var reqP = UnityWebRequest.Get(urlPlace);
        reqP.timeout = 7;
        yield return reqP.SendWebRequest();
        if (reqP.result == UnityWebRequest.Result.Success)
            pid = JToken.Parse(reqP.downloadHandler.text)["records"]?[0]?["id"]?.ToString();
        if (string.IsNullOrEmpty(pid))
        {
            DebugHelper.Show($"✖ {country}: no placeId", 2f);
            yield break;
        }

        // —— 1. 先抓 page=1 拿 pages & firstRecs
        int totalPages = 1;
        List<JToken> firstRecs = null;
        yield return StartCoroutine(GetPage(pid, 1, fromY, toY,
            (pages, recs) => {
                totalPages = pages;
                firstRecs  = recs;
            }));
        if (firstRecs == null)
        {
            DebugHelper.Show($"✖ {country}: page1 failed", 2f);
            yield break;
        }
        DebugHelper.Show($"✔ {country}: pages={totalPages}", 2f);

        // —— 2. 处理第 1 页
        Shuffle(firstRecs);
        foreach (var r in firstRecs)
        {
            if (store.Count >= WANT) break;
            if (!HasImage(r)) continue;
            if (!DateWithin(r["dated"]?.ToString(), fromY, toY)) continue;
            store.Add(r);
        }
        DebugHelper.Show($"ℹ {country}: after p1 → {store.Count}/{WANT}", 2f);
        if (store.Count >= WANT) yield break;

        // —— 3. 顺序拉其余页，直到凑够 or 超时
        const int MAX_MS = 7000;
        float t0 = Time.realtimeSinceStartup * 1000f;
        for (int page = 2; page <= totalPages; page++)
        {
            if (Time.realtimeSinceStartup * 1000f - t0 > MAX_MS)
            {
                DebugHelper.Show($"⏱ {country}: timeout", 2f);
                break;
            }

            List<JToken> recs = null;
            yield return StartCoroutine(GetPage(pid, page, fromY, toY,
                (__, list) => recs = list));
            if (recs == null) continue;

            Shuffle(recs);
            foreach (var r in recs)
            {
                if (store.Count >= WANT) break;
                if (!HasImage(r)) continue;
                if (!DateWithin(r["dated"]?.ToString(), fromY, toY)) continue;
                store.Add(r);
            }
            DebugHelper.Show($"ℹ {country}: after p{page} → {store.Count}/{WANT}", 2f);
            if (store.Count >= WANT) break;
        }
    }
    finally
    {
        // ◆ 一定要 activeJobs-- ◆
        activeJobs--;
        DebugHelper.Show($"◀ {country}: done", 2f);
    }
}


/* ---------- 抓单页：GetPage ---------- */
IEnumerator GetPage(string pid, int page, int f, int t,
                    System.Action<int, List<JToken>> cb)
{
    string fields = "id,title,dated,people,place,places,primaryimageurl,images,iiifbaseuri";
    string url = $"https://api.harvardartmuseums.org/object?apikey={APIKEY}" +
                 $"&place={pid}&hasimage=1&size={PAGE_SIZE}&page={page}" +
                 $"&fromdate={f}&todate={t}&fields={fields}";

    using var req = UnityWebRequest.Get(url);
    req.timeout = 7;
    yield return req.SendWebRequest();

    if (req.result != UnityWebRequest.Result.Success)
    {
        cb?.Invoke(1, null);          // 至少把 pages=1 回去，records=null
        yield break;
    }

    var jo     = JToken.Parse(req.downloadHandler.text);
    int pages  = jo["info"]?["pages"]?.ToObject<int>() ?? 1;
    var recs   = jo["records"]?.ToList() ?? new List<JToken>();

    cb?.Invoke(pages, recs);
}

/* 判断记录是否真的有图（包括 secureimageurl） */
static bool HasImage(JToken r) =>
        r["secureimageurl"] != null
     || r["primaryimageurl"] != null
     || r["images"]?.Any(img => img["baseimageurl"] != null) == true
     || r["images"]?[0]?["baseimageurl"] != null;



    // ======== 模糊日期解析 =========
static bool DateWithin(string dated, int from, int to)
{
    if (string.IsNullOrWhiteSpace(dated)) return false;
    (int minY, int maxY)? rng = ParseYearRange(dated);
    if (rng == null) return false;
    return !(rng.Value.maxY < from || rng.Value.minY > to);
}

static (int minY, int maxY)? ParseYearRange(string str)
{
    str = str.ToLower();
    List<int> yrs = new List<int>();

    void Add(int a, int b) { yrs.Add(a); yrs.Add(b); }

    //—— 1) millennium ——//
    str = Regex.Replace(str,
        @"(\d+)(?:st|nd|rd|th)?\s+millennium\s*(bce|bc|ce|ad)?",
        m => {
            int n = int.Parse(m.Groups[1].Value);
            bool bc = m.Groups[2].Value.StartsWith("b");
            if (bc) Add(-n*1000, -(n-1)*1000-1);
            else    Add((n-1)*1000, n*1000-1);
            return " ";
        });

    //—— 2) century RANGE (含 early/mid/late) ——//
    str = Regex.Replace(str,
        @"(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s*-\s*(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s+century\s*(bce|bc|ce|ad)?",
        m => {
            (int a, int b) Seg(int c,string mod){
                int s=(c-1)*100,e=s+99;
                if(mod=="early") e=s+49;
                else if(mod=="late") s+=50;
                else if(mod=="mid"){s+=25;e-=25;}
                return (s,e);
            }
            var s1=Seg(int.Parse(m.Groups[2].Value), m.Groups[1].Value);
            var s2=Seg(int.Parse(m.Groups[4].Value), m.Groups[3].Value);
            int a=Math.Min(s1.a,s2.a),b=Math.Max(s1.b,s2.b);
            if(m.Groups[5].Value.StartsWith("b")) Add(-b,-a);
            else Add(a,b);
            return " ";
        });

    //—— 3) single century ——//
    str = Regex.Replace(str,
        @"(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s+century\s*(bce|bc|ce|ad)?",
        m=>{
            int c=int.Parse(m.Groups[2].Value);int a=(c-1)*100,b=a+99;
            string mod=m.Groups[1].Value;
            if(mod=="early") b=a+49;
            else if(mod=="late") a+=50;
            else if(mod=="mid"){a+=25;b-=25;}
            if(m.Groups[3].Value.StartsWith("b")) Add(-b,-a); else Add(a,b);
            return" ";
        });

    //—— 4) numeric RANGE 1620-40 / 1853-54 / 1800-1900 ——//
    str = Regex.Replace(str,
        @"(?:c\.?\s*)?(\d{3,4})\s*[-–—]\s*(\d{1,4})\s*(bce|bc|ce|ad)?",
        m=>{
            int a=int.Parse(m.Groups[1].Value),b;
            string y2=m.Groups[2].Value;
            if(y2.Length<m.Groups[1].Value.Length && !m.Groups[3].Value.StartsWith("b"))
            {   // 1620-40 → 1640
                int factor=(int)Mathf.Pow(10,y2.Length);
                b=a-a%factor+int.Parse(y2);
            }else b=int.Parse(y2);
            if(m.Groups[3].Value.StartsWith("b")){a=-a;b=-b;if(a>b){var t=a;a=b;b=t;}}
            Add(a,b);
            return" ";
        });

    //—— 5) circa / 单年份 ——//
    str = Regex.Replace(str,
        @"(?:c\.?\s*)?(\d{3,4})\s*(bce|bc|ce|ad)?",
        m=>{
            int y=int.Parse(m.Groups[1].Value);
            if(m.Groups[2].Value.StartsWith("b")) y=-y;
            Add(y,y);return" ";
        });

    if (yrs.Count == 0) return null;
    return (yrs.Min(), yrs.Max());
}


    // ---------- 小工具 ----------
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ========== 公共刷新接口 ==========
public void ReloadGallery()
{
    StopAllCoroutines();
    StartCoroutine(WaitAndLoad());
}

IEnumerator WaitAndLoad()
{
    if (statusText)
    {
        statusText.gameObject.SetActive(true);
        statusText.text = "Loading…";
    }

    // 等待场景中 10 个 Harvard 画框就绪
    while (GameObject.FindGameObjectsWithTag("ArtFrameHarvard").Length < 10)
        yield return new WaitForSeconds(0.1f);

    yield return StartCoroutine(LoadGallery());
}

}