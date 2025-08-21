using UnityEngine;
using System.Collections.Generic;

public class WeatherEffectController : MonoBehaviour
{
    List<GameObject> effects = new(); 

    Dictionary<string, string> map = new()
    {
        {"Clear","FX_Sun"},
        {"Clouds","FX_Cloud"},
        {"Rain","FX_Rain"},
        {"Snow","FX_Snow"},
        {"Fog","FX_Sand"},
        {"Dust","FX_Sand"},
        {"Squall","FX_Sand"},
        {"Thunderstorm","FX_Rain"}
    };

    void Awake()     
    {
        effects.Clear();
        foreach (Transform child in transform) effects.Add(child.gameObject);
    }

    public void ShowWeather(string main)
    {
        foreach (var go in effects) go.SetActive(false);

        if (map.TryGetValue(main, out var fxName))
        {
            var fx = effects.Find(e => e.name == fxName);
            if (fx) fx.SetActive(true);
        }
        Debug.Log($"Weather = {main} → FX = {fxName}");
    }
}

