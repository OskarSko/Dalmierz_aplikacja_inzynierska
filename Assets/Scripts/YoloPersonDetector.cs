using System.Threading.Tasks;
using UnityEngine;
using Unity.InferenceEngine;


public struct PersonBox
{
    public float cx, cy, w, h, score;
}


public class YoloPersonDetector : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("Plik .onnx YOLO (np. yolo11s.onnx). Pusty = detektor wylaczony.")]
    public ModelAsset detectorModelAsset;
    [Tooltip("Rozmiar wejscia, z jakim wyeksportowano model (modele Unity: 640).")]
    public int inputSize = 640;
    public BackendType backend = BackendType.GPUCompute;

    [Header("Wybor osoby")]
    [Tooltip("Minimalna pewnosc klasy 'person'.")]
    [Range(0.05f, 0.95f)] public float minScore = 0.25f;
    [Tooltip("Sila preferencji dla osoby blisko srodka kadru (0 = tylko pewnosc).")]
    public float centerPreference = 1f;
    [Tooltip("Indeks klasy osoby w modelu (COCO: 0).")]
    public int personClassIndex = 0;

    private Worker worker;
    private float[] inputData;

    public bool IsReady { get { return worker != null; } }

    void Start()
    {
        if (detectorModelAsset == null)
        {
            Debug.Log("[YOLO] brak modelu - detektor wylaczony");
            return;
        }
        try
        {
            Model model = ModelLoader.Load(detectorModelAsset);
            worker = new Worker(model, backend);
            Debug.Log($"[YOLO] model zaladowany, wejscie {inputSize}x{inputSize}");
        }
        catch (System.Exception e)
        {
            worker = null;
            Debug.LogError($"[YOLO] blad ladowania modelu: {e}");
        }
    }

    public async Task<PersonBox?> DetectAsync(byte[] rgb)
    {
        if (worker == null) return null;

        int S = inputSize;
        int plane = S * S;
        if (inputData == null || inputData.Length != 3 * plane) inputData = new float[3 * plane];

        // HWC bajty -> CHW float 0..1 (format oczekiwany przez eksport Ultralytics)
        const float inv = 1f / 255f;
        for (int p = 0, k = 0; p < plane; p++, k += 3)
        {
            inputData[p] = rgb[k] * inv;
            inputData[plane + p] = rgb[k + 1] * inv;
            inputData[2 * plane + p] = rgb[k + 2] * inv;
        }

        Tensor<float> input = new Tensor<float>(new TensorShape(1, 3, S, S), inputData);
        try
        {
            worker.Schedule(input);
            Tensor<float> output = worker.PeekOutput() as Tensor<float>;
            Tensor<float> cpu = await output.ReadbackAndCloneAsync();
            try
            {
                return Decode(cpu);
            }
            finally
            {
                cpu.Dispose();
            }
        }
        finally
        {
            input.Dispose();
        }
    }

    PersonBox? Decode(Tensor<float> t)
    {
        TensorShape shape = t.shape;
        if (shape.rank != 3)
        {
            Debug.LogError($"[YOLO] nieoczekiwany ksztalt wyjscia: {shape}");
            return null;
        }

        int d1 = shape[1];
        int d2 = shape[2];
        bool channelsFirst = d1 < d2;               // [1, 84, 8400] (standard) albo [1, 8400, 84]
        int C = channelsFirst ? d1 : d2;
        int N = channelsFirst ? d2 : d1;
        int scoreRow = 4 + personClassIndex;
        if (scoreRow >= C)
        {
            Debug.LogError($"[YOLO] model ma tylko {C - 4} klas");
            return null;
        }

        float[] data = t.DownloadToArray();
        float invS = 1f / inputSize;

        int best = -1;
        float bestRank = 0f;
        float bestScore = 0f;

        for (int a = 0; a < N; a++)
        {
            float score = channelsFirst ? data[scoreRow * N + a] : data[a * C + scoreRow];
            if (score < minScore) continue;

            float cx = (channelsFirst ? data[a] : data[a * C]) * invS;
            float cy = (channelsFirst ? data[N + a] : data[a * C + 1]) * invS;
            float dx = cx - 0.5f, dy = cy - 0.5f;
            float dist2 = (dx * dx + dy * dy) / (0.25f * 0.25f);
            float rank = score / (1f + centerPreference * dist2);

            if (rank > bestRank)
            {
                bestRank = rank;
                bestScore = score;
                best = a;
            }
        }

        if (best < 0) return null;

        PersonBox box;
        box.cx = (channelsFirst ? data[best] : data[best * C]) * invS;
        box.cy = (channelsFirst ? data[N + best] : data[best * C + 1]) * invS;
        box.w = (channelsFirst ? data[2 * N + best] : data[best * C + 2]) * invS;
        box.h = (channelsFirst ? data[3 * N + best] : data[best * C + 3]) * invS;
        box.score = bestScore;

        if (box.h <= 0f || box.w <= 0f) return null;
        return box;
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}