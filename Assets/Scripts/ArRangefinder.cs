using System.Collections.Generic;
using Microsoft.Unity.VisualStudio.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ArRangefinder : MonoBehaviour
{
    [Header("Główne komponenty")]
    public ARRaycastManager raycastManager;
    public Camera arCamera;
    public TextMeshProUGUI distanceText;

    [Header("Komponenty dodatkowe")]
    public Image Crosshair;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>(); // Lista do przechowywania wyników

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // Wyliczenie środka ekranu

        if (raycastManager.Raycast(screenCenter, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon | UnityEngine.XR.ARSubsystems.TrackableType.FeaturePoint))
        {
            
        }
    }
}
