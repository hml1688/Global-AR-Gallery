using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class WeatherService
{
    // ====== 你自己的 OpenWeather API Key ======
    private const string ApiKey  = "f7101260f8028886f1654c8a8b3a94b4";
    private const float  Timeout = 5f;
    // 定位失败或精度差 > 500 m 时，改用 固定坐标或城市名 请求 OpenWeather
    private const string DefaultCity  = "London,UK";
    private const double AccuracyThresholdMeters = 500.0;


    private static readonly System.Random rng = new System.Random();

    /* ──── A. 归一化函数 ──── */
    private static string NormalizeWeatherMain(string main)
{
    switch (main)
    {
        case "Drizzle": return "Rain";
        case "Mist":
        case "Haze":   return "Fog";
        case "Smoke":
        case "Dust":
        case "Sand":
        case "Ash":    return "Dust";
        case "Squall":
        case "Tornado":return "Squall";
        default:       return main;
    }
}

    // 天气 → 关键词映射
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

    // 5xx Rain  (Drizzle 合并为子集)
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


    /// <summary>
    /// 获取天气关键词并返回完整天气数据
    /// </summary>
    /// <param name="onDone">回调：(keyword, weatherData)</param>
    /// <param name="onError">错误回调：string msg</param>
    public static IEnumerator FetchKeyword(
    Action<string, WeatherResponse> onDone,
    Action<string> onError)
{
    // ---------- 1. 判断 GPS 权限 & 状态 ----------
    bool gpsAvailable = Input.location.isEnabledByUser;

    if (gpsAvailable)
    {
        Input.location.Start(500f, 500f);   // 允许 0.5 km 精度
        int wait = 5;
        while (Input.location.status == LocationServiceStatus.Initializing && wait-- > 0)
            yield return new WaitForSeconds(1);

        // 如果没进入 Running，就认定为不可用 —> fallback
    gpsAvailable = Input.location.status == LocationServiceStatus.Running;
    }

    // ---------- 2. 构造 URL ----------
    string url = gpsAvailable
        ? $"https://api.openweathermap.org/data/2.5/weather?lat={Input.location.lastData.latitude}&lon={Input.location.lastData.longitude}&appid={ApiKey}&units=metric"
        : $"https://api.openweathermap.org/data/2.5/weather?q={DefaultCity}&appid={ApiKey}&units=metric";

    if (!gpsAvailable)
    {
        onError?.Invoke("Location permission denied. Using default location: London");
    }

    // ---------- 3. 请求天气 ----------
    using UnityWebRequest req = UnityWebRequest.Get(url);
    req.timeout = (int)Timeout;
    yield return req.SendWebRequest();

    if (req.result != UnityWebRequest.Result.Success)
    {
        onError?.Invoke("Weather request failed.");
        yield break;
    }

    // ---------- 4. 解析 JSON ----------
    string json = req.downloadHandler.text;
    WeatherResponse data = JsonUtility.FromJson<WeatherResponse>(json);
    string rawMain = data.weather.Length > 0 ? data.weather[0].main : "Clear";

    // ---------- 5. 映射关键词 ----------
    string main    = NormalizeWeatherMain(rawMain);
    if (!WeatherMap.TryGetValue(main, out var list))
    list = WeatherMap["Clear"];

    // 6. 随机一个关键词
    string keyword = list[rng.Next(list.Length)];
    PlayerPrefs.SetString("WeatherKeyword", keyword);

    // ---------- 7. 回调返回 ----------
    onDone?.Invoke(keyword, data);
}

}

/* ---------- 天气 JSON 对应的数据结构 ---------- */
[Serializable] public class WeatherResponse
{
    public WeatherDesc[] weather;
    public MainData      main;
    public WindData      wind;
    public string        name;      // 城市 / 地点名
}

[Serializable] public class WeatherDesc
{
    public string main;        // e.g. "Rain"
    public string description; // e.g. "moderate rain"
}

[Serializable] public class MainData
{
    public float temp;         // 摄氏温度
    public float feels_like;
}

[Serializable] public class WindData
{
    public float speed;        // m/s
}
