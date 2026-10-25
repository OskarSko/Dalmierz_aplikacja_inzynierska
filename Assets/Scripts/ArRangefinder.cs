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

    [Header("Aktualnosc odczytu")]
    [Tooltip("Po ilu sekundach bez trafienia raycastu wynik uznaje sie za nieaktualny.")]
    public float hitValidSeconds = 0.3f;
    [Tooltip("Po takiej przerwie w trafieniach wygladzanie startuje od nowej wartosci zamiast od starej.")]
    public float resetSmoothingAfterSeconds = 0.5f;

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private float smoothedDistance = 0f;
    private float lastHitTime = -999f;

    [HideInInspector] public float lastDistance;
    [HideInInspector] public float lastRawDistance;

    public bool HasFreshHit
    {
        get { return isActiveAndEnabled && (Time.time - lastHitTime) <= hitValidSeconds; }
    }

    public float HitAge
    {
        get { return Time.time - lastHitTime; }
    }

    void Start()
    {

    }

    void OnEnable()
    {
        if (crosshair != null)
        {
            crosshair.enabled = true;
        }
    }

    void Update()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
        {
            Pose hitPose = hits[0].pose;

            float actualDistance = Vector3.Distance(arCamera.transform.position, hitPose.position);

            if (Time.time - lastHitTime > resetSmoothingAfterSeconds)
            {
                smoothedDistance = actualDistance;
            }
            else
            {
                smoothedDistance = Mathf.Lerp(smoothedDistance, actualDistance, Time.deltaTime * 10f);
            }

            lastHitTime = Time.time;
            lastRawDistance = actualDistance;
            lastDistance = smoothedDistance;

            distanceText.text = $"<size=50%><color=#B0B0B0>DYSTANS (Tryb AR)</color></size>\n<b>{smoothedDistance:F2}</b><size=60%> m</size>";
        }
        else if (HasFreshHit)
        {
            // Pojedyncza klatka bez trafienia - pokazujemy ostatni wynik bez zmian.
            distanceText.text = $"<size=50%><color=#B0B0B0>DYSTANS (Tryb AR)</color></size>\n<b>{smoothedDistance:F2}</b><size=60%> m</size>";
        }
        else
        {
            distanceText.text = $"<size=50%><color=#FFD166>BRAK TRAFIENIA (Tryb AR)</color></size>\n<color=#B0B0B0><b>{smoothedDistance:F2}</b><size=60%> m</size></color>";
        }
    }
    void OnDisable()
    {
        if(crosshair != null)
        {
            crosshair.enabled = false;
        }
    }
}