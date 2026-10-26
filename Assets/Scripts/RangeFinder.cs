using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine.InputSystem;

public class RangeFinder : MonoBehaviour
{
    [Header("Ustawienia Kamery i Obiektu")]
    public Camera Camera;
    public float referenceHeight = 1.8f;
    public float anthropometricFactor = 0.896f;

    [Header("Osoba odniesienia - wzrost")]
    [Tooltip("Pole w panelu ustawien do wpisania wzrostu osoby odniesienia (m lub cm). Opcjonalne.")]
    public TMP_InputField referenceHeightInput;
    [Tooltip("Najmniejszy akceptowany wzrost [m].")]
    public float minReferenceHeight = 1.0f;
    [Tooltip("Najwiekszy akceptowany wzrost [m].")]
    public float maxReferenceHeight = 2.3f;
    private const string ReferenceHeightPrefKey = "Dalmierz.referenceHeight";

    [Header("Kalibracja kamery")]
    public bool useCalibration = true;
    public float focalNormalized = 0.673673f;

    [Header("UI")]
    public RectTransform topLine;
    public RectTransform bottomLine;
    public Slider zoomSlider;
    public TextMeshProUGUI distanceText;
    [Tooltip("Pokazuje pod wynikiem linijke diagnostyczna (raw, pewnosci, ratio).")]
    public bool showDebugInfo = true;
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
    [Tooltip("Po ilu sekundach bez poprawnej detekcji wynik w trybie auto uznaje sie za nieaktualny.")]
    public float detectionValidSeconds = 1.0f;
    private float lastValidDetectionTime = -999f;

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
    [HideInInspector] public string cameraResolution = "?";   // ustawiane przez PersonAutoDetector, wszystkie tryby
    [HideInInspector] public float lastCropSize = -1f;        // bok wycinka [px] przy ostatniej poprawnej detekcji

    /// <summary>
    /// True, gdy w trybie auto ostatnia poprawna detekcja jest swieza.
    /// Czytane przez MeasurementLogger - nieaktualny wynik nie jest zapisywany.
    /// </summary>
    public bool HasFreshDetection
    {
        get { return autoDetectMode && (Time.time - lastValidDetectionTime) <= detectionValidSeconds; }
    }

    /// <summary>Czas [s] od ostatniej poprawnej detekcji w trybie auto.</summary>
    public float DetectionAge
    {
        get { return Time.time - lastValidDetectionTime; }
    }

    // Awake wywoluje sie nawet wtedy, gdy ModeSwitcher od razu wylaczy ten komponent
    // (start w trybie AR), wiec wzrost z pola dziala we wszystkich trybach.
    void Awake()
    {
        if (PlayerPrefs.HasKey(ReferenceHeightPrefKey))
        {
            float saved = PlayerPrefs.GetFloat(ReferenceHeightPrefKey, referenceHeight);
            if (saved >= minReferenceHeight && saved <= maxReferenceHeight)
            {
                referenceHeight = saved;
            }
        }

        if (referenceHeightInput != null)
        {
            referenceHeightInput.text = referenceHeight.ToString("0.00", CultureInfo.InvariantCulture);
            referenceHeightInput.onEndEdit.AddListener(SetReferenceHeightFromText);
        }
    }

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
        float halfFovTan = 0.5f / focalNormalized;
        float heightFactor = autoDetectMode ? anthropometricFactor : 1f;
        float calculatedDistance = (referenceHeight * heightFactor) / (2f * realRatio * halfFovTan);

        lastDistance = calculatedDistance;
        lastRatio = currentRatio;

        string header = $"<size=40%><color=#B0B0B0>CEL: {referenceHeight:F2}m | ZOOM: {currentZoom:F1}x</color></size>\n";
        string debugLine = showDebugInfo
            ? $"\n<size=30%><color=#22D3EE>raw={debugRawRatio:F3} dx={debugRawRatioX:F3} nos={debugNoseConf:F2} kostki={debugAnkleL:F2}/{debugAnkleR:F2} tgt={targetRatio:F3} cur={currentRatio:F3} upd={debugUpdateCount}</color></size>"
            : "";

        if (autoDetectMode && !HasFreshDetection)
        {
            distanceText.text = header +
                "<color=#FFD166><b>--.--</b></color><size=60%> m</size>\n" +
                "<size=30%><color=#FFD166>SZUKANIE OSOBY W KADRZE...</color></size>" +
                debugLine;
        }
        else
        {
            distanceText.text = header + $"<b>{calculatedDistance:F2}</b><size=60%> m</size>" + debugLine;
        }

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
    void OnDestroy()
    {
        if (referenceHeightInput != null)
        {
            referenceHeightInput.onEndEdit.RemoveListener(SetReferenceHeightFromText);
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

    /// <summary>
    /// Ustawia wzrost osoby odniesienia z tekstu pola (np. "1.82", "1,82" lub "182").
    /// Wartosc jest zapamietywana miedzy uruchomieniami aplikacji.
    /// Mozna tez podpiac recznie pod On End Edit pola tekstowego.
    /// </summary>
    public void SetReferenceHeightFromText(string text)
    {
        string raw = (text ?? "").Trim().Replace(',', '.');
        float value;
        bool parsed = float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // Wpisane w centymetrach (np. 182) -> metry
        if (parsed && value > 3f) value /= 100f;

        if (!parsed || value < minReferenceHeight || value > maxReferenceHeight)
        {
            Debug.LogWarning($"[RangeFinder] Niepoprawny wzrost '{text}' - dozwolone {minReferenceHeight:F2}-{maxReferenceHeight:F2} m. Zostaje {referenceHeight:F2} m.");
            if (referenceHeightInput != null)
            {
                referenceHeightInput.SetTextWithoutNotify(referenceHeight.ToString("0.00", CultureInfo.InvariantCulture));
            }
            return;
        }

        referenceHeight = value;
        PlayerPrefs.SetFloat(ReferenceHeightPrefKey, value);
        PlayerPrefs.Save();

        if (referenceHeightInput != null)
        {
            referenceHeightInput.SetTextWithoutNotify(value.ToString("0.00", CultureInfo.InvariantCulture));
        }
        Debug.Log($"[RangeFinder] Wzrost osoby odniesienia: {value:F2} m");
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

        bool wasStale = !HasFreshDetection;

        debugUpdateCount++;
        float zoom = zoomSlider != null ? zoomSlider.value : 1f;
        targetRatio = Mathf.Clamp(debugRawRatio * fovFraction * zoom, 0.01f, 1.0f);
        lastValidDetectionTime = Time.time;

        // Po przerwie w detekcji nie wygladzamy od starej wartosci - od razu nowy wynik.
        if (wasStale)
        {
            currentRatio = targetRatio;
        }
    }
    public void ToggleAutoDetectMode()
    {
        autoDetectMode = !autoDetectMode;

        if (autoDetectMode)
        {
            targetRatio = currentRatio;
            lastValidDetectionTime = -999f;
        }
        if(autoDetectButtonText != null)
        {
            autoDetectButtonText.text = autoDetectMode ? "<b>TRYB RĘCZNY</b>\n<size=60%>Przełącz</size>" : "<b>AUTO-DETEKCJA</b>\n<size=60%>Przełącz</size>";
        }
        UpdateModeSegments();
    }
    public void SetAutoDetectMode(bool enabled)
    {
        bool switchedOn = enabled && !autoDetectMode;
        autoDetectMode = enabled;
        if(enabled) targetRatio = currentRatio;
        if (switchedOn) lastValidDetectionTime = -999f;

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