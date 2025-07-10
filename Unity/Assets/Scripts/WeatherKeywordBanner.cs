using UnityEngine;
using TMPro;

public class WeatherKeywordBanner : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI keywordText;

    void Start()
    {
        // ① 最保险：从 PlayerPrefs 读取
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");

        keywordText.text = $"Keyword: <b>{kw}</b>";
    }

    /// <summary>允许 GalleryManager 在刷新时调用，实时更新</summary>
    public void UpdateKeyword(string newKw)
    {
        keywordText.text = $"Keyword: <b>{newKw}</b>";
    }
}
