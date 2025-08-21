using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;       // Parse JSON
using System.Linq;                     // OrderBy / LINQ

public class GalleryManagerVA : MonoBehaviour
{
    [Header("Picture material (Unlit/Texture)")]
    public Material pictureMat;

    [Header("Loading text (TMP) – optional")]
    public TextMeshProUGUI statusText;

    // Region to Country list
    readonly Dictionary<string, string[]> REGION_COUNTRIES = new()
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

    // Constant
    const int WANT = 10;        // Number of artworks to display
    const int PAGE_SIZE = 100;  // API page size
    const int MAX_COUNTRY = 8;  // Max number of countries to poll

    // Entry
    void Start() => StartCoroutine(LoadGallery());

    // Main loading coroutine
    IEnumerator LoadGallery()
    {
        // Load filter from PlayerPrefs or use default values
        string region = PlayerPrefs.GetString("region", "Europe");
        int fromY = PlayerPrefs.GetInt("yearFrom", 1500);
        int toY = PlayerPrefs.GetInt("yearTo", 1900);

        if (statusText) statusText.text = $"V&A Loading {region}  {fromY}–{toY} …";

        // Find all 10 ArtFrame objects tagged for V&A
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrame")
                                      .OrderBy(g => g.name)
                                      .Select(g => g.GetComponent<ArtFrame>())
                                      .ToArray();
        if (frames.Length == 0)
        {
            Debug.LogError("No ArtFrame found in scene!");
            yield break;
        }

        // Clear old textures
        foreach (var f in frames)
            f.hiTex = null;

        // Fetch 1 page per country, then pool and select artworks evenly
        var buckets = new Dictionary<string, List<JToken>>();
        var countries = new List<string>(REGION_COUNTRIES[region]);
        Shuffle(countries);

        // Fetch artworks for each country (up to MAX_COUNTRY)
        foreach (var c in countries.Take(MAX_COUNTRY))
        {
            string url = BuildURL("q_place_name", c, fromY, toY);
            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            { Debug.Log(req.error); continue; }

            JToken j = JToken.Parse(req.downloadHandler.text);
            var list = new List<JToken>();

            foreach (var rec in j["records"] ?? new JArray())
            {
                if (rec["_images"]?["_primary_thumbnail"] == null) continue;   // Thumbnail images are necessary.
                if (!PlaceMatches(rec, c)) continue;
                list.Add(rec);
            }
            if (list.Count > 0) buckets[c] = list;
        }

        // Evenly select artworks from country buckets
        var chosen = new List<JToken>();
        var seen = new HashSet<string>();

        while (chosen.Count < WANT)
        {
            bool moved = false;
            foreach (var kv in buckets)
            {
                var arr = kv.Value;
                // Skip the duplicates
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
            if (!moved) break;   // All the buckets are empty.
        }

        // Apply selected artworks to canvas
        Shuffle(chosen);

        int loaded = 0;
        for (int i = 0; i < frames.Length && i < chosen.Count; i++)
        {
            var rec = chosen[i];

            // Build image URL
            string url;
            var iiif = rec["_images"]?["_iiif_image_base_url"]?.ToString();
            if (!string.IsNullOrEmpty(iiif))
                url = iiif + "full/!1024,1024/0/default.jpg"; // 1024px
            else
                url = rec["_images"]["_primary_thumbnail"]!.ToString(); // Return the thumbnail image

            // Download texture
            UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(url);
            yield return texReq.SendWebRequest();
            if (texReq.result != UnityWebRequest.Result.Success) continue;

            Texture tex = DownloadHandlerTexture.GetContent(texReq);

            // Assign material and texture
            frames[i].paintingRenderer.sharedMaterial = new Material(pictureMat);
            frames[i].SetTexture(tex);

            // Record metadata
            frames[i].hiResUrl = url;
            frames[i].title =
            !string.IsNullOrEmpty(rec["_primaryTitle"]?.ToString()) ? rec["_primaryTitle"].ToString()
            : !string.IsNullOrEmpty(rec["objectType"]?.ToString()) ? rec["objectType"].ToString()
            : !string.IsNullOrEmpty(rec["title"]?.ToString()) ? rec["title"].ToString()
            : "(object)";
            frames[i].date = rec["_primaryDate"]?.ToString() ?? "";
            frames[i].maker = rec["_primaryMaker"]?["name"]?.ToString() ?? "";
            frames[i].place = PlaceMatches(rec, "") ? rec["_primaryPlace"]?.ToString()
                                                : rec["placeOfOrigin"]?.ToString() ?? "";

            loaded++;
            if (statusText) statusText.text = $"V&A Loaded {loaded}/{Mathf.Min(WANT, chosen.Count)}";
        }


        // Done
        if (statusText) statusText.text = "Done";
        if (statusText) statusText.gameObject.SetActive(false);  //  Hide
    }

    // Utility function
    // Helper: Build V&A API URL
    static string BuildURL(string param, string val, int f, int t) =>
        $"https://api.vam.ac.uk/v2/objects/search?{param}={UnityWebRequest.EscapeURL(val)}" +
        $"&year_made_from={f}&year_made_to={t}&images_exist=1&page_size={PAGE_SIZE}";

    // Helper: Match record to country
    static bool PlaceMatches(JToken rec, string country)
    {
        string place = (rec["_primaryPlace"] ?? rec["placeOfOrigin"] ?? "").ToString().ToLower();
        string kw = country.ToLower();
        return place == kw || place.Contains(kw);
    }

    // Shuffle a list
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Public interface for external UI buttons to refresh gallery
    public void ReloadGallery()
    {
        StopAllCoroutines();
        StartCoroutine(WaitAndLoad());   // Wait for ArtFrame
    }

    IEnumerator WaitAndLoad()
    {
        if (statusText) { statusText.gameObject.SetActive(true); statusText.text = "Loading…"; }

        // Check every 0.1 second instead of remaining stuck.
        // Wait until 10 ArtFrame objects are ready
        while (GameObject.FindGameObjectsWithTag("ArtFrame").Length < 10)
            yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(LoadGallery());
    }


}