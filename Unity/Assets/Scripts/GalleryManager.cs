using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Linq;                     // OrderBy / LINQ

public class GalleryManager : MonoBehaviour
{
    [Header("Picture material (Unlit/Texture)")]
    public Material pictureMat;

    [Header("Loading text (TMP) – optional")]
    public TextMeshProUGUI statusText;

    /* ------------ Region → Countries 列表，与 HTML 保持一致 -------------- */
    readonly Dictionary<string,string[]> REGION_COUNTRIES = new()
    {
        ["Europe"] = new[]{"France","Germany","Italy","United Kingdom",
                           "England","Netherlands","Spain","Sweden","Russia"},
        ["North and central America"] = new[]{"United States","USA","Mexico",
                           "Canada","Guatemala","Cuba"},
        ["Asia"] = new[]{"China","Japan","India","Korea","Iran",
                         "Turkey","Thailand","Indonesia"},
        ["Latin America"] = new[]{"Brazil","Argentina","Peru",
                                  "Chile","Colombia","Ecuador","Bolivia"},
        ["Africa & Oceania"] = new[]{"Nigeria","Egypt","South Africa","Kenya","Ghana",
                                     "Australia","New Zealand","Fiji","Papua New Guinea"}
    };

    /* -------------------- 常量 -------------------- */
    const int WANT = 20;        // 目标张数
    const int PAGE_SIZE = 100;  // 每次 API 取 100
    const int MAX_COUNTRY = 8;  // 最多轮询 8 国

    /* ========== 入口 ========== */
    void Start() => StartCoroutine(LoadGallery());

    /* ========== 主协程 ========== */
    IEnumerator LoadGallery()
    {
        /* 1️⃣ 读取 Menu 场景写入的筛选条件（如无则用缺省值） */
        string region = PlayerPrefs.GetString("region", "Europe");
        int fromY = PlayerPrefs.GetInt("yearFrom", 1500);
        int toY   = PlayerPrefs.GetInt("yearTo"  , 1900);

        if (statusText) statusText.text = $"Loading {region}  {fromY}–{toY} …";

        /* 2️⃣ 找到场景里全部 20 个 Canvas（Tag=ArtFrame） */
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrame")
                                      .OrderBy(g => g.name)      // 确保顺序一致
                                      .Select(g => g.GetComponent<ArtFrame>())
                                      .ToArray();
        if (frames.Length == 0)
        {
            Debug.LogError("No ArtFrame found in scene!");
            yield break;
        }

        /* 3️⃣ HTML 同款“均衡算法”——为每国抓 1 页再平均抽取 */
        var buckets = new Dictionary<string,List<JToken>>();
        var countries = new List<string>(REGION_COUNTRIES[region]);
        Shuffle(countries);

        /* 3-1：并行请求各国第一页 */
        foreach (var c in countries.Take(MAX_COUNTRY))
        {
            string url = BuildURL("q_place_name", c, fromY, toY);
            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();           // 等待

            if (req.result != UnityWebRequest.Result.Success)
            { Debug.Log(req.error); continue; }

            JToken j = JToken.Parse(req.downloadHandler.text);
            var list = new List<JToken>();

            foreach (var rec in j["records"] ?? new JArray())
            {
                if (rec["_images"]?["_primary_thumbnail"] == null) continue;   // 必须有缩略图
                if (!PlaceMatches(rec, c)) continue;                           // 地名匹配
                list.Add(rec);
            }
            if (list.Count > 0) buckets[c] = list;
        }

        /* 3-2：轮询各桶平均取到 WANT */
        var chosen = new List<JToken>();
        var seen   = new HashSet<string>();   // systemNumber 去重

        while (chosen.Count < WANT)
        {
            bool moved = false;
            foreach (var kv in buckets)
            {
                var arr = kv.Value;
                // 跳过重复
                while (arr.Count > 0 && seen.Contains(arr[^1]["systemNumber"]!.ToString()))
                    arr.RemoveAt(arr.Count - 1);

                if (arr.Count > 0)
                {
                    var rec = arr[^1]; arr.RemoveAt(arr.Count - 1);
                    chosen.Add(rec);
                    seen.Add(rec["systemNumber"]!.ToString());
                    moved = true;
                    if (chosen.Count == WANT) break;
                }
            }
            if (!moved) break;   // 全部桶都空
        }

        /* 4️⃣ 下载缩略图并贴到 Canvas */
        Shuffle(chosen);                     // 再随机打散

        int loaded = 0;
        for (int i = 0; i < frames.Length && i < chosen.Count; i++)
        {
            var rec   = chosen[i];
            string thumb = rec["_images"]["_primary_thumbnail"]!.ToString();
            string id    = rec["_primaryImageId"]!.ToString();
            string full  = $"https://framemark.vam.ac.uk/collections/{id}/full/!800,800/0/default.jpg";

            UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(thumb);
            yield return texReq.SendWebRequest();

            if (texReq.result == UnityWebRequest.Result.Success)
            {
                Texture tex = DownloadHandlerTexture.GetContent(texReq);
                frames[i].paintingRenderer.sharedMaterial = new Material(pictureMat);
                frames[i].SetTexture(tex);
                frames[i].hiResUrl = full;          // 点击可看大图
            }
            loaded++;
            if (statusText) statusText.text = $"Loaded {loaded}/{Mathf.Min(WANT, chosen.Count)}";
        }

        /* 5️⃣ 结束 */
        if (statusText) statusText.text = "Done";
    }

    /* --------- 工具函数 --------- */
    static string BuildURL(string param, string val, int f, int t) =>
        $"https://api.vam.ac.uk/v2/objects/search?{param}={UnityWebRequest.EscapeURL(val)}" +
        $"&year_made_from={f}&year_made_to={t}&images_exist=1&page_size={PAGE_SIZE}";

    static bool PlaceMatches(JToken rec, string country)
    {
        string place = (rec["_primaryPlace"] ?? rec["placeOfOrigin"] ?? "").ToString().ToLower();
        string kw = country.ToLower();
        return place == kw || place.Contains(kw);
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
