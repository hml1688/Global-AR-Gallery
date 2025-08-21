using UnityEngine;
using TMPro;

public class WeatherKeywordBanner : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI keywordText;

    void Start()
    {
        // The safest option: Reading from PlayerPrefs
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");

        keywordText.text = $"Keyword: <b>{kw}</b>";
    }

    // Allow GalleryManager to be called during the refresh process to perform real-time updates
    public void UpdateKeyword(string newKw)
    {
        keywordText.text = $"Keyword: <b>{newKw}</b>";
    }
}
