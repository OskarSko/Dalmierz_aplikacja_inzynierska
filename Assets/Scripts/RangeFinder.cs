using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Runtime.CompilerServices;
using UnityEngine.InputSystem;

public class RangeFinder : MonoBehaviour
{
    [Header("Ustawienia Kamery i Obiektu")]
    public Camera Camera;
    public float referenceHeight = 1.8f;

    [Header("UI")]
    public RectTransform topLine;
    public RectTransform bottomLine;
    public TextMeshProUGUI distanceText;
    [Header("Sterowanie linii")]
    public float dragSensitivity = 0.001f;
    public float canvasHeight;
    public float currentRatio = 0.2f;
    private bool isDraggingLines = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RectTransform canvasRect = topLine.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        canvasHeight = canvasRect.rect.height;

    }

    // Update is called once per frame
    void Update()
    {
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var Touch = Touchscreen.current.touches[0];
            Vector2 touchPos = Touch.position.ReadValue();

            if (Touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                bool hitTop = RectTransformUtility.RectangleContainsScreenPoint(topLine, touchPos, null);
                bool hitBottom = RectTransformUtility.RectangleContainsScreenPoint(bottomLine, touchPos, null);

                if(hitTop || hitBottom)
                {
                    isDraggingLines = true;
                }
            }
            else if (Touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                if (isDraggingLines)
                {
                    float dragY = Touch.delta.ReadValue().y;
                    currentRatio += dragY * dragSensitivity;
                    currentRatio = Mathf.Clamp(currentRatio, 0.01f, 1.0f);
                }
            }
            else if(Touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended || Touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                isDraggingLines = false;
            }
        }
        float distanceBetweenLines = currentRatio * canvasHeight;
        topLine.anchoredPosition = new Vector2(0, distanceBetweenLines / 2f);
        bottomLine.anchoredPosition = new Vector2(0, -distanceBetweenLines / 2f);

        float cameraFOV = Camera.fieldOfView;
        float apparentAngleDegrees = currentRatio * cameraFOV;
        float apparentAngleRad = apparentAngleDegrees * Mathf.Deg2Rad;
        float calculatedDistance = (referenceHeight / 2f) / Mathf.Tan(apparentAngleRad / 2f);
        distanceText.text = $"Odległość: {calculatedDistance.ToString("F2")} m";
        
    }
}
