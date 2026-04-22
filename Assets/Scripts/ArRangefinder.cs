using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class ArRangefinder : MonoBehaviour
{
    [Header("Główne komponenty")]
    public ARRaycastManager raycastManager;
    public Camera arCamera;
    public TextMeshProUGUI distanceText;

    [Header("Komponenty dodatkowe")]
    public Image crosshair;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>(); // Lista do przechowywania wyników

    private float smoothedDistance = 0f; // Zmienna do przechowywania wygładzonej odległości

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // Wyliczenie środka ekranu

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
        {
            Pose hitPose = hits[0].pose;

            float actualDistance = Vector3.Distance(arCamera.transform.position, hitPose.position);

            smoothedDistance = Mathf.Lerp(smoothedDistance, actualDistance, Time.deltaTime * 10f);

            distanceText.text = $"Dystancs:\n{smoothedDistance.ToString("F2")} m";
            
        }
        else
        {
            distanceText.text = "Szukam celu...";
        }
    }
}
