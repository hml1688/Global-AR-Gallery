using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuUI : MonoBehaviour
{
    public TMP_Dropdown dropdownRegion;
    public TMP_InputField inputFrom;
    public TMP_InputField inputTo;
    public TextMeshProUGUI errorText;

    public void OnEnterGallery()
    {
        string region = dropdownRegion.options[dropdownRegion.value].text;
        string fromText = inputFrom.text.Trim();
        string toText = inputTo.text.Trim();

        if (!int.TryParse(fromText, out int from) || !int.TryParse(toText, out int to))
        {
            errorText.text = "Please enter valid years.";
            errorText.gameObject.SetActive(true);
            return;
        }

        errorText.gameObject.SetActive(false);
        PlayerPrefs.SetString("region", region);
        PlayerPrefs.SetInt("yearFrom", Mathf.Min(from, to));
        PlayerPrefs.SetInt("yearTo", Mathf.Max(from, to));

        SceneManager.LoadScene("AR Scene Interface");
    }
}
