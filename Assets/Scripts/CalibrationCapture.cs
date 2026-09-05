using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using System.IO;

/// <summary>
/// Zapisuje pojedyncze klatki ze strumienia CPU kamery do plikow PNG.
/// Klatki sluza do kalibracji wewnetrznych parametrow kamery (intrinsics)
/// metoda szachownicy. Kluczowe jest, aby pochodzily z tego samego
/// strumienia i tej samej rozdzielczosci, ktorej uzywa modul pomiarowy.
/// </summary>
public class CalibrationCapture : MonoBehaviour
{
    [Header("Referencje")]
    public ARCameraManager cameraManager;

    [Header("Stan")]
    public int savedCount = 0;

    private bool captureRequested = false;

    void OnEnable()
    {
        if (cameraManager != null) cameraManager.frameReceived += OnFrame;
    }

    void OnDisable()
    {
        if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
    }

    /// <summary>Podepnij pod przycisk UI (OnClick).</summary>
    public void RequestCapture()
    {
        captureRequested = true;
    }

    void OnFrame(ARCameraFrameEventArgs args)
    {
        if (!captureRequested) return;
        captureRequested = false;

        if (!cameraManager.TryAcquireLatestCpuImage(out var image))
        {
            Debug.LogError("[Kalibracja] Nie udalo sie pobrac klatki CPU");
            return;
        }

        try
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height),
                outputFormat = TextureFormat.RGB24,
                transformation = XRCpuImage.Transformation.None
            };

            int size = image.GetConvertedDataSize(conversionParams);
            using var buffer = new NativeArray<byte>(size, Allocator.Temp);
            image.Convert(conversionParams, buffer);

            var tex = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(buffer);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            string fileName = $"calib_{savedCount:D2}_{image.width}x{image.height}.png";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, png);

            savedCount++;
            Debug.Log($"[Kalibracja] Zapisano {fileName} — lacznie {savedCount}");

            Destroy(tex);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Kalibracja] Blad zapisu: {e}");
        }
        finally
        {
            image.Dispose();
        }
    }
}