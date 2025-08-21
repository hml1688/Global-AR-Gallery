using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FloatingPanel : MonoBehaviour
{
    [Header("UI element")]
    public GameObject configPanel;               // Reference to the configuration panel
    public TMP_Dropdown regionDropdown;          // Region dropdown menu
    public TMP_InputField yearFromInput;         // Input field for start year
    public TMP_InputField yearToInput;           // Input field for end year
    public Button toggleButton;                  // ☰ button for toggling panel

    // Managers for both museum data source
    public GalleryManagerVA vaManager;
    public GalleryManagerHarvard harvardManager;

    // 1. Initialize the panel with current filter values on scene start.
    void Start()
    {
        // Load saved filter values (or fallback to defaults)
        string region = PlayerPrefs.GetString("region", "Europe");
        int from = PlayerPrefs.GetInt("yearFrom", 1500);
        int to = PlayerPrefs.GetInt("yearTo", 1900);

        // Set dropdown to match saved region
        int idx = regionDropdown.options.FindIndex(o => o.text == region);
        if (idx >= 0) regionDropdown.value = idx;

        // Set year input fields
        yearFromInput.text = from.ToString();
        yearToInput.text = to.ToString();

        // Hide the configuration panel by default
        configPanel.SetActive(false);
    }

    // Toggle the visibility of the configuration panel (☰ / ✕ button)
    public void TogglePanel()
    {
        configPanel.SetActive(!configPanel.activeSelf);
    }

    // Apply button: save new filter values and reload galleries.
    public void OnApply()
    {
        // Validate year input fields
        if (!int.TryParse(yearFromInput.text, out int from) ||
            !int.TryParse(yearToInput.text, out int to))
            return;

        // Save filter values to PlayerPrefs
        string region = regionDropdown.options[regionDropdown.value].text;
        PlayerPrefs.SetString("region", region);
        PlayerPrefs.SetInt("yearFrom", Mathf.Min(from, to));
        PlayerPrefs.SetInt("yearTo", Mathf.Max(from, to));

        // Close panel and reload data from both sources
        configPanel.SetActive(false);

        // Simultaneously refresh two museums
        if (vaManager) vaManager.ReloadGallery();
        if (harvardManager) harvardManager.ReloadGallery();
    }

    // Refresh button: fetch a new batch of exhibits using current filters.
    public void OnRefresh()
    {
        // Keep current filters, but reload artwork selection
        if (vaManager) vaManager.ReloadGallery();
        if (harvardManager) harvardManager.ReloadGallery();
    }

    // Close the configuration panel without applying changes.
    public void OnClose()
    {
        configPanel.SetActive(false);
    }

}