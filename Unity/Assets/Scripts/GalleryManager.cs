using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Linq;                     // For LINQ operations

public class GalleryManager : MonoBehaviour
{
    [Header("Picture material (Unlit/Texture)")]
    public Material pictureMat;

    [Header("Loading text (TMP) – optional")]
    public TextMeshProUGUI statusText;

    // Mapping from region names to country lists
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

    // Constants
    const int WANT = 20;        // Number of artworks to display
    const int PAGE_SIZE = 100;  // Number of results per API call
    const int MAX_COUNTRY = 8;  // Max number of countries to query per refresh

    // Entry Point 
    void Start() => StartCoroutine(LoadGallery());

    // Main Coroutine
    IEnumerator LoadGallery()
    {
        // Load region and year filter from PlayerPrefs or use defaults
        string region = PlayerPrefs.GetString("region", "Europe");
        int fromY = PlayerPrefs.GetInt("yearFrom", 1500);
        int toY = PlayerPrefs.GetInt("yearTo", 1900);

        if (statusText) statusText.text = $"Loading {region}  {fromY}–{toY} …";

        // Find all 20 ArtFrame objects in the scene
        ArtFrame[] frames = GameObject.FindGameObjectsWithTag("ArtFrame")
                                      .OrderBy(g => g.name)      // 确保顺序一致
                                      .Select(g => g.GetComponent<ArtFrame>())
                                      .ToArray();
        if (frames.Length == 0)
        {
            Debug.LogError("No ArtFrame found in scene!");
            yield break;
        }

        // Clear previous hiTex references to avoid memory issues
        foreach (var f in frames)
            f.hiTex = null;

        // Fair Distribution Strategy – Pull 1 page per country and balance across
        var buckets = new Dictionary<string, List<JToken>>();
        var countries = new List<string>(REGION_COUNTRIES[region]);
        // Randomize country order
        Shuffle(countries);

        // Parallel request: fetch first page from each country
        foreach (var c in countries.Take(MAX_COUNTRY))
        {
            string url = BuildURL("q_place_name", c, fromY, toY);
            UnityWebRequest req = UnityWebRequest.Get(url);
            // Wait for response
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            { Debug.Log(req.error); continue; }

            JToken j = JToken.Parse(req.downloadHandler.text);
            var list = new List<JToken>();

            foreach (var rec in j["records"] ?? new JArray())
            {
                // Filter out results without images
                if (rec["_images"]?["_primary_thumbnail"] == null) continue;
                // Ensure place matches country
                if (!PlaceMatches(rec, c)) continue;
                list.Add(rec);
            }
            if (list.Count > 0) buckets[c] = list;
        }

        // Round-robin: Select evenly from each country until target count is reached
        var chosen = new List<JToken>();
        // Avoid duplicates by systemNumber
        var seen = new HashSet<string>();

        while (chosen.Count < WANT)
        {
            bool moved = false;
            foreach (var kv in buckets)
            {
                var arr = kv.Value;

                // Skip if artwork already selected
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
            if (!moved) break;   // All buckets exhausted
        }

        // Download and apply images to frames
        // Randomize final display order
        Shuffle(chosen);

        int loaded = 0;
        for (int i = 0; i < frames.Length && i < chosen.Count; i++)
        {
            var rec = chosen[i];

            // Build image URL
            string url;
            var iiif = rec["_images"]?["_iiif_image_base_url"]?.ToString();
            if (!string.IsNullOrEmpty(iiif))
                url = iiif + "full/!1024,1024/0/default.jpg"; // HD preview
            else
                url = rec["_images"]["_primary_thumbnail"]!.ToString(); // fallback thumbnail

            // Download image
            UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(url);
            yield return texReq.SendWebRequest();
            if (texReq.result != UnityWebRequest.Result.Success) continue;

            // Apply image to ArtFrame
            Texture tex = DownloadHandlerTexture.GetContent(texReq);

            // Use a new material instance for each to avoid texture override
            frames[i].paintingRenderer.sharedMaterial = new Material(pictureMat);
            frames[i].SetTexture(tex);

            // Store high-res metadata
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
            if (statusText) statusText.text = $"Loaded {loaded}/{Mathf.Min(WANT, chosen.Count)}";
        }


        // Finalize
        if (statusText) statusText.text = "Done";
        if (statusText) statusText.gameObject.SetActive(false);  // Auto-hide when done
    }

    // Utility Methods
    static string BuildURL(string param, string val, int f, int t) =>
        $"https://api.vam.ac.uk/v2/objects/search?{param}={UnityWebRequest.EscapeURL(val)}" +
        $"&year_made_from={f}&year_made_to={t}&images_exist=1&page_size={PAGE_SIZE}";

    // Check whether the artwork’s location matches the country keyword
    static bool PlaceMatches(JToken rec, string country)
    {
        string place = (rec["_primaryPlace"] ?? rec["placeOfOrigin"] ?? "").ToString().ToLower();
        string kw = country.ToLower();
        return place == kw || place.Contains(kw);
    }

    // Shuffle a list using Fisher-Yates algorithm
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Reload the gallery with updated filters
    public void ReloadGallery()
    {
        StopAllCoroutines();
        StartCoroutine(WaitAndLoad());   // Wait for ArtFrames before reloading
    }

    IEnumerator WaitAndLoad()
    {
        if (statusText) { statusText.gameObject.SetActive(true); statusText.text = "Loading…"; }

        // Wait until all ArtFrame objects are present in the scene
        while (GameObject.FindGameObjectsWithTag("ArtFrame").Length < 20)
            yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(LoadGallery());
    }


}