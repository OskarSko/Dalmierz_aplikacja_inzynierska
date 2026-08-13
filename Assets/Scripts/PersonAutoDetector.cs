using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using Unity.InferenceEngine;
using System.Threading.Tasks;

public class PersonAutoDetector : MonoBehaviour
{
    [Header("Reference")]
    public ARCameraManager cameraManager;
    public RangeFinder rangeFinder;

    [Header("Model")]
    public ModelAsset poseModelAsset;

    [Header("Settings")]
    public int frameSkip = 5;
    public float minConfidence = 0.3f;
    public int modelInputSize = 192;

    private Worker worker;
    private Model runtimeModel;
    private int frameCounter = 0;
    private bool isProcessing = false;
    void Start()
    {
        runtimeModel = ModelLoader.Load(poseModelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
    }
    void OnEnable() => cameraManager.frameReceived += OnCameraFrameReceived;
    void OnDisable() => cameraManager.frameReceived -= OnCameraFrameReceived;

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
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
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
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

            var keypoints = await RunInference(tex);
            Destroy(tex);

            if (keypoints.HasValue)
            {
                rangeFinder.UpdateFromDetection(keypoints.Value);
            }
        }
        finally
        {
            isProcessing = false;
        }
    }

    async Task<PoseKeypoints?> RunInference(Texture2D tex)
    {
        using Tensor<float> input = TextureConverter.ToTensor(tex, modelInputSize, modelInputSize, 3);
        worker.Schedule(input);

        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        using Tensor<float> cpuTensor = await outputTensor.ReadbackAndCloneAsync();
        
        return PoseKeypointUtils.Parse(cpuTensor);
    }
    void OnDestroy()
    {
        worker?.Dispose();
    }
}
