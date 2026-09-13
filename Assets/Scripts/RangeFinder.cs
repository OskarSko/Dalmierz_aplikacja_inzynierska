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
    public float anthropometricFactor = 0.896f;

    [Header("Kalibracja kamery")]
    public bool useCalibration = true;
    public float focalNormalized = 0.673673f;

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

    [Header("Auto-detekcja")]
    public bool autoDetectMode = false;
    public float autoSmoothSpeed = 8f;
    public TextMeshProUGUI autoDetectButtonText;

    [Header("Przelacznik segmentowy trybu")] 
    public Image segmentManualBg; 
    public TextMeshProUGUI segmentManualText; 
    public Image segmentAutoBg; 
    public TextMeshProUGUI segmentAutoText; 
    private readonly Color segActiveBg = new Color(0.133f, 0.827f, 0.933f, 1f); 
    private readonly Color segInactiveBg = new Color(1f, 1f, 1f, 0f); 
    private readonly Color segActiveText = new Color(0.016f, 0.173f, 0.325f, 1f); 
    private readonly Color segInactiveText = new Color(1f, 1f, 1f, 0.65f);

    private float targetRatio;
    private float debugRawRatio = -1f;
    private float debugRawRatioX = -1f;
    private float debugNoseConf = -1f;
    private float debugAnkleL = -1f, debugAnkleR = -1f;
    private int debugUpdateCount = 0;


    private RenderTexture autoRenderTexture;
    private GameObject autoBackgroundObj;
    private RawImage autoRawImage;

    [HideInInspector] public float lastDistance;
    [HideInInspector] public float lastRatio;
    [HideInInspector] public float lastNoseConf;
    [HideInInspector] public float lastAnkleL, lastAnkleR;
    [HideInInspector] public float lastZoom = 1f;
    public string debugImageInfo = "?";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RectTransform canvasRect = topLine.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        canvasHeight = canvasRect.rect.height;
        targetRatio = currentRatio;
        UpdateModeSegments();
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
        if (!autoDetectMode)
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
        }
        else
        {
            currentRatio = Mathf.Lerp(currentRatio, targetRatio, Time.deltaTime * autoSmoothSpeed);
        }

        float currentZoom = zoomSlider != null ? zoomSlider.value : 1f;
        lastZoom = currentZoom;
        if (autoBackgroundObj != null)
        {
            autoBackgroundObj.transform.localScale = new Vector3(currentZoom, currentZoom, 1f);
        }
        float distanceBetweenLines = currentRatio * canvasHeight;
        topLine.anchoredPosition = new Vector2(0, distanceBetweenLines / 2f);
        bottomLine.anchoredPosition = new Vector2(0, -distanceBetweenLines / 2f);

        float realRatio = currentRatio / currentZoom;
        float cameraFOV = Camera.fieldOfView;
        float halfFovTan = 0.5f / focalNormalized;
        float heightFactor = autoDetectMode ? anthropometricFactor : 1f;
        float calculatedDistance = (referenceHeight * heightFactor) / (2f * realRatio * halfFovTan);

        lastDistance = calculatedDistance;
        lastRatio = currentRatio;

        distanceText.text = $"<size=40%><color=#B0B0B0>CEL: {referenceHeight}m | ZOOM: {currentZoom:F1}x</color></size>\n<b>{calculatedDistance:F2}</b><size=60%> m</size>\n<size=30%><color=#22D3EE>raw={debugRawRatio:F3} dx={debugRawRatioX:F3} nos={debugNoseConf:F2} kostki={debugAnkleL:F2}/{debugAnkleR:F2} tgt={targetRatio:F3} cur={currentRatio:F3} upd={debugUpdateCount}</color></size>";
        
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
    public void UpdateFromDetection(PoseKeypoints kp, float fovFraction = 1f)
    {
        Vector2 headNorm = HeadFootEstimator.EstimateHeadTop(kp);
        Vector2 feetNorm = HeadFootEstimator.EstimateFeet(kp);
        debugRawRatio = Mathf.Abs(feetNorm.y - kp.nose.y);
        debugRawRatioX = Mathf.Abs(feetNorm.x - headNorm.x);
        debugNoseConf = kp.noseConfidence;
        debugAnkleL = kp.leftAnkleConfidence;
        debugAnkleR = kp.rightAnkleConfidence;
        lastNoseConf = kp.noseConfidence;
        lastAnkleL = kp.leftAnkleConfidence;
        lastAnkleR = kp.rightAnkleConfidence;

        if (kp.noseConfidence < 0.3f) return;

        debugUpdateCount++;
        float zoom = zoomSlider != null ? zoomSlider.value : 1f;
        targetRatio = Mathf.Clamp(debugRawRatio * fovFraction * zoom, 0.01f, 1.0f);
    }
    public void ToggleAutoDetectMode()
    {
        autoDetectMode = !autoDetectMode;

        if (autoDetectMode)
        {
            targetRatio = currentRatio;
        }
        if(autoDetectButtonText != null)
        {
            autoDetectButtonText.text = autoDetectMode ? "<b>TRYB RĘCZNY</b>\n<size=60%>Przełącz</size>" : "<b>AUTO-DETEKCJA</b>\n<size=60%>Przełącz</size>";
        }
        UpdateModeSegments();
    }
    public void SetAutoDetectMode(bool enabled)
    {
        autoDetectMode = enabled;
        if(enabled) targetRatio = currentRatio;

        if(autoDetectButtonText != null)
        {
            autoDetectButtonText.text = autoDetectMode ? "<b>TRYB RĘCZNY</b>\n<size=60%>Przełącz</size>" : "<b>AUTO-DETEKCJA</b>\n<size=60%>Przełącz</size>";
        }
        UpdateModeSegments();
    }
    void UpdateModeSegments()
    {
        if (segmentAutoBg != null) segmentAutoBg.color = autoDetectMode ? segActiveBg : segInactiveBg;
        if (segmentManualBg != null) segmentManualBg.color = autoDetectMode ? segInactiveBg : segActiveBg;
        if (segmentAutoText != null) segmentAutoText.color = autoDetectMode ? segActiveText : segInactiveText;
        if (segmentManualText != null) segmentManualText.color = autoDetectMode ? segInactiveText : segActiveText;
    }
}