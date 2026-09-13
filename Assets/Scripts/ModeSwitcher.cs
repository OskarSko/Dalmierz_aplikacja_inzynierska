using UnityEngine;
using UnityEngine.UI;
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

    [Header("Przelacznik segmentowy")]
    public Image segmentHeightBg;
    public TextMeshProUGUI segmentHeightText;
    public Image segmentArBg;
    public TextMeshProUGUI segmentArText;

    private readonly Color activeBg = new Color(0.133f, 0.827f, 0.933f, 1f);
    private readonly Color inactiveBg = new Color(1f, 1f, 1f, 0f);
    private readonly Color activeText = new Color(0.016f, 0.173f, 0.325f, 1f);
    private readonly Color inactiveText = new Color(1f, 1f, 1f, 0.65f);

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
    public void SelectHeightMode()
    {
        if (isArMode) ToggleMode();
    }
    public void SelectArMode()
    {
        if (!isArMode) ToggleMode();
    }
        void ActivateArMode()
    {
        arScript.enabled = true;
        rangeFinder.enabled = false;

        if (arCrosshairUI != null) arCrosshairUI.SetActive(true);
        if (rangeFinderLinesUI != null) rangeFinderLinesUI.SetActive(false);
        if (zoomSliderUI != null) zoomSliderUI.SetActive(false);

        if (buttonText != null)
        {
            buttonText.text = "<b>TRYB DALEKI</b>\n<size=60%>Przełącz</size>";
        }
        if (distanceText != null)
        {
            distanceText.text = "<size=50%><color=#B0B0B0>SKANOWANIE OTOCZENIA...</color></size>";
        }
        UpdateSegments(true);
    }
    void ActivateRangeFinder()
    {
        arScript.enabled = false;
        rangeFinder.enabled = true;

        if (arCrosshairUI != null) arCrosshairUI.SetActive(false);
        if (rangeFinderLinesUI != null) rangeFinderLinesUI.SetActive(true);
        if (zoomSliderUI != null) zoomSliderUI.SetActive(true);

        if (buttonText != null)
        {
            buttonText.text = "<b>Tryb AR </b>\n<size=60%>Przełącz</size>";
        }
        if (distanceText != null)
        {
            distanceText.text = "<size=50%><color=#B0B0B0>KALIBRACJA OPTYKI...</color></size>";
        }
        UpdateSegments(false);
    }
void UpdateSegments(bool arActive)
    {
        if (segmentArBg != null) segmentArBg.color = arActive ? activeBg : inactiveBg;
        if (segmentHeightBg != null) segmentHeightBg.color = arActive ? inactiveBg : activeBg;
 
        if (segmentArText != null) segmentArText.color = arActive ? activeText : inactiveText;
        if (segmentHeightText != null) segmentHeightText.color = arActive ? inactiveText : activeText;
    }
}