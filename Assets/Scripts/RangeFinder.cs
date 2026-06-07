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
    public Slider zoomSlider;
    public TextMeshProUGUI distanceText;
    [Header("Sterowanie linii")]
    public float dragSensitivity = 0.001f;
    public float pinchZoomSensitivity = 0.005f;
    public float canvasHeight;
    public float currentRatio = 0.2f;
    private bool isDraggingLines = false;

    private RenderTexture autoRenderTexture;
    private GameObject autoBackgroundObj;
    private RawImage autoRawImage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RectTransform canvasRect = topLine.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        canvasHeight = canvasRect.rect.height;
        /*if (zoomSlider != null)
        {
            zoomSlider.minValue = 1f;
            zoomSlider.maxValue = 5f;
            zoomSlider.value = 1f;
        }*/

    }

    // Update is called once per frame
    void Update()
    {
        int touchCount = Touchscreen.current != null ? GetActiveTouchesCount() : 0;
        if (touchCount == 1)
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
                    currentRatio += Touch.delta.ReadValue().y * dragSensitivity;
                    currentRatio = Mathf.Clamp(currentRatio, 0.01f, 1.0f);
                }
            }
            else if(Touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended || Touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                isDraggingLines = false;
            }
        }
        else if (touchCount >= 2)
        {
            var touchZero = Touchscreen.current.touches[0];
            var touchOne = Touchscreen.current.touches[1];

            if (touchZero.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved || touchOne.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 touchZeroPos = touchZero.position.ReadValue();
                Vector2 touchOnePos = touchOne.position.ReadValue();
                
                Vector2 touchZeroPrevPos = touchZeroPos - touchZero.delta.ReadValue();
                Vector2 touchOnePrevPos = touchOnePos - touchOne.delta.ReadValue();

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZeroPos - touchOnePos).magnitude;
                float deltaMagnitudeDiff = touchDeltaMag - prevTouchDeltaMag;

                if (zoomSlider != null)
                {
                    zoomSlider.value += deltaMagnitudeDiff * pinchZoomSensitivity;
                }
            }
        }
        else
        {
            isDraggingLines = false;
        }

        float currentZoom = zoomSlider != null ? zoomSlider.value : 1f;
        if (autoBackgroundObj != null)
        {
            autoBackgroundObj.transform.localScale = new Vector3(currentZoom, currentZoom, 1f);
        }
        float distanceBetweenLines = currentRatio * canvasHeight;
        topLine.anchoredPosition = new Vector2(0, distanceBetweenLines / 2f);
        bottomLine.anchoredPosition = new Vector2(0, -distanceBetweenLines / 2f);

        float realRatio = currentRatio / currentZoom;

        float cameraFOV = Camera.fieldOfView;
        float apparentAngleDegrees = realRatio * cameraFOV;
        float apparentAngleRad = apparentAngleDegrees * Mathf.Deg2Rad;
        float calculatedDistance = (referenceHeight / 2f) / Mathf.Tan(apparentAngleRad / 2f);

        distanceText.text = $"<size=40%><color=#B0B0B0>CEL: {referenceHeight}m | ZOOM: {currentZoom:F1}x</color></size>\n<b>{calculatedDistance:F2}</b><size=60%> m</size>";
        
    }
    void OnEnable()
    {
        if (Camera == null) return;
        autoRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        Camera.targetTexture = autoRenderTexture;

        Canvas canvas = topLine.GetComponentInParent<Canvas>();
        
        autoBackgroundObj = new GameObject("AutomatyczneTloZoomu");
        autoBackgroundObj.transform.SetParent(canvas.transform, false);
        autoBackgroundObj.transform.SetAsFirstSibling();

        autoRawImage = autoBackgroundObj.AddComponent<RawImage>();
        autoRawImage.texture = autoRenderTexture;

        RectTransform bgRect = autoBackgroundObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgRect.sizeDelta = Vector2.zero;
    }
    void OnDisable()
    {
        if (Camera != null)
        {
            Camera.targetTexture = null;
        }
        if (autoBackgroundObj != null)
        {
            Destroy (autoBackgroundObj);
        }
        if (autoRenderTexture != null)
        {
            autoRenderTexture.Release();
            Destroy(autoRenderTexture);
        }
    }
    private int GetActiveTouchesCount()
    {
        int count = 0;
        foreach (var touch in Touchscreen.current.touches)
        {
            var phase = touch.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                count++;
            }
        }
        return count;
    }
}
