using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;

public class PersonAutoDetector : MonoBehaviour
{
    [Header("Adaptacyjne kadrowanie")]
    [Tooltip("Minimalna pewnosc nosa i kostki, aby detekcja sterowala kadrem.")]
    public float cropControlConfidence = 0.15f;
    [Tooltip("Wylaczone = stary tryb: staly kwadrat ze srodka obrazu.")]
    public bool adaptiveCrop = true;
    [Tooltip("Czesc wysokosci wejscia sieci zajmowana przez sylwetke (czubek glowy - kostki). Zalecane 0.6.")]
    public float targetFillRatio = 0.6f;
    [Tooltip("Najmniejszy bok wycinka w pikselach sensora. Moze byc mniejszy niz wejscie sieci - wycinek jest wtedy powiekszany. Zalecane 96.")]
    public int minCropSize = 96;

    [Header("Reference")]
    public ARCameraManager cameraManager;
    public RangeFinder rangeFinder;
    public PoseDebugVisualizer debugVisualizer;

    [Header("Model")]
    public ModelAsset poseModelAsset;

    [Header("Settings")]
    [Tooltip("Co ktora klatke kamery uruchamiac siec (i tak nie czesciej niz konczy sie poprzednia inferencja). Zalecane 2.")]
    public int frameSkip = 2;
    [Tooltip("Minimalna pewnosc nosa, aby wynik trafil do pomiaru.")]
    public float minConfidence = 0.3f;
    public int modelInputSize = 256;
    public bool rotateClockwise = true;

    [Header("Zasieg - sledzenie i szukanie")]
    [Tooltip("Minimalna pewnosc lepszej kostki, aby wynik trafil do pomiaru.")]
    public float minAnkleConfidence = 0.2f;
    [Tooltip("Liczba kolejnych nieudanych detekcji, po ktorej sledzenie przechodzi w szukanie.")]
    public int lostAfterFailures = 3;
    [Tooltip("Krok drabiny przyblizen podczas szukania (kolejny wycinek = poprzedni / krok).")]
    public float searchLadderStep = 1.5f;
    [Tooltip("Pozwala na wycinek wiekszy niz krotszy bok obrazu (z dopelnieniem tla) - pomaga z bliska, gdy sylwetka nie miesci sie w kwadracie 1080 px.")]
    public bool allowPaddedLargeCrop = true;
    [Tooltip("Szybkosc podazania srodka wycinka za osoba (0..1).")]
    [Range(0.05f, 1f)] public float trackCenterLerp = 0.8f;
    [Tooltip("Szybkosc zmiany rozmiaru wycinka (0..1).")]
    [Range(0.05f, 1f)] public float trackSizeLerp = 0.5f;
    [Tooltip("Ile razy powiekszyc wycinek po pojedynczej nieudanej detekcji podczas sledzenia.")]
    public float failureGrowFactor = 1.3f;
    [Tooltip("Przedluzenie odcinka nos-kostki o glowe (czesc dlugosci nos-kostki) przy kadrowaniu.")]
    public float headExtension = 0.12f;

    [Header("Opcjonalny detektor osob (YOLO)")]
    [Tooltip("Pusty = szukanie tylko drabina przyblizen.")]
    public YoloPersonDetector personDetector;
    [Tooltip("Bok kwadratu (piksele sensora) przeszukiwanego detektorem. 0 = krotszy bok obrazu.")]
    public int detectorRegionSize = 0;

    [Header("Diagnostyka")]
    [Tooltip("Loguje kazda inferencje (widoczne w OnScreenLogger).")]
    public bool verboseLog = false;
    [HideInInspector] public string statusLine = "";

    enum State { Searching, Tracking }

    private State state = State.Searching;
    private SensorCrop trackCrop;
    private int trackFailures = 0;
    private int searchIndex = 0;
    private int searchStep = 0;
    private float lastGoodSide = -1f;
    private readonly List<float> ladder = new List<float>();
    private int ladderW = -1, ladderH = -1, ladderMin = -1;

    private Worker worker;
    private Model runtimeModel;
    private int frameCounter = 0;
    private bool isProcessing = false;
    private bool cameraConfigSelected = false;

    private byte[] poseBytes;
    private int[] poseInts;
    private byte[] detectorBytes;
    private Texture2D debugTex;

    void Start()
    {
        try
        {
            runtimeModel = ModelLoader.Load(poseModelAsset);
            worker = new Worker(runtimeModel, BackendType.GPUCompute);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PersonAutoDetector] Bład ladowania modelu: {e}");
        }
    }

    void OnEnable() { if (cameraManager != null) cameraManager.frameReceived += OnCameraFrameReceived; }
    void OnDisable() { if (cameraManager != null) cameraManager.frameReceived -= OnCameraFrameReceived; }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (!cameraConfigSelected)
        {
            cameraConfigSelected = true;
            SelectBestCameraConfig();
        }
        if (rangeFinder == null) return;
        if (!rangeFinder.autoDetectMode)
        {
            if (state != State.Searching) ResetTracking("tryb reczny");
            return;
        }
        if (worker == null || isProcessing) return;

        frameCounter++;
        if (frameCounter % Mathf.Max(1, frameSkip) != 0) return;

        if (!cameraManager.TryAcquireLatestCpuImage(out var image))
            return;

        isProcessing = true;
        _ = ProcessImageAsync(image);
    }

    async Task ProcessImageAsync(XRCpuImage image)
    {
        bool imageDisposed = false;
        try
        {
            int W = image.width;
            int H = image.height;
            int S = modelInputSize;
            EnsureLadder(W, H);

            // ---------- 1. Wybor wycinka i sieci ----------
            bool runDetector = false;
            string mode;
            SensorCrop crop;

            if (!adaptiveCrop)
            {
                crop = SensorCrop.Centered(W, H, Mathf.Min(W, H));
                mode = "STALY";
            }
            else if (state == State.Tracking)
            {
                crop = trackCrop;
                mode = "SLEDZENIE";
            }
            else
            {
                bool detectorReady = personDetector != null && personDetector.IsReady;
                if (detectorReady && searchStep % 2 == 0)
                {
                    float region = detectorRegionSize > 0 ? detectorRegionSize : Mathf.Min(W, H);
                    crop = SensorCrop.Centered(W, H, region);
                    runDetector = true;
                    mode = "DETEKTOR";
                }
                else
                {
                    if (searchIndex >= ladder.Count) searchIndex = 0;
                    crop = SensorCrop.Centered(W, H, ladder[searchIndex]);
                    searchIndex = (searchIndex + 1) % ladder.Count;
                    mode = "SZUKANIE";
                }
                searchStep++;
            }

            // ---------- 2. Probkowanie wycinka z natywnej rozdzielczosci ----------
            if (runDetector)
            {
                int D = personDetector.inputSize;
                EnsureBuffer(ref detectorBytes, D * D * 3);
                CropSampler.Sample(image, crop, D, rotateClockwise, 114, detectorBytes);
            }
            else
            {
                EnsureBuffer(ref poseBytes, S * S * 3);
                CropSampler.Sample(image, crop, S, rotateClockwise, 0, poseBytes);
            }
            image.Dispose();
            imageDisposed = true;

            rangeFinder.debugImageInfo = $"{W}x{H} crop={crop.side:F0}";

            // ---------- 3a. Detektor osob ----------
            if (runDetector)
            {
                PersonBox? box = await personDetector.DetectAsync(detectorBytes);
                HandleDetectorResult(box, crop, W, H);
                return;
            }

            // ---------- 3b. MoveNet ----------
            PoseKeypoints? result = await RunInference(poseBytes, S);
            ShowDebug(result, S);

            if (!result.HasValue)
            {
                OnPoseFailure(W, H, mode, crop, null);
                return;
            }

            PoseKeypoints kp = result.Value;
            float ankleConf = Mathf.Max(kp.leftAnkleConfidence, kp.rightAnkleConfidence);
            Vector2 feet = HeadFootEstimator.EstimateFeet(kp);
            float spanModel = Mathf.Abs(feet.y - kp.nose.y);
            bool plausible = spanModel > 0.05f && spanModel < 1.05f;

            bool goodForCrop = plausible &&
                               kp.noseConfidence >= cropControlConfidence &&
                               ankleConf >= cropControlConfidence;
            bool goodForMeasure = plausible &&
                                  kp.noseConfidence >= minConfidence &&
                                  ankleConf >= minAnkleConfidence;

            if (goodForMeasure)
            {
                // Ten sam wzor co wczesniej: ratio_kadr * (bok_wycinka / szerokosc_obrazu)
                rangeFinder.UpdateFromDetection(kp, crop.side / W);
            }

            if (verboseLog)
            {
                Debug.Log($"[Auto] {mode} crop={crop.side:F0} fill={spanModel:F2} nos={kp.noseConfidence:F2} " +
                          $"kostki={kp.leftAnkleConfidence:F2}/{kp.rightAnkleConfidence:F2} pomiar={(goodForMeasure ? "tak" : "nie")}");
            }

            if (!adaptiveCrop)
            {
                statusLine = $"{mode} crop={crop.side:F0}";
                return;
            }

            if (goodForCrop)
            {
                UpdateTrackCrop(kp, feet, crop, W, H, mode);
            }
            else
            {
                OnPoseFailure(W, H, mode, crop, kp);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PersonAutoDetector] Blad inferencji: {e}");
        }
        finally
        {
            if (!imageDisposed) image.Dispose();
            isProcessing = false;
        }
    }

    // ===================== Kadrowanie =====================

    void UpdateTrackCrop(PoseKeypoints kp, Vector2 feet, SensorCrop usedCrop, int W, int H, string mode)
    {
        Vector2 noseS = CropSampler.ModelToSensor(usedCrop, kp.nose.x, kp.nose.y, rotateClockwise);
        Vector2 feetS = CropSampler.ModelToSensor(usedCrop, feet.x, feet.y, rotateClockwise);

        // Przy pionowym trzymaniu telefonu sylwetka lezy wzdluz osi X obrazu z sensora.
        float span = Mathf.Abs(noseS.x - feetS.x);
        float headTopX = noseS.x + (noseS.x - feetS.x) * headExtension;
        float bodyLen = span * (1f + headExtension);

        float cx = (headTopX + feetS.x) * 0.5f;
        float cy = (noseS.y + feetS.y) * 0.5f;
        float desired = Mathf.Clamp(bodyLen / Mathf.Max(0.2f, targetFillRatio), minCropSize, MaxCropSide(W, H));

        bool wasTracking = state == State.Tracking;
        if (wasTracking)
        {
            trackCrop = new SensorCrop(
                Mathf.Lerp(trackCrop.centerX, cx, trackCenterLerp),
                Mathf.Lerp(trackCrop.centerY, cy, trackCenterLerp),
                Mathf.Lerp(trackCrop.side, desired, trackSizeLerp));
        }
        else
        {
            trackCrop = new SensorCrop(cx, cy, desired);
            state = State.Tracking;
            Debug.Log($"[Auto] NAMIERZONO ({mode}, wycinek {usedCrop.side:F0}px) -> sledzenie, nowy wycinek {desired:F0}px, sylwetka {bodyLen:F0}px");
        }

        trackCrop = ClampCrop(trackCrop, W, H);
        trackFailures = 0;
        lastGoodSide = trackCrop.side;
        statusLine = $"SLEDZENIE crop={trackCrop.side:F0} sylwetka={bodyLen:F0}px";
    }

    void OnPoseFailure(int W, int H, string mode, SensorCrop usedCrop, PoseKeypoints? kp)
    {
        if (!adaptiveCrop) return;

        if (state == State.Tracking)
        {
            trackFailures++;
            trackCrop = ClampCrop(new SensorCrop(trackCrop.centerX, trackCrop.centerY,
                Mathf.Min(trackCrop.side * failureGrowFactor, MaxCropSide(W, H))), W, H);

            if (trackFailures >= Mathf.Max(1, lostAfterFailures))
            {
                ResetTracking($"zgubiono po {trackFailures} probach");
            }
        }
        else
        {
            statusLine = $"SZUKANIE crop={usedCrop.side:F0}";
        }
    }

    void HandleDetectorResult(PersonBox? box, SensorCrop region, int W, int H)
    {
        if (!box.HasValue)
        {
            statusLine = $"DETEKTOR: brak osoby (obszar {region.side:F0}px)";
            if (verboseLog) Debug.Log("[Auto] " + statusLine);
            return;
        }

        PersonBox b = box.Value;
        Vector2 center = CropSampler.ModelToSensor(region, b.cx, b.cy, rotateClockwise);
        Vector2 top = CropSampler.ModelToSensor(region, b.cx, b.cy - b.h * 0.5f, rotateClockwise);
        Vector2 bottom = CropSampler.ModelToSensor(region, b.cx, b.cy + b.h * 0.5f, rotateClockwise);

        float heightS = Mathf.Abs(top.x - bottom.x);
        float widthS = b.w * region.side;
        float side = Mathf.Max(heightS / Mathf.Max(0.2f, targetFillRatio), widthS / 0.9f);
        side = Mathf.Clamp(side, minCropSize, MaxCropSide(W, H));

        trackCrop = ClampCrop(new SensorCrop(center.x, center.y, side), W, H);
        state = State.Tracking;      // MoveNet potwierdzi w kolejnej klatce
        trackFailures = 0;
        lastGoodSide = side;
        statusLine = $"DETEKTOR: osoba {heightS:F0}px (pewnosc {b.score:F2}) -> wycinek {side:F0}px";
        Debug.Log("[Auto] " + statusLine);
    }

    void ResetTracking(string reason)
    {
        state = State.Searching;
        trackFailures = 0;
        searchStep = 0;
        searchIndex = ClosestLadderIndex(lastGoodSide);
        statusLine = "SZUKANIE";
        Debug.Log($"[Auto] SZUKANIE ({reason}), start od wycinka {(ladder.Count > 0 ? ladder[searchIndex] : 0f):F0}px");
    }

    float MaxCropSide(int W, int H)
    {
        return allowPaddedLargeCrop ? Mathf.Max(W, H) : Mathf.Min(W, H);
    }

    /// <summary>Srodek wycinka nie moze uciec poza obraz (inaczej sledzenie dryfuje w pustke).</summary>
    SensorCrop ClampCrop(SensorCrop c, int W, int H)
    {
        c.centerX = Mathf.Clamp(c.centerX, 0f, W);
        c.centerY = Mathf.Clamp(c.centerY, 0f, H);
        c.side = Mathf.Clamp(c.side, Mathf.Max(16, minCropSize), MaxCropSide(W, H));
        return c;
    }

    void EnsureLadder(int W, int H)
    {
        if (W == ladderW && H == ladderH && minCropSize == ladderMin && ladder.Count > 0) return;
        ladderW = W; ladderH = H; ladderMin = minCropSize;

        ladder.Clear();
        float step = Mathf.Max(1.1f, searchLadderStep);
        float minSide = Mathf.Max(16, minCropSize);
        float s = Mathf.Min(W, H);
        while (s >= minSide - 0.5f)
        {
            ladder.Add(s);
            s /= step;
        }
        if (ladder.Count == 0) ladder.Add(Mathf.Min(W, H));
        if (allowPaddedLargeCrop && Mathf.Max(W, H) > Mathf.Min(W, H)) ladder.Add(Mathf.Max(W, H));

        searchIndex = 0;
        string list = string.Join(", ", ladder.ConvertAll(v => v.ToString("F0")).ToArray());
        Debug.Log($"[Auto] obraz {W}x{H}, drabina wycinkow: {list}");
    }

    int ClosestLadderIndex(float side)
    {
        if (side <= 0f || ladder.Count == 0) return 0;
        int best = 0;
        float bestDiff = float.MaxValue;
        for (int k = 0; k < ladder.Count; k++)
        {
            float diff = Mathf.Abs(Mathf.Log(ladder[k] / side));
            if (diff < bestDiff) { bestDiff = diff; best = k; }
        }
        return best;
    }

    // ===================== Siec =====================

    async Task<PoseKeypoints?> RunInference(byte[] rgb, int S)
    {
        int n = S * S * 3;
        if (poseInts == null || poseInts.Length != n) poseInts = new int[n];
        for (int k = 0; k < n; k++) poseInts[k] = rgb[k];

        Tensor<int> input = new Tensor<int>(new TensorShape(1, S, S, 3), poseInts);
        try
        {
            worker.Schedule(input);
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
            Tensor<float> cpuTensor = await outputTensor.ReadbackAndCloneAsync();
            try
            {
                return PoseKeypointUtils.Parse(cpuTensor);
            }
            finally
            {
                cpuTensor.Dispose();
            }
        }
        finally
        {
            input.Dispose();
        }
    }

    void ShowDebug(PoseKeypoints? keypoints, int S)
    {
        if (debugVisualizer == null) return;

        if (debugTex == null || debugTex.width != S)
        {
            if (debugTex != null) Destroy(debugTex);
            debugTex = new Texture2D(S, S, TextureFormat.RGB24, false);
        }
        // Te same bajty co wejscie sieci - podglad wyglada tak samo jak dotychczas.
        debugTex.LoadRawTextureData(poseBytes);
        debugTex.Apply();

        debugVisualizer.ShowFrame(debugTex);
        if (keypoints.HasValue) debugVisualizer.ShowKeypoints(keypoints.Value);
    }

    static void EnsureBuffer(ref byte[] buf, int size)
    {
        if (buf == null || buf.Length != size) buf = new byte[size];
    }

    void SelectBestCameraConfig()
    {
        using (var configs = cameraManager.GetConfigurations(Unity.Collections.Allocator.Temp))
        {
            if (!configs.IsCreated || configs.Length == 0) return;

            var best = configs[0];
            foreach (var c in configs)
            {
                Debug.Log($"[Kamera] dostępna konfiguracja: {c.width}x{c.height}");
                if (c.width * c.height > best.width * best.height) best = c;
            }

            cameraManager.currentConfiguration = best;
            Debug.Log($"[Kamera] WYBRANO: {best.width}x{best.height}");
        }
    }

    void OnDestroy()
    {
        worker?.Dispose();
        if (debugTex != null) Destroy(debugTex);
    }
}