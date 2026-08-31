using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using Unity.InferenceEngine;
using System.Threading.Tasks;

public class PersonAutoDetector : MonoBehaviour
{
    
    [Header("Adaptacyjne kadrowanie")]
    public bool adaptiveCrop = true;
    public float targetFillRatio = 0.45f;
    public int minCropSize = 320;

    private int currentCropSize = -1;
    private int framesWithoutDetection = 0;

    [Header("Reference")]
    public ARCameraManager cameraManager;
    public RangeFinder rangeFinder;
    public PoseDebugVisualizer debugVisualizer;

    [Header("Model")]
    public ModelAsset poseModelAsset;

    [Header("Settings")]
    public int frameSkip = 5;
    public float minConfidence = 0.3f;
    public int modelInputSize = 192;
    public bool rotateClockwise = true;

    private Worker worker;
    private Model runtimeModel;
    private int frameCounter = 0;
    private Texture2D lastDebugTex;
    private bool isProcessing = false;
    private bool cameraConfigSelected = false;
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
    void OnEnable() => cameraManager.frameReceived += OnCameraFrameReceived;
    void OnDisable() => cameraManager.frameReceived -= OnCameraFrameReceived;

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (!cameraConfigSelected)
        {
            cameraConfigSelected = true;
            SelectBestCameraConfig();
        }
        if (rangeFinder == null || !rangeFinder.autoDetectMode) return;
        if (isProcessing) return;

        frameCounter++;
        if (frameCounter % frameSkip != 0) return;

        if (!cameraManager.TryAcquireLatestCpuImage(out var image))
            return;
        
        isProcessing = true;
        _ = ProcessImageAsync(image);
    }

    async Task ProcessImageAsync(XRCpuImage image)
    {
        try
        {
            int maxCrop = Mathf.Min(image.width, image.height);
            if (currentCropSize <= 0) currentCropSize = maxCrop;
            int cropSize = adaptiveCrop ? Mathf.Clamp(currentCropSize, minCropSize, maxCrop) : maxCrop;
            int cropX = (image.width - cropSize) / 2;
            int cropY = (image.height - cropSize) / 2;
            float fovFraction = cropSize / (float)image.width;

            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(cropX, cropY, cropSize, cropSize),
                outputDimensions = new Vector2Int(modelInputSize, modelInputSize),
                outputFormat = TextureFormat.RGB24,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = image.GetConvertedDataSize(conversionParams);
            using var buffer = new NativeArray<byte>(size, Allocator.TempJob);
            image.Convert(conversionParams,buffer);
            image.Dispose();

            var tex = new Texture2D(modelInputSize, modelInputSize, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(buffer);
            tex.Apply();
            var rotatedTex = RotateTexture90(tex, rotateClockwise);
            Destroy(tex);
            tex = rotatedTex;

            var keypoints = await RunInference(tex);
            if(debugVisualizer != null)
            {
                debugVisualizer.ShowFrame(tex);
                if (keypoints.HasValue)
                {
                    debugVisualizer.ShowKeypoints(keypoints.Value);
                }
            }
            if (lastDebugTex != null)
            {
                Destroy(lastDebugTex);
            }
            lastDebugTex = tex;

            if (keypoints.HasValue)
            {
                rangeFinder.UpdateFromDetection(keypoints.Value, fovFraction);

                if (adaptiveCrop)
                {
                    if (keypoints.HasValue && keypoints.Value.noseConfidence >= 0.3f)
                    {
                        framesWithoutDetection = 0;
                        Vector2 feet = HeadFootEstimator.EstimateFeet(keypoints.Value);
                        float fill = Mathf.Abs(feet.y - keypoints.Value.nose.y);

                        if (fill > 0.05f)
                        {
                            int desired = Mathf.RoundToInt(cropSize * (fill / targetFillRatio));
                            currentCropSize = Mathf.Clamp(
                                Mathf.RoundToInt(Mathf.Lerp(cropSize, desired, 0.3f)),
                                minCropSize, maxCrop);
                        }
                    }
                    else
                    {
                        framesWithoutDetection++;
                        if (framesWithoutDetection > 5)
                        {
                            currentCropSize = maxCrop;
                            framesWithoutDetection = 0;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PersonAutoDetector] Blad inferencji: {e}");
            
        }
        finally
        {
            isProcessing = false;
        }
    }

    async Task<PoseKeypoints?> RunInference(Texture2D tex)
    {
        byte[] rawPixels = tex.GetRawTextureData<byte>().ToArray();
        int[] intPixels = new int[rawPixels.Length];
        for (int i = 0; i < rawPixels.Length; i++)
        {
            intPixels[i] = rawPixels[i];
        }
        using Tensor<int> input = new Tensor<int>(new TensorShape(1, modelInputSize, modelInputSize, 3), intPixels);
        worker.Schedule(input);

        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        using Tensor<float> cpuTensor = await outputTensor.ReadbackAndCloneAsync();
        
        return PoseKeypointUtils.Parse(cpuTensor);
    }

    Texture2D RotateTexture90(Texture2D original, bool clockwise)
    {
        int size = original.width;
        Color32[] src = original.GetPixels32();
        Color32[] dst = new Color32[src.Length];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int nx = clockwise ? (size - 1 - y) : y;
                int ny = clockwise ? x : (size - 1 - x);
                dst[ny * size + nx] = src[y * size + x];
            }
        }

        Texture2D rotated = new Texture2D(size, size, original.format, false);
        rotated.SetPixels32(dst);
        rotated.Apply();
        return rotated;
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
    }
}
