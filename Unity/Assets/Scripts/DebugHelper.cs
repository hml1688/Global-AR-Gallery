using UnityEngine;
using TMPro;

public class DebugHelper : MonoBehaviour
{
    static TextMeshProUGUI debugText;
    static float showUntil;

    void Awake()
    {
        debugText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (debugText && Time.time > showUntil)
            debugText.text = "";   // 超时清除
    }

    public static void Show(string msg, float duration = 2f)
    {
        if (debugText)
        {
            debugText.text = msg;
            showUntil = Time.time + duration;
        }
    }
}
