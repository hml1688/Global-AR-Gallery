using UnityEngine;
using TMPro;
using UnityEngine.UI;   // 若想给按钮加透明度动画，可用

public class FloatingPanel : MonoBehaviour
{
    [Header("UI element")]
    public GameObject configPanel;               // 拖 ConfigPanel
    public TMP_Dropdown regionDropdown;          // 拖 dropdown
    public TMP_InputField yearFromInput;         // 拖 input
    public TMP_InputField yearToInput;           // 拖 input
    public Button toggleButton;                  // ☰ 按钮（可选）
    // ——改成同时引用两种 Manager——
    public GalleryManagerVA       vaManager;
    public GalleryManagerHarvard  harvardManager;

    /* ---------- 1. 场景开始时，把输入框填成当前筛选 ---------- */
    void Start()
    {
        // 若 PlayerPrefs 尚未写入，用 Menu 的默认值
        string region = PlayerPrefs.GetString("region", "Europe");
        int from = PlayerPrefs.GetInt("yearFrom", 1500);
        int to   = PlayerPrefs.GetInt("yearTo",   1900);

        // Dropdown 选项同步
        int idx = regionDropdown.options.FindIndex(o => o.text == region);
        if (idx >= 0) regionDropdown.value = idx;

        yearFromInput.text = from.ToString();
        yearToInput.text   = to.ToString();

        // 关闭配置窗
        configPanel.SetActive(false);
    }

    /* ---------- 2. ☰ / ✕ 共用：开关面板 ---------- */
    public void TogglePanel()
    {
        configPanel.SetActive(!configPanel.activeSelf);
    }

    /* ---------- 3. 点击 Apply：保存新筛选并刷新画作 ---------- */
    public void OnApply()
    {
        // 年份校验
        if (!int.TryParse(yearFromInput.text, out int from) ||
            !int.TryParse(yearToInput.text,   out int to))
            return;

        // 写入 PlayerPrefs
        string region = regionDropdown.options[regionDropdown.value].text;
        PlayerPrefs.SetString("region", region);
        PlayerPrefs.SetInt   ("yearFrom", Mathf.Min(from, to));
        PlayerPrefs.SetInt   ("yearTo",   Mathf.Max(from, to));

        // 关闭面板 + 重新抓取贴图
        configPanel.SetActive(false);
        // ——同时刷新两家博物馆——
        if (vaManager)       vaManager.ReloadGallery();
        if (harvardManager)  harvardManager.ReloadGallery();
    }

    /* ---------- 4. 单独的 Refresh 按钮 ---------- */
    public void OnRefresh()
    {
        // 不改筛选，只换 20 张
        if (vaManager)       vaManager.ReloadGallery();
        if (harvardManager)  harvardManager.ReloadGallery();
    }

    // 5.单独的关闭函数（不刷新）
    public void OnClose()
    {
        configPanel.SetActive(false);
    }

}