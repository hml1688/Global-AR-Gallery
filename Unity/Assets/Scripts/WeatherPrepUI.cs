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
    // Check either of the two permissions at the same time; either one will suffice.
    if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) &&
        !Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
    {
        // Apply for two permissions at once, and have the system display the "Precise / Approximate" option.
        Permission.RequestUserPermissions(new[]
        {
            Permission.FineLocation,
            Permission.CoarseLocation
        });
        return;   // Wait for the user to make a choice.
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
                tempText.text = $"Temperature: {weatherData.main.temp}°C";
                weatherText.text = $"Weather: {weatherData.weather[0].description}";
                windText.text = $"Wind Speed: {weatherData.wind.speed} m/s";
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