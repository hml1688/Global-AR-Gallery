using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class WeatherService
{
    private const string ApiKey = "f7101260f8028886f1654c8a8b3a94b4";
    private const float Timeout = 5f;
    // If the positioning fails or the accuracy is poor (more than 500 meters), switch to using fixed coordinates or city names and request OpenWeather.
    private const string DefaultCity = "London,UK";
    private const double AccuracyThresholdMeters = 500.0;


    private static readonly System.Random rng = new System.Random();

    // Normalization function
    private static string NormalizeWeatherMain(string main)
    {
        switch (main)
        {
            case "Drizzle": return "Rain";
            case "Mist":
            case "Haze": return "Fog";
            case "Smoke":
            case "Dust":
            case "Sand":
            case "Ash": return "Dust";
            case "Squall":
            case "Tornado": return "Squall";
            default: return main;
        }
    }

    // Weather → Keyword Mapping
    private static readonly Dictionary<string, string[]> WeatherMap = new()
    {
        // 800 Clear
        ["Clear"] = new[]
    {
        "sun", "sunshine", "sunlight", "bright", "golden", "blue sky",
        "warm", "summer", "picnic", "breeze", "sunlit", "glow", "dawn", "sunset"
    },

        // 80x Clouds
        ["Clouds"] = new[]
    {
        "cloud", "gray", "soft light", "moody", "shadow",
        "nimbus", "billowing", "pensive", "quiet"
    },

        // 5xx Rain  (Mistfall has been merged into the subset)
        ["Rain"] = new[]
    {
        "rain", "rainy", "wet", "umbrella", "puddle",
        "reflection", "ripple", "splatter", "storm clouds"
    },

        // 2xx Thunderstorm
        ["Thunderstorm"] = new[]
    {
        "thunder", "lightning", "storm", "tempest", "electrical", "flash",
        "dark sky", "dramatic", "roar", "black clouds", "energy", "explosive", "bolt"
    },

        // 6xx Snow
        ["Snow"] = new[]
    {
        "snow", "snowy", "frost", "ice","winter", "white", "powder",
        "icy", "stillness", "blanket", "crystal", "glacial", "drift"
    },

        // Fog (= Mist + Fog + Haze)
        ["Fog"] = new[]
    {
        "fog", "mist", "foggy", "blurred", "veil", "twilight", "shrouded",
        "obscured", "mysterious", "low cloud", "silvery"
    },

        // Dust (= Smoke + Dust + Sand + Ash)
        ["Dust"] = new[]
    {
        "dust", "smoke", "ash", "sand", "sandy", "desert", "sepia", "grit", "volcano"
    },

        // Squall (= Squall + Tornado + violent wind)
        ["Squall"] = new[]
    {
        "angry", "squall", "gale", "gust", "tornado", "hurricane", "typhoon", "storm"
    }
    };

    // Each "fallback keyword" of the "weather main" category
    private static readonly Dictionary<string, string> DefaultKeyword = new()
    {
        ["Clear"] = "sun",
        ["Clouds"] = "cloud",
        ["Rain"] = "rain",
        ["Thunderstorm"] = "storm",
        ["Snow"] = "snow",
        ["Fog"] = "fog",
        ["Dust"] = "sand",
        ["Squall"] = "storm"
    };

    public static string GetDefaultKeyword(string main) =>
        DefaultKeyword.TryGetValue(main, out var k) ? k : "sun";


    // Obtain the key weather information and return the complete weather data
    /// <param name="onDone"> call back：(keyword, weatherData)</param>
    /// <param name="onError"> error call back：string msg</param>
    public static IEnumerator FetchKeyword(
    Action<string, WeatherResponse> onDone,
    Action<string> onError)
    {
        // Check GPS permission & status
        bool gpsAvailable = Input.location.isEnabledByUser;

        if (gpsAvailable)
        {
            Input.location.Start(500f, 500f);   // Allow 0.5 km accuracy
            int wait = 5;
            while (Input.location.status == LocationServiceStatus.Initializing && wait-- > 0)
                yield return new WaitForSeconds(1);

            // If it does not enter the "Running" state, it is considered unavailable -> fallback
            gpsAvailable = Input.location.status == LocationServiceStatus.Running;
        }

        // Construct URL
        string url = gpsAvailable
            ? $"https://api.openweathermap.org/data/2.5/weather?lat={Input.location.lastData.latitude}&lon={Input.location.lastData.longitude}&appid={ApiKey}&units=metric"
            : $"https://api.openweathermap.org/data/2.5/weather?q={DefaultCity}&appid={ApiKey}&units=metric";

        if (!gpsAvailable)
        {
            onError?.Invoke("Location permission denied. Using default location: London");
        }

        // Request the weather
        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = (int)Timeout;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke("Weather request failed.");
            yield break;
        }

        // Parse JSON
        string json = req.downloadHandler.text;
        WeatherResponse data = JsonUtility.FromJson<WeatherResponse>(json);
        string rawMain = data.weather.Length > 0 ? data.weather[0].main : "Clear";

        // Mapping keyword
        string main = NormalizeWeatherMain(rawMain);
        if (!WeatherMap.TryGetValue(main, out var list))
            list = WeatherMap["Clear"];

        // A random keyword
        string keyword = list[rng.Next(list.Length)];
        PlayerPrefs.SetString("WeatherKeyword", keyword);
        PlayerPrefs.SetString("WeatherMain", main);      // Store the result of NormalizeWeatherMain in main for easy reading by Manager.

        // Callback return
        onDone?.Invoke(keyword, data);
    }

}

// Weather JSON corresponding data structure
[Serializable]
public class WeatherResponse
{
    public WeatherDesc[] weather;
    public MainData main;
    public WindData wind;
    public string name;      // city name
}

[Serializable]
public class WeatherDesc
{
    public string main;        // e.g. "Rain"
    public string description; // e.g. "moderate rain"
}

[Serializable]
public class MainData
{
    public float temp;         // Celsius
    public float feels_like;
}

[Serializable]
public class WindData
{
    public float speed;        // m/s
}
