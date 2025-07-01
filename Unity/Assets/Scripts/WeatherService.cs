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
        Action<string>                  onError)
    {
        // ---------- 1. GPS ----------
        if (!Input.location.isEnabledByUser)
        {
            onError?.Invoke("Location permission denied.");
            yield break;
        }

        Input.location.Start();
        int maxWait = 20;                     // 最多等待 20 秒
        while (Input.location.status == LocationServiceStatus.Initializing &&
               maxWait-- > 0)
        {
            yield return new WaitForSeconds(1);
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            onError?.Invoke("Unable to get GPS.");
            yield break;
        }

        var loc = Input.location.lastData;
        string url =
            $"https://api.openweathermap.org/data/2.5/weather?lat={loc.latitude}&lon={loc.longitude}&appid={ApiKey}&units=metric";

        // ---------- 2. 请求天气 ----------
        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = (int)Timeout;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke("Weather request failed.");
            yield break;
        }

        // ---------- 3. 解析 JSON ----------
        string          json  = req.downloadHandler.text;
        WeatherResponse data  = JsonUtility.FromJson<WeatherResponse>(json);
        string          cond  = data.weather.Length > 0 ? data.weather[0].main : "Clear";

        // ---------- 4. 映射关键词 ----------
        if (!WeatherMap.TryGetValue(cond, out var list))
            list = WeatherMap["Clear"];

        string keyword = list[rng.Next(list.Length)];
        PlayerPrefs.SetString("WeatherKeyword", keyword);

        // ---------- 5. 返回 ----------
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

