using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class InfoPanel : MonoBehaviour
{
    public GameObject canvasRoot;    // Root of the panel canvas
    public RawImage img;     // UI element for displaying image
    public AspectRatioFitter fitter;     // Maintains image aspect ratio
    public TextMeshProUGUI titleValue, dateValue, makerValue, placeValue, museumValue, loadingHint;

    static InfoPanel inst;
    void Awake() => inst = this;      // Singleton instance

    // Entry point to show the info panel with metadata and image from ArtFrame
    public static void Show(ArtFrame f) => inst?.StartCoroutine(inst.ShowRoutine(f));

    IEnumerator ShowRoutine(ArtFrame f)
    {
        // Show panel UI
        canvasRoot.SetActive(true);
        loadingHint.gameObject.SetActive(true);
        img.texture = null;

        // Populate metadata fields
        titleValue.text = f.title;
        dateValue.text = f.date;
        makerValue.text = string.IsNullOrWhiteSpace(f.maker) ? "Unknown" : f.maker;
        placeValue.text = f.place;

        // Determine artwork source by tag
        if (museumValue)
        {
            string museum = f.gameObject.CompareTag("ArtFrameHarvard")
                            ? "Harvard Art Museum"
                            : "V&A Museum";
            museumValue.text = museum;
        }

        // 1. Load high-resolution image if not cached
        if (f.hiTex == null)
        {
            UnityWebRequest req = UnityWebRequestTexture.GetTexture(f.hiResUrl);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                f.hiTex = DownloadHandlerTexture.GetContent(req);
        }

        // 2. Apply loaded texture and maintain correct aspect ratio
        if (f.hiTex)
        {
            img.texture = f.hiTex;

            // Maintain correct height/width ratio
            float ratio = (float)f.hiTex.width / f.hiTex.height;
            fitter.aspectRatio = ratio;                 // Make "Height" equal to "Width" divided by "ratio".
        }

        loadingHint.gameObject.SetActive(false);
    }

    // Hide the panel when user closes it
    public void Hide()
    {
        DebugHelper.Show("Close Triggered");
        canvasRoot.SetActive(false);
    }

}