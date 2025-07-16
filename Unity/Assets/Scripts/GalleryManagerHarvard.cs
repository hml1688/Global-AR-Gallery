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
    static void HUD(string msg, float sec = 3f)
{
    Debug.Log(msg);
    DebugHelper.Show(msg, sec);
}

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

    //void Start() => StartCoroutine(LoadGallery());
    void Start(){}


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

    // —— 等所有国家抓完，强制最多等 500 帧
int waitLoops = 0;
while (activeJobs > 0)
{
    yield return null;
    waitLoops++;
    HUD($" Waiting... activeJobs = {activeJobs}", 1f);

    if (waitLoops > 500) // 比如最多等待 500 帧
    {
        HUD("⚠️ Force exit wait! activeJobs未归零", 5f);
        break;
    }
}

        HUD($" Pre-bind counts: artworks={artworks.Count}", 3f);
        yield return new WaitForSeconds(3f);
        HUD($" region={region}  fromY={fromY}  toY={toY}", 3f);
            // 可选：打乱全局 artworks 顺序
Shuffle(artworks);


        // 3. 绑定到画框
        // 先初始化 loaded 计数
        // int loaded = 0;
        /*for (int i = 0; i < frames.Length && i < artworks.Count; i++)
        {
            var rec = artworks[i];

            string thumb = rec["primaryimageurl"]?.ToString() ??
                           rec["images"]?[0]?["baseimageurl"]?.ToString();
            if (string.IsNullOrEmpty(thumb)) continue;

            string iiif  = rec["images"]?[0]?["iiifbaseuri"]?.ToString();
            string hiUrl = !string.IsNullOrEmpty(iiif) ? $"{iiif}/full/!1024,1024/0/default.jpg" : thumb;*/
        
        // ====== 第一轮：在线记录填框（while-补位） ======
int loaded = 0;
int idx    = 0;
while (loaded < frames.Length && idx < artworks.Count)
{
    var rec = artworks[idx++];
    GetHamUrls(rec, out string thumb, out string hiUrl);
    HUD($"idx={idx-1}, thumb={thumb}", 2f); // 加一句
    if (string.IsNullOrEmpty(thumb)) continue;

    using UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
    yield return texReq.SendWebRequest();
    if (texReq.result != UnityWebRequest.Result.Success)
    {
    DebugHelper.Show($"❌ Img fail: {texReq.error}\n{thumb}", 3f);
    continue;
}

    Texture tex = DownloadHandlerTexture.GetContent(texReq);
    var f = frames[loaded];
    f.paintingRenderer.sharedMaterial = new Material(pictureMat);
    f.SetTexture(tex);

    f.hiResUrl = hiUrl;
    f.title    = rec["title"]?.ToString() ?? "(object)";
    f.date     = rec["dated"]?.ToString() ?? "";
    f.maker    = rec["people"]?[0]?["displayname"]?.ToString() ?? rec["maker"]?.ToString() ?? "";
    f.place    = rec["place"]?.ToString() ??
                 rec["places"]?[0]?["displayname"]?.ToString() ??
                 "Unknown";
    f.hiTex    = tex;

    loaded++;
    if (statusText) statusText.text = $"HAM Loaded {loaded}/{WANT}";
}

HUD($"📌 Checking offline fallback: loaded={loaded}, region={region}, fromY={fromY}, toY={toY}", 3f);


// ====== 第二轮：离线补货（如 Asia + 年代交集 + 未满） ======
if (loaded < WANT && region == "Asia" && !(toY < 2000 || fromY > 2025))
{
    yield return OfflineHamAsia.EnsureLoaded();
    int need = WANT - loaded;
    var picks = OfflineHamAsia.Pick(fromY, toY, need);
    // ✅ 这里添加 HUD 查看 pick 到了多少条
HUD($"🟩 Offline picks={picks.Count}", 3f);

    foreach (var recObj in picks)
    {
        var rec = OfflineHamAsia.ToJObj(recObj);
        GetHamUrls(rec, out string thumb, out string hiUrl);
        if (string.IsNullOrEmpty(thumb)) continue;

        using UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
        yield return texReq.SendWebRequest();
        if (texReq.result != UnityWebRequest.Result.Success) 
        {
    DebugHelper.Show($"❌ Img fail: {texReq.error}\n{thumb}", 3f);
    continue;
}

        Texture tex = DownloadHandlerTexture.GetContent(texReq);
        var f = frames[loaded];
        f.paintingRenderer.sharedMaterial = new Material(pictureMat);
        f.SetTexture(tex);
        f.hiResUrl = hiUrl;
        f.title    = rec["title"]?.ToString() ?? "(object)";
        f.date     = rec["dated"]?.ToString() ?? "";
        f.maker    = rec["maker"]?.ToString() ?? "";
        f.place    = rec["place"]?.ToString() ?? "Asia";
        f.hiTex    = tex;

        loaded++;
        if (statusText) statusText.text = $"HAM Loaded {loaded}/{WANT}";
        if (loaded >= WANT) break;
    }

    DebugHelper.Show($"Offline fallback: added {Mathf.Min(need, picks.Count)} Asia recs ({fromY}–{toY})", 3f);
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
    float start = Time.realtimeSinceStartup;
    float timeout = 10f; // 最多10秒

    try // 只用 try-finally！不要 try-catch-finally
    {
        HUD($"▶ Start country: {country}", 2f);

        // —— 0. placeId
        string pid = null;
        string urlPlace = $"https://api.harvardartmuseums.org/place?apikey={APIKEY}"
                        + $"&size=1&q={UnityWebRequest.EscapeURL(country)}";
        using (var reqP = UnityWebRequest.Get(urlPlace))
        {
            reqP.timeout = 7;
            yield return reqP.SendWebRequest();

            if (reqP.result != UnityWebRequest.Result.Success)
            {
                HUD($"❌ 网络失败: {country} {reqP.error}", 2f);
                yield break; // finally会执行
            }
            // JSON 解析 try-catch（不能有yield）
            try
            {
                pid = JToken.Parse(reqP.downloadHandler.text)["records"]?[0]?["id"]?.ToString();
            }
            catch (Exception ex)
            {
                HUD($"❌ JSON解析失败: {country}\n{ex}", 2f);
                yield break;
            }
        }

        if (string.IsNullOrEmpty(pid))
        {
            HUD($" No placeId: {country}", 2f);
            yield break;
        }

        // —— 1. 先抓 page=1 拿 pages & firstRecs
        int totalPages = 1;
        List<JToken> firstRecs = null;
        bool page1Fail = false;
        yield return StartCoroutine(GetPage(pid, 1, fromY, toY,
            (pages, recs) => {
                totalPages = pages;
                firstRecs = recs;
            }));

        if (firstRecs == null)
        {
            HUD($"No records in page 1: {country}", 2f);
            yield break;
        }

        // —— 2. 处理第 1 页
        Shuffle(firstRecs);
        foreach (var r in firstRecs)
        {
            if (store.Count >= WANT) break;
            if (!HasImage(r)) continue;
            if (!DateWithin(r["dated"]?.ToString(), fromY, toY)) continue;

            GetHamUrls(r, out string thumb, out string hiUrl);
            if (string.IsNullOrEmpty(thumb)) continue; // 没图不要
            store.Add(r);

            HUD($"[store] Added 1, now count={store.Count}", 1.5f);
        }
        if (store.Count >= WANT) yield break;

        // —— 3. 顺序拉其余页，直到凑够 or 超时
        const int MAX_MS = 7000;
        float t0 = Time.realtimeSinceStartup * 1000f;
        for (int page = 2; page <= totalPages; page++)
        {
            if (Time.realtimeSinceStartup * 1000f - t0 > MAX_MS)
            {
                HUD($"⏰ Timeout page loop: {country}", 2f);
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
                GetHamUrls(r, out string thumb, out string hiUrl);
    if (string.IsNullOrEmpty(thumb)) continue; // 没图不要
    store.Add(r);
    HUD($"[store] Added 1, now count={store.Count}", 1.5f);
            }
            if (store.Count >= WANT) break;

            // 超时保护
            if (Time.realtimeSinceStartup - start > timeout)
            {
                HUD($"⏰ Timeout country: {country}", 2f);
                yield break;
            }
        }
    }
    finally
    {
        // **保证一定会被执行**
        activeJobs--;
        HUD($"◀ Finish: {country}, activeJobs now: {activeJobs}", 2f);
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
     || r["thumb"] != null                // offline
     || r["hi"] != null                   // offline
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

/// Resolve thumbnail + hiRes from either online Harvard record or offline patched record.
static void GetHamUrls(JToken rec, out string thumb, out string hiUrl)
{
    // 1) 优先离线字段
    hiUrl = rec["_offline_hi"]?.ToString();
    thumb = rec["thumb"]?.ToString();

    // 2) 在线字段
    if (string.IsNullOrEmpty(thumb))
        thumb = rec["primaryimageurl"]?.ToString();
    if (string.IsNullOrEmpty(thumb))
        thumb = rec["images"]?[0]?["baseimageurl"]?.ToString();

    if (string.IsNullOrEmpty(hiUrl))
    {
        string iiif = rec["images"]?[0]?["iiifbaseuri"]?.ToString()
                   ?? rec["iiifbaseuri"]?.ToString();
        if (!string.IsNullOrEmpty(iiif))
            hiUrl = $"{iiif}/full/!1024,1024/0/default.jpg";
    }

    // 3) fallback
    if (string.IsNullOrEmpty(hiUrl))
        hiUrl = thumb;

    // 4) 强制 https（分别处理，不要互相覆盖）
    thumb = ForceHttps(thumb);
    hiUrl = ForceHttps(hiUrl);
}


static string ForceHttps(string url)
{
    if (string.IsNullOrEmpty(url)) return url;
    if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        return "https://" + url.Substring(7);
    return url;
}

}

