using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;   // Parse JSON
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

    // Constants
    const int WANT = 10;  // Number of artworks to load
    const int PAGE_SIZE = 100;   // API page size
    const int MAX_COUNTRY = 30;   // Max countries to query
    const int PARALLEL_COUNTRIES = 6;   // Max parallel requests to avoid rate limiting
    int activeJobs = 0;   // Track active country fetch tasks

    // Region to country mappings
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
    void Start() { }

    // Main loading coroutine
    IEnumerator LoadGallery()
    {
        string region = PlayerPrefs.GetString("region", "Europe");
        int fromY = PlayerPrefs.GetInt("yearFrom", -800);
        int toY = PlayerPrefs.GetInt("yearTo", 1300);

        // Show initial loading text
        if (statusText)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = $"HAM Loading {region} {fromY}–{toY} …";
        }


        // Find 10 frames that belong to Harvard
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrameHarvard")
                                      .OrderBy(g => g.name)
                                      .Select(g => g.GetComponent<ArtFrame>())
                                      .ToArray();
        foreach (var f in frames) f.hiTex = null;

        // Prepare to collect artwork data
        List<string> countries = new List<string>(REGION[region]);
        Shuffle(countries);                         // Disorder the sequence of countries (keep it random)

        List<Coroutine> running = new List<Coroutine>();
        List<JToken> artworks = new List<JToken>();

        // Start fetching artworks by country
        foreach (string c in countries.Take(MAX_COUNTRY))
        {
            while (activeJobs >= PARALLEL_COUNTRIES)
                yield return null;

            activeJobs++;
            StartCoroutine(FetchOneCountry(c, fromY, toY, artworks));

            if (artworks.Count >= WANT) break;
        }

        // Wait until all fetch jobs complete or timeout
        int waitLoops = 0;
        while (activeJobs > 0)
        {
            yield return null;
            waitLoops++;
            HUD($" Waiting... activeJobs = {activeJobs}", 1f);

            if (waitLoops > 500) // Maximum waiting time: 500 frames
            {
                HUD(" Force exit wait! ActiveJobs has not been reset to zero", 5f);
                break;
            }
        }

        HUD($" Pre-bind counts: artworks={artworks.Count}", 3f);
        yield return new WaitForSeconds(3f);
        HUD($" region={region}  fromY={fromY}  toY={toY}", 3f);
        // Shuffle and bind artworks to frames
        Shuffle(artworks);

        // Round 1: Online record filling in boxes
        int loaded = 0;
        int idx = 0;
        while (loaded < frames.Length && idx < artworks.Count)
        {
            var rec = artworks[idx++];
            GetHamUrls(rec, out string thumb, out string hiUrl);
            HUD($"idx={idx - 1}, thumb={thumb}", 2f);
            if (string.IsNullOrEmpty(thumb)) continue;

            using UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
            yield return texReq.SendWebRequest();
            if (texReq.result != UnityWebRequest.Result.Success)
            {
                DebugHelper.Show($"Img fail: {texReq.error}\n{thumb}", 3f);
                continue;
            }

            Texture tex = DownloadHandlerTexture.GetContent(texReq);
            var f = frames[loaded];
            f.paintingRenderer.sharedMaterial = new Material(pictureMat);
            f.SetTexture(tex);

            // Assign metadata
            f.hiResUrl = hiUrl;
            f.title = rec["title"]?.ToString() ?? "(object)";
            f.date = rec["dated"]?.ToString() ?? "";
            f.maker = rec["people"]?[0]?["displayname"]?.ToString() ?? rec["maker"]?.ToString() ?? "";
            f.place = rec["place"]?.ToString() ??
                         rec["places"]?[0]?["displayname"]?.ToString() ??
                         "Unknown";
            f.hiTex = tex;

            loaded++;
            if (statusText) statusText.text = $"HAM Loaded {loaded}/{WANT}";
        }

        HUD($"Checking offline fallback: loaded={loaded}, region={region}, fromY={fromY}, toY={toY}", 3f);


        // Check if online results are insufficient AND within supported offline range
        if (loaded < WANT && region == "Asia" && !(toY < 2000 || fromY > 2025))
        {
            // Load local JSON cache for Asia (2000–2025) if not already loaded
            yield return OfflineHamAsia.EnsureLoaded();
            int need = WANT - loaded;
            // Filter local records that match the selected time range
            var picks = OfflineHamAsia.Pick(fromY, toY, need);

            // Display number of offline picks for debugging
            HUD($"Offline picks={picks.Count}", 3f);

            foreach (var recObj in picks)
            {
                var rec = OfflineHamAsia.ToJObj(recObj);
                // Extract thumbnail and high-res image URLs
                GetHamUrls(rec, out string thumb, out string hiUrl);
                if (string.IsNullOrEmpty(thumb)) continue;

                // Asynchronously fetch thumbnail texture
                using UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
                yield return texReq.SendWebRequest();
                if (texReq.result != UnityWebRequest.Result.Success)
                {
                    DebugHelper.Show($"Img fail: {texReq.error}\n{thumb}", 3f);
                    continue;
                }

                // Assign image to next available frame in the scene
                Texture tex = DownloadHandlerTexture.GetContent(texReq);
                var f = frames[loaded];
                f.paintingRenderer.sharedMaterial = new Material(pictureMat); // Clone material
                f.SetTexture(tex);     // Apply texture
                f.hiResUrl = hiUrl;
                f.title = rec["title"]?.ToString() ?? "(object)";
                f.date = rec["dated"]?.ToString() ?? "";
                f.maker = rec["maker"]?.ToString() ?? "";
                f.place = rec["place"]?.ToString() ?? "Asia";
                f.hiTex = tex;        // Cache texture for info panel

                loaded++;
                // Update loading status on screen
                if (statusText) statusText.text = $"HAM Loaded {loaded}/{WANT}";
                if (loaded >= WANT) break;
            }
            // Show final fallback summary
            DebugHelper.Show($"Offline fallback: added {Mathf.Min(need, picks.Count)} Asia recs ({fromY}–{toY})", 3f);
        }

        // ====== Europe 2000–2025 Offline logic ======
        if (loaded < WANT && region == "Europe" && fromY >= 2000 && toY <= 2025)
        {
            yield return OfflineHamEurope.EnsureLoaded();
            int need = WANT - loaded;
            var picks = OfflineHamEurope.Pick(fromY, toY, need);
            HUD($"🟦 Europe offline picks={picks.Count}", 3f);

            foreach (var recObj in picks)
            {
                var rec = OfflineHamEurope.ToJObj(recObj);
                GetHamUrls(rec, out string thumb, out string hiUrl);
                if (string.IsNullOrEmpty(thumb)) continue;

                using UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
                yield return texReq.SendWebRequest();
                if (texReq.result != UnityWebRequest.Result.Success)
                {
                    DebugHelper.Show($"Img fail: {texReq.error}\n{thumb}", 3f);
                    continue;
                }

                Texture tex = DownloadHandlerTexture.GetContent(texReq);
                var f = frames[loaded];
                f.paintingRenderer.sharedMaterial = new Material(pictureMat);
                f.SetTexture(tex);
                f.hiResUrl = hiUrl;
                f.title = rec["title"]?.ToString() ?? "(object)";
                f.date = rec["dated"]?.ToString() ?? "";
                f.maker = rec["maker"]?.ToString() ?? "";
                f.place = rec["place"]?.ToString() ?? "Europe";
                f.hiTex = tex;

                loaded++;
                if (statusText) statusText.text = $"HAM Loaded {loaded}/{WANT}";
                if (loaded >= WANT) break;
            }

            DebugHelper.Show($"Europe offline fallback: added {Mathf.Min(need, picks.Count)} recs ({fromY}–{toY})", 3f);
        }

        if (statusText)
        {
            statusText.text = "Done";
            statusText.gameObject.SetActive(false);
        }

    }

    // Coroutine to fetch artworks from one country
    // Try to pack all the works that the entire country can contribute into the store, until: the store is filled to capacity (store ≥ WANT), or the time taken exceeds 7 seconds.
    IEnumerator FetchOneCountry(string country, int fromY, int toY, List<JToken> store)
    {
        float start = Time.realtimeSinceStartup;
        float timeout = 10f; // At most 10 seconds

        try
        {
            HUD($"▶ Start country: {country}", 2f);

            // placeId
            string pid = null;
            string urlPlace = $"https://api.harvardartmuseums.org/place?apikey={APIKEY}"
                            + $"&size=1&q={UnityWebRequest.EscapeURL(country)}";
            using (var reqP = UnityWebRequest.Get(urlPlace))
            {
                reqP.timeout = 7;
                yield return reqP.SendWebRequest();

                if (reqP.result != UnityWebRequest.Result.Success)
                {
                    HUD($"Network failure: {country} {reqP.error}", 2f);
                    yield break; // finally will be executed
                }
                // JSON parse try-catch
                try
                {
                    pid = JToken.Parse(reqP.downloadHandler.text)["records"]?[0]?["id"]?.ToString();
                }
                catch (Exception ex)
                {
                    HUD($"JSON parsing failed: {country}\n{ex}", 2f);
                    yield break;
                }
            }

            if (string.IsNullOrEmpty(pid))
            {
                HUD($" No placeId: {country}", 2f);
                yield break;
            }

            // 1. Request data for the first page to obtain the pages & firstRecs
            int totalPages = 1;
            List<JToken> firstRecs = null;
            bool page1Fail = false;
            yield return StartCoroutine(GetPage(pid, 1, fromY, toY,
                (pages, recs) =>
                {
                    totalPages = pages;
                    firstRecs = recs;
                }));

            if (firstRecs == null)
            {
                HUD($"No records in page 1: {country}", 2f);
                yield break;
            }

            // —— 2. handle the first page
            Shuffle(firstRecs);
            foreach (var r in firstRecs)
            {
                if (store.Count >= WANT) break;
                if (!HasImage(r)) continue;
                if (!DateWithin(r["dated"]?.ToString(), fromY, toY)) continue;

                GetHamUrls(r, out string thumb, out string hiUrl);
                // Check if there is a picture for this exhibit. If there is no picture, discard it.
                if (string.IsNullOrEmpty(thumb)) continue;
                store.Add(r);

                HUD($"[store] Added 1, now count={store.Count}", 1.5f);
            }
            if (store.Count >= WANT) yield break;

            // —— 3. Traverse the remaining pages in sequence until the required amount is reached or the time limit is exceeded.
            const int MAX_MS = 7000;
            float t0 = Time.realtimeSinceStartup * 1000f;
            for (int page = 2; page <= totalPages; page++)
            {
                if (Time.realtimeSinceStartup * 1000f - t0 > MAX_MS)
                {
                    HUD($"Timeout page loop: {country}", 2f);
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
                    // Check if there is a picture for this exhibit. If there is no picture, discard it.
                    if (string.IsNullOrEmpty(thumb)) continue;
                    store.Add(r);
                    HUD($"[store] Added 1, now count={store.Count}", 1.5f);
                }
                if (store.Count >= WANT) break;

                // Global timeout limit
                if (Time.realtimeSinceStartup - start > timeout)  // // timeout = 10f
                {
                    HUD($"Timeout country: {country}", 2f);
                    yield break;
                }
            }
        }
        finally
        {
            activeJobs--;
            HUD($"Finish: {country}, activeJobs now: {activeJobs}", 2f);
        }
    }



    // Take a single page
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
            cb?.Invoke(1, null);          // At least return pages1，records=null
            yield break;
        }

        var jo = JToken.Parse(req.downloadHandler.text);
        int pages = jo["info"]?["pages"]?.ToObject<int>() ?? 1;
        var recs = jo["records"]?.ToList() ?? new List<JToken>();

        cb?.Invoke(pages, recs);
    }

    // Determine whether the record actually contains a picture (including secureimageurl)
    static bool HasImage(JToken r) =>
            r["secureimageurl"] != null
         || r["primaryimageurl"] != null
         || r["thumb"] != null                // offline
         || r["hi"] != null                   // offline
         || r["images"]?.Any(img => img["baseimageurl"] != null) == true
         || r["images"]?[0]?["baseimageurl"] != null;



    // Ambiguous date parsing
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
            m =>
            {
                int n = int.Parse(m.Groups[1].Value);
                bool bc = m.Groups[2].Value.StartsWith("b");
                if (bc) Add(-n * 1000, -(n - 1) * 1000 - 1);
                else Add((n - 1) * 1000, n * 1000 - 1);
                return " ";
            });

        //—— 2) century RANGE (include early/mid/late) ——//
        str = Regex.Replace(str,
            @"(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s*-\s*(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s+century\s*(bce|bc|ce|ad)?",
            m =>
            {
                (int a, int b) Seg(int c, string mod)
                {
                    int s = (c - 1) * 100, e = s + 99;
                    if (mod == "early") e = s + 49;
                    else if (mod == "late") s += 50;
                    else if (mod == "mid") { s += 25; e -= 25; }
                    return (s, e);
                }
                var s1 = Seg(int.Parse(m.Groups[2].Value), m.Groups[1].Value);
                var s2 = Seg(int.Parse(m.Groups[4].Value), m.Groups[3].Value);
                int a = Math.Min(s1.a, s2.a), b = Math.Max(s1.b, s2.b);
                if (m.Groups[5].Value.StartsWith("b")) Add(-b, -a);
                else Add(a, b);
                return " ";
            });

        //—— 3) single century ——//
        str = Regex.Replace(str,
            @"(early|mid|late)?\s*(\d+)(?:st|nd|rd|th)?\s+century\s*(bce|bc|ce|ad)?",
            m =>
            {
                int c = int.Parse(m.Groups[2].Value); int a = (c - 1) * 100, b = a + 99;
                string mod = m.Groups[1].Value;
                if (mod == "early") b = a + 49;
                else if (mod == "late") a += 50;
                else if (mod == "mid") { a += 25; b -= 25; }
                if (m.Groups[3].Value.StartsWith("b")) Add(-b, -a); else Add(a, b);
                return " ";
            });

        //—— 4) numeric RANGE 1620-40 / 1853-54 / 1800-1900 ——//
        str = Regex.Replace(str,
            @"(?:c\.?\s*)?(\d{3,4})\s*[-–—]\s*(\d{1,4})\s*(bce|bc|ce|ad)?",
            m =>
            {
                int a = int.Parse(m.Groups[1].Value), b;
                string y2 = m.Groups[2].Value;
                if (y2.Length < m.Groups[1].Value.Length && !m.Groups[3].Value.StartsWith("b"))
                {   // 1620-40 → 1640
                    int factor = (int)Mathf.Pow(10, y2.Length);
                    b = a - a % factor + int.Parse(y2);
                }
                else b = int.Parse(y2);
                if (m.Groups[3].Value.StartsWith("b")) { a = -a; b = -b; if (a > b) { var t = a; a = b; b = t; } }
                Add(a, b);
                return " ";
            });

        //—— 5) circa / Single year ——//
        str = Regex.Replace(str,
            @"(?:c\.?\s*)?(\d{3,4})\s*(bce|bc|ce|ad)?",
            m =>
            {
                int y = int.Parse(m.Groups[1].Value);
                if (m.Groups[2].Value.StartsWith("b")) y = -y;
                Add(y, y); return " ";
            });

        if (yrs.Count == 0) return null;
        return (yrs.Min(), yrs.Max());
    }


    // Tools
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Public refresh interface
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

        // Wait for the 10 Harvard frames in the scene to be ready
        while (GameObject.FindGameObjectsWithTag("ArtFrameHarvard").Length < 10)
            yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(LoadGallery());
    }

    /// Resolve thumbnail + hiRes from either online Harvard record or offline patched record.
    static void GetHamUrls(JToken rec, out string thumb, out string hiUrl)
    {
        // 1) Priority Offline Field
        hiUrl = rec["_offline_hi"]?.ToString();
        thumb = rec["thumb"]?.ToString();

        // 2) Online field
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

        // 4) Enforce https (process separately, do not overlap with each other)
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

