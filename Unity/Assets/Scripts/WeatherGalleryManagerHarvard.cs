using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;


public class WeatherGalleryManagerHarvard : MonoBehaviour
{
    [Header("Dependencies")]
    public Material pictureMat;   // Unlit material template for images
    public TextMeshProUGUI statusText;    // Optional loading status text
    [SerializeField] string apiKey = "d54e083e-a267-40e4-8d55-f1259589be3b";

    const int WANT = 10, PAGE_SIZE = 100;    // Target number of artworks and API page size

    // Main entry coroutine to load weather-driven artworks based on a keyword
    public IEnumerator LoadWeatherGallery(string kw)
    {
        if (statusText) statusText.text = "Harvard Loading…";

        // Build base API URL with query keyword
        const string FIELDS = "primaryimageurl,secureimageurl,title";
        string baseURL = $"https://api.harvardartmuseums.org/object" +
                 $"?apikey={apiKey}&size={PAGE_SIZE}&hasimage=1" +
                 $"&q=title:{UnityWebRequest.EscapeURL(kw)}&fields={FIELDS}";

        // Request first page to get total page count
        RootHAM first = null;
        yield return StartCoroutine(GetJson<RootHAM>(baseURL + "&page=1", r => first = r));
        if (first == null) yield break;

        int pages = Mathf.Max(1, first.info.pages);
        int rnd = Random.Range(1, pages + 1);

        RootHAM data = first;
        if (rnd != 1)
            yield return StartCoroutine(GetJson<RootHAM>(baseURL + $"&page={rnd}", r => data = r));

        var list = data.records.Where(r => !string.IsNullOrEmpty(r.primaryimageurl)).ToList();
        Shuffle(list);

        // Fallback logic: fetch extra artworks if not enough
        if (list.Count < WANT)
        {
            string main = PlayerPrefs.GetString("WeatherMain", "Clear");
            string fallbackKw = WeatherService.GetDefaultKeyword(main);

            if (fallbackKw != kw)      // avoid repetition
            {
                string fbURL = $"https://api.harvardartmuseums.org/object" +
                               $"?apikey={apiKey}&hasimage=1&size={PAGE_SIZE}&sort=random" +
                               $"&q=title:{UnityWebRequest.EscapeURL(fallbackKw)}" +
                               $"&fields={FIELDS}";

                RootHAM fb = null;
                yield return StartCoroutine(GetJson<RootHAM>(fbURL + "&page=1", r => fb = r));
                if (fb != null)
                {
                    var extra = fb.records
                                  .Where(r => !string.IsNullOrEmpty(r.primaryimageurl))
                                  .ToList();
                    Shuffle(extra);
                    list.AddRange(extra);
                }
            }
        }

        // Trim to max count
        if (list.Count > WANT) list = list.GetRange(0, WANT);

        // Get references to ArtFrameHarvard objects
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrameHarvard")
                             .OrderBy(g => g.name)
                             .Select(g => g.GetComponent<ArtFrame>())
                             .Take(WANT)
                             .ToArray();

        // Download and apply images to frames
        for (int i = 0; i < frames.Length && i < list.Count; i++)
        {
            // Use secure image URL if available, fallback to primaryimageurl
            string img = !string.IsNullOrEmpty(list[i].secureimageurl)
                         ? list[i].secureimageurl
                         : list[i].primaryimageurl;

            if (string.IsNullOrEmpty(img)) continue;

            // Force HTTPS if protocol is http
            if (img.StartsWith("http:"))
                img = "https:" + img.Substring(5);

            // Downscale resolution to 400px width
            img = img.Replace("/full/full/0/", "/full/400,/0/");

            yield return StartCoroutine(SetTexture(frames[i], img));
            frames[i].title = list[i].title ?? "(object)";
        }

        if (statusText) statusText.text = "Done";
        if (statusText) statusText.gameObject.SetActive(false);
    }

    // Reload gallery using the keyword stored in PlayerPrefs
    public void ReloadFromPrefs()
    {
        StopAllCoroutines();
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");
        StartCoroutine(LoadWeatherGallery(kw));
        FindObjectOfType<WeatherKeywordBanner>()?.UpdateKeyword(kw);
    }

    // Generic JSON fetcher using UnityWebRequest
    IEnumerator GetJson<T>(string url, System.Action<T> cb)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { Debug.Log(req.error); yield break; }
        cb(JsonUtility.FromJson<T>(req.downloadHandler.text));
    }

    // Downloads a texture and applies it to an ArtFrame
    IEnumerator SetTexture(ArtFrame f, string u)
    {
        using UnityWebRequest r = UnityWebRequestTexture.GetTexture(u);
        yield return r.SendWebRequest();
        if (r.result != UnityWebRequest.Result.Success) yield break;
        Texture t = DownloadHandlerTexture.GetContent(r);
        f.paintingRenderer.sharedMaterial = new Material(pictureMat);
        f.SetTexture(t);
        f.hiResUrl = u;
        f.hiTex = t;   // Cache the texture for reuse
    }
    void Shuffle<T>(IList<T> a) { for (int i = a.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); } }

    /* --- JSON --- */
    [System.Serializable] public class Info { public int pages; }
    [System.Serializable]
    public class Record
    {
        public string primaryimageurl;
        public string secureimageurl;
        public string title;
    }

    [System.Serializable] public class RootHAM { public Info info; public List<Record> records; }
}