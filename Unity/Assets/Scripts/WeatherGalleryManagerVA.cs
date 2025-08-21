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
    public TextMeshProUGUI statusText;   // Optional loading status text

    const int WANT = 10;
    const int PAGE_SIZE = 100;

    /* -------- Main entry coroutine -------- */
    public IEnumerator LoadWeatherGallery(string keyword)
    {
        if (statusText) statusText.text = "V&A Loading…";

        // Construct base API URL with keyword and pagination settings
        string baseURL = $"https://api.vam.ac.uk/v2/objects/search" +
                         $"?q={UnityWebRequest.EscapeURL(keyword)}" +
                         $"&image_exists=true&page_size={PAGE_SIZE}&responseGroup=full";

        // Load first page of results
        RootVA page1 = null;
        yield return StartCoroutine(YieldJson<RootVA>(baseURL + "&page=1", r => page1 = r));
        if (page1 == null) yield break;

        int total = page1.info.record_count;
        int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PAGE_SIZE));

        // Randomly pick one page (might be page1 again)
        int rndPage = Random.Range(1, pages + 1);
        RootVA data = page1;
        if (rndPage != 1)
        {
            yield return StartCoroutine(YieldJson<RootVA>(baseURL + $"&page={rndPage}", r => data = r));
            if (data == null) yield break;
        }


        // Filter valid image entries and shuffle
        var list = data.records.Where(r => !string.IsNullOrEmpty(r._primaryImageId)).ToList();
        Shuffle(list);
        // Fallback refill logic if not enough result
        if (list.Count < WANT)              // < 10 
        {
            // Current Weather-Main Category
            string main = PlayerPrefs.GetString("WeatherMain", "Clear");
            string fallbackKw = WeatherService.GetDefaultKeyword(main);

            // Avoid duplicate keyword
            // If the random word is exactly the same as the default word, the condition "fallbackKw != keyword" will prevent duplicate requests. 
            // If there are still less than 10 items after restocking, the actual quantity will be displayed without causing a crash.
            if (fallbackKw != keyword)
            {
                string fbURL = $"https://api.vam.ac.uk/v2/objects/search" +
                               $"?q={UnityWebRequest.EscapeURL(fallbackKw)}" +
                               $"&image_exists=true&page_size={PAGE_SIZE}&responseGroup=full";

                RootVA fb = null;
                yield return StartCoroutine(YieldJson<RootVA>(fbURL + "&page=1", r => fb = r));

                if (fb != null)
                {
                    var extra = fb.records
                                  .Where(r => !string.IsNullOrEmpty(r._primaryImageId))
                                  .ToList();
                    Shuffle(extra);          // Guarantee that restocking is also random
                    list.AddRange(extra);
                }
            }
        }
        // End fallback logic

        if (list.Count > WANT) list = list.GetRange(0, WANT);

        // Find all available ArtFrame objects in scene
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrame")
                             .OrderBy(g => g.name)
                             .Select(g => g.GetComponent<ArtFrame>())
                             .Take(WANT)
                             .ToArray();

        // Download and assign textures to each frame
        for (int i = 0; i < frames.Length && i < list.Count; i++)
        {
            string imgUrl = $"https://framemark.vam.ac.uk/collections/{list[i]._primaryImageId}/full/400,/0/default.jpg";
            yield return StartCoroutine(SetTexture(frames[i], imgUrl));

            // Set title by checking several fields in order
            frames[i].title = FirstNonEmpty("(object)",
            list[i]._primaryTitle,
            list[i].title,
            list[i]._primaryObjectName,
            list[i].objectType
            );


        }
        if (statusText) statusText.text = "Done";
        if (statusText) statusText.gameObject.SetActive(false);
    }

    // Manual Reload via UI
    public void ReloadFromPrefs()
    {
        StopAllCoroutines();
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");
        StartCoroutine(LoadWeatherGallery(kw));
        FindObjectOfType<WeatherKeywordBanner>()?.UpdateKeyword(kw);
    }

    // Helper methods
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

        // Save hi-res info to frame for later use
        frame.hiResUrl = url;
        frame.hiTex = tex;
    }

    void Shuffle<T>(IList<T> a) { for (int i = a.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); } }

    /* ---------- JSON ---------- */
    [System.Serializable] public class Info { public int record_count; }
    [System.Serializable]
    public class RecordVA
    {
        public string _primaryImageId, _primaryTitle, title, _primaryObjectName, objectType;
    }
    [System.Serializable] public class RootVA { public Info info; public RecordVA[] records; }

    static string FirstNonEmpty(string fallback, params string[] ss)
    {
        foreach (var s in ss)
            if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        return fallback;
    }


}