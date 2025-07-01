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

    // 天气 → 关键词映射
    private static readonly Dictionary<string, string[]> WeatherMap = new()
    {
        { "Clear",        new[]{ "sun", "bright", "summer" } },
        { "Clouds",       new[]{ "cloud", "overcast" } },
        { "Rain",         new[]{ "rain", "umbrella", "storm" } },
        { "Thunderstorm", new[]{ "lightning", "storm" } },
        { "Snow",         new[]{ "snow", "winter", "white" } },
        { "Fog",          new[]{ "fog", "mist" } }
        // 其它未列出的情况自动 fall back
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
    string cond = data.weather.Length > 0 ? data.weather[0].main : "Clear";

    // ---------- 5. 映射关键词 ----------
    if (!WeatherMap.TryGetValue(cond, out var list))
        list = WeatherMap["Clear"];

    string keyword = list[rng.Next(list.Length)];
    PlayerPrefs.SetString("WeatherKeyword", keyword);

    // ---------- 6. 回调返回 ----------
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

