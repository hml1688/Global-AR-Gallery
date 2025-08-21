using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

// WeatherGalleryScene-specific information panel: Displays only the work images, titles, and the source museum.
public class InfoPanelWeather : MonoBehaviour
{
    [Header("UI References")]
    public GameObject canvasRoot;     // Root canvas for toggling visibility
    public RawImage img;            // UI image component for artwork
    public AspectRatioFitter fitter;       // Maintains aspect ratio of image
    public TextMeshProUGUI titleValue;     // Artwork title text field
    public TextMeshProUGUI museumValue;    // Text field showing the museum name
    public TextMeshProUGUI loadingHint;    // Optional "Loading..." hint text

    // Singleton instance for easy static access
    static InfoPanelWeather inst;
    void Awake() => inst = this;

    // Public entry point for showing the weather info panel
    public static void Show(ArtFrame f)
    {
        if (inst != null) inst.StartCoroutine(inst.ShowRoutine(f));
    }

    // Coroutine for downloading and displaying high-res image and metadata
    IEnumerator ShowRoutine(ArtFrame f)
    {
        canvasRoot.SetActive(true);
        if (loadingHint) loadingHint.gameObject.SetActive(true);
        img.texture = null;

        // Populate artwork metadata
        titleValue.text = f.title;
        museumValue.text = f.gameObject.CompareTag("ArtFrameHarvard")
                           ? "Harvard Art Museums"
                           : "V&A Museum";

        // Future: Add tag detection for more museums if needed

        // Download high-resolution image (if not already cached)
        if (f.hiTex == null)
        {
            using UnityWebRequest req = UnityWebRequestTexture.GetTexture(f.hiResUrl);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                f.hiTex = DownloadHandlerTexture.GetContent(req);
        }

        // Apply texture and maintain correct aspect ratio
        if (f.hiTex)
        {
            img.texture = f.hiTex;
            fitter.aspectRatio = (float)f.hiTex.width / f.hiTex.height;
        }

        if (loadingHint) loadingHint.gameObject.SetActive(false);
    }

    // Close the panel via UI button
    public void Hide() => canvasRoot.SetActive(false);
}
