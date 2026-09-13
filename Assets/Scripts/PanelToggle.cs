using UnityEngine;

public class PanelToggle : MonoBehaviour
{
    [Tooltip("Panel, ktory ma byc pokazywany i chowany.")]
    public GameObject panel;

    /// <summary>Przelacza widocznosc panelu.</summary>
    public void Toggle()
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf);
    }

    /// <summary>Pokazuje panel.</summary>
    public void Show()
    {
        if (panel != null) panel.SetActive(true);
    }

    /// <summary>Chowa panel.</summary>
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}