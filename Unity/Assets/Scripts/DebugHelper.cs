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
            debugText.text = "";   // Timeout clearance
    }

    public static void Show(string msg, float duration = 3f)
    {
        if (debugText)
        {
            debugText.text = msg;
            showUntil = Time.time + duration;
        }
    }
}
