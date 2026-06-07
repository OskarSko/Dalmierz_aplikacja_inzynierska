using UnityEngine;
using TMPro;

public class ModeSwitcher : MonoBehaviour
{
    [Header("Logika")]
    public MonoBehaviour arScript;
    public MonoBehaviour rangeFinder;

    [Header("UI")]
    public GameObject arCrosshairUI;
    public GameObject rangeFinderLinesUI;
    public GameObject zoomSliderUI;
    public TextMeshProUGUI buttonText;
    public TextMeshProUGUI distanceText;

    private bool isArMode = true;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        ActivateArMode();
    }

    public void ToggleMode()
    {
        isArMode = !isArMode;
        if (isArMode)
        {
            ActivateArMode();
        }
        else
        {
            ActivateRangeFinder();
        }
    }
    void ActivateArMode()
    {
        arScript.enabled = true;
        rangeFinder.enabled = false;

        arCrosshairUI.SetActive(true);
        rangeFinderLinesUI.SetActive(false);
        zoomSliderUI.SetActive(false);

        buttonText.text = "<b>TRYB DALEKI</b>\n<size=60%>Przełącz</size>";
        if(distanceText != null)
        {
            distanceText.text = "<size=50%><color=#B0B0B0>SKANOWANIE OTOCZENIA...</color></size>";
        }
    }
    void ActivateRangeFinder()
    {
        arScript.enabled = false;
        rangeFinder.enabled = true;

        arCrosshairUI.SetActive(false);
        rangeFinderLinesUI.SetActive(true);
        zoomSliderUI.SetActive(true);
        
        buttonText.text = "<b>Tryb AR </b>\n<size=60%>Przełącz</size>";
        if (distanceText != null)
        {
            distanceText.text = "<size=50%><color=#B0B0B0>KALIBRACJA OPTYKI...</color></size>";
        }
    }
}
