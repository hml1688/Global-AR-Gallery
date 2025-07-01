using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections; 

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class WeatherPrepUI : MonoBehaviour
{
    public TextMeshProUGUI statusText, errorText;
    public GameObject enterBtn;

    [Header("Weather Info Panel")]
    public TextMeshProUGUI locationText, tempText, weatherText, windText;

    void Start()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    // 同时检查两种权限，只要其一即可
    if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) &&
        !Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
    {
        // 一次性申请两个权限，让系统弹出“Precise / Approximate”选项
        Permission.RequestUserPermissions(new[]
        {
            Permission.FineLocation,
            Permission.CoarseLocation
        });
        return;   // 先等待用户选择
    }
#endif
    StartCoroutine(BeginWeatherLoad());
}

void OnApplicationFocus(bool focus)
{
#if UNITY_ANDROID && !UNITY_EDITOR
    if (focus && (Permission.HasUserAuthorizedPermission(Permission.FineLocation) ||
                  Permission.HasUserAuthorizedPermission(Permission.CoarseLocation)))
    {
        StartCoroutine(BeginWeatherLoad());
    }
#endif
}


IEnumerator BeginWeatherLoad()
{
    yield return WeatherService.FetchKeyword(
        onDone: (kw, weatherData) =>
        {
            statusText.text = $"Weather keyword: <b>{kw}</b>";
            enterBtn.SetActive(true);

            locationText.text = $"Location: {weatherData.name}";
            tempText.text     = $"Temperature: {weatherData.main.temp}°C";
            weatherText.text  = $"Weather: {weatherData.weather[0].description}";
            windText.text     = $"Wind Speed: {weatherData.wind.speed} m/s";
        },
        onError: msg =>
        {
            errorText.text = msg + "\nLoading default gallery.";
            errorText.gameObject.SetActive(true);
            PlayerPrefs.SetString("WeatherKeyword", "sun");
            enterBtn.SetActive(true);
        });
}


    public void OnEnterGallery()
    {
        SceneManager.LoadScene("WeatherGalleryScene");
    }
}
