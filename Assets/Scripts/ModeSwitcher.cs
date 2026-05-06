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
    public TextMeshProUGUI buttonText;

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
    }
    void ActivateRangeFinder()
    {
        arScript.enabled = false;
        rangeFinder.enabled = true;

        arCrosshairUI.SetActive(false);
        rangeFinderLinesUI.SetActive(true);
    }
}
