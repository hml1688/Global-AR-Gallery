using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class WeatherPrepUI : MonoBehaviour
{
    public TextMeshProUGUI statusText, errorText;
    public GameObject enterBtn;

    [Header("Weather Info Panel")]
    public TextMeshProUGUI locationText, tempText, weatherText, windText;

    void Start()
    {
        StartCoroutine(WeatherService.FetchKeyword(
            onDone: (kw, weatherData) =>
            {
                statusText.text = $"Weather keyword: <b>{kw}</b>";
                enterBtn.SetActive(true);

                // 更新面板信息
                locationText.text = $"📍 Location: {weatherData.name}";
                tempText.text     = $"🌡 Temperature: {weatherData.main.temp}°C";
                weatherText.text  = $"☁️ Weather: {weatherData.weather[0].description}";
                windText.text     = $"💨 Wind Speed: {weatherData.wind.speed} m/s";
            },
            onError: msg =>
            {
                errorText.text = msg + "\nLoading default gallery.";
                errorText.gameObject.SetActive(true);
                PlayerPrefs.SetString("WeatherKeyword", "sun");
                enterBtn.SetActive(true);
            }));
    }

    public void OnEnterGallery()
    {
        SceneManager.LoadScene("WeatherGalleryScene");
    }
}
