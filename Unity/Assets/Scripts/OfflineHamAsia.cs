using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Loads & caches the offline Harvard Asia 2000–2025 dataset
/// you placed in StreamingAssets/offline-ham-asia-2000-2025-STRICT-CE.json.
/// Provides filtered lists for fallback usage.
/// </summary>
public static class OfflineHamAsia
{
    const string FILENAME = "offline-ham-asia-2000-2025-STRICT-CE.json";

    [Serializable]
    public class Rec
    {
        public int    id;
        public string title;
        public string dated;
        public int    minY;
        public int    maxY;
        public string maker;
        public string place;
        public string region;
        public string thumb;
        public string hi;
    }

    static List<Rec> _all;     // cache
    static bool _loading;      // guard concurrent loads

    /// Ensure data loaded (call as coroutine in MonoBehaviour).
    public static IEnumerator EnsureLoaded()
{
    if (_all != null) yield break;

    if (_loading)
    {
        // 等到其它协程加载完
        while (_loading) yield return null;
        yield break;
    }

    _loading = true;
    string path = Path.Combine(Application.streamingAssetsPath, FILENAME);
    Debug.Log("🟨 JSON Load Path: " + path);

    string json = null;
    if (path.Contains("://") || path.Contains("jar:"))
    {
        using UnityWebRequest req = UnityWebRequest.Get(path);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
        else Debug.LogError("OfflineHamAsia load error: " + req.error);
    }
    else
    {
        try { json = System.IO.File.ReadAllText(path); }
        catch (Exception e) { Debug.LogError("OfflineHamAsia read error: " + e); }
    }

    if (!string.IsNullOrEmpty(json))
    {
        try {
            _all = JsonConvert.DeserializeObject<List<Rec>>(json);
            Debug.Log($"✅ Loaded {_all.Count} offline records.");
        } catch (Exception e) {
            Debug.LogError("OfflineHamAsia parse error: " + e);
            _all = new List<Rec>();
        }
    }
    else
    {
        _all = new List<Rec>();
    }

    _loading = false;
}


    /// <summary>
    /// Get up to `maxCount` records whose year range overlaps user [fromY..toY].
    /// regionIgnored — because this file is Asia-only; include param in case future expansion.
    /// </summary>
    public static List<Rec> Pick(int fromY, int toY, int maxCount)
    {
        if (_all == null) return new List<Rec>();

        // overlap check
        bool Overlaps(Rec r) => !(r.maxY < fromY || r.minY > toY);

        var pool = _all.FindAll(Overlaps);
        Shuffle(pool);
        if (pool.Count > maxCount) pool.RemoveRange(maxCount, pool.Count - maxCount);
        return pool;
    }

    /// Convert offline Rec to a JObject that mimics a Harvard API record
    /// so existing binding code keeps working with minimal changes.
    public static JObject ToJObj(Rec r)
    {
        // people[0].displayname  -> maker
        var peopleArr = new JArray();
        if (!string.IsNullOrEmpty(r.maker))
            peopleArr.Add(new JObject { ["displayname"] = r.maker });

        // create minimal "images"
        var imagesArr = new JArray();
        imagesArr.Add(new JObject {
            ["baseimageurl"] = r.thumb,
            // Can't guarantee IIIF base is derivable; store hi in custom key:
            ["hi"] = r.hi
        });

        var jo = new JObject
        {
            ["id"]    = r.id,
            ["title"] = r.title,
            ["dated"] = r.dated,
            ["people"]= peopleArr,
            ["place"] = r.place,
            ["primaryimageurl"] = r.thumb,
            // Custom hi field (read in binding helper)
            ["_offline_hi"] = r.hi,
            ["images"] = imagesArr
        };
        return jo;
    }

    /// Simple in-place Fisher–Yates
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
