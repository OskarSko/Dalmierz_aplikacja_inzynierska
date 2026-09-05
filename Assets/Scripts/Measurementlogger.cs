using UnityEngine;
using TMPro;
using System.IO;
using System.Text;
using System.Globalization;

/// <summary>
/// Rejestruje pojedyncze pomiary do pliku CSV w pamieci urzadzenia.
/// Plik sluzy do pozniejszej analizy statystycznej (MAE, RMSE,
/// odchylenie standardowe, wykresy bledu w funkcji odleglosci).
///
/// Separator: srednik. Separator dziesietny: kropka (InvariantCulture).
/// </summary>
public class MeasurementLogger : MonoBehaviour
{
    [Header("Referencje")]
    public RangeFinder rangeFinder;

    [Header("UI")]
    public TMP_InputField realDistanceInput;   // odleglosc rzeczywista w metrach
    public TMP_InputField seriesLabelInput;    // opcjonalna etykieta serii
    public TextMeshProUGUI statusText;

    [Header("Warunki pomiaru (do opisu w pracy)")]
    public string lightingConditions = "dzienne";
    public string location = "pomieszczenie";

    private string filePath;
    private int recordCount = 0;

    void Start()
    {
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        filePath = Path.Combine(Application.persistentDataPath, $"pomiary_{stamp}.csv");

        WriteHeader();
        UpdateStatus("Gotowy. Plik: " + Path.GetFileName(filePath));
    }

    void WriteHeader()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", new string[]
        {
            "lp",
            "czas",
            "seria",
            "odleglosc_rzeczywista_m",
            "odleglosc_zmierzona_m",
            "blad_m",
            "blad_procent",
            "tryb",
            "wysokosc_odniesienia_m",
            "focal_normalized",
            "zoom",
            "ratio",
            "pewnosc_nos",
            "pewnosc_kostka_L",
            "pewnosc_kostka_P",
            "rozdzielczosc_obrazu",
            "urzadzenie",
            "oswietlenie",
            "miejsce"
        }));

        try
        {
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Logger] Nie mozna utworzyc pliku: {e}");
        }
    }

    /// <summary>Podepnij pod przycisk UI (OnClick).</summary>
    public void RecordMeasurement()
    {
        if (rangeFinder == null)
        {
            UpdateStatus("BLAD: brak referencji do RangeFinder");
            return;
        }

        float realDistance;
        string rawInput = realDistanceInput != null ? realDistanceInput.text : "";
        rawInput = rawInput.Replace(',', '.');

        if (!float.TryParse(rawInput, NumberStyles.Float, CultureInfo.InvariantCulture, out realDistance))
        {
            UpdateStatus("BLAD: wpisz odleglosc rzeczywista");
            return;
        }

        float measured = rangeFinder.lastDistance;
        float error = measured - realDistance;
        float errorPct = realDistance > 0.001f ? (error / realDistance) * 100f : 0f;

        string series = seriesLabelInput != null && !string.IsNullOrEmpty(seriesLabelInput.text)
            ? seriesLabelInput.text
            : "brak";

        recordCount++;

        var ci = CultureInfo.InvariantCulture;
        string line = string.Join(";", new string[]
        {
            recordCount.ToString(),
            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            series,
            realDistance.ToString("F3", ci),
            measured.ToString("F3", ci),
            error.ToString("F3", ci),
            errorPct.ToString("F2", ci),
            rangeFinder.autoDetectMode ? "auto" : "reczny",
            rangeFinder.referenceHeight.ToString("F3", ci),
            rangeFinder.focalNormalized.ToString("F6", ci),
            rangeFinder.lastZoom.ToString("F2", ci),
            rangeFinder.lastRatio.ToString("F4", ci),
            rangeFinder.lastNoseConf.ToString("F2", ci),
            rangeFinder.lastAnkleL.ToString("F2", ci),
            rangeFinder.lastAnkleR.ToString("F2", ci),
            rangeFinder.debugImageInfo,
            SystemInfo.deviceModel,
            lightingConditions,
            location
        });

        try
        {
            File.AppendAllText(filePath, line + "\n", Encoding.UTF8);
            UpdateStatus($"Zapisano #{recordCount}: {measured:F2} m (blad {error:+0.00;-0.00} m)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Logger] Blad zapisu: {e}");
            UpdateStatus("BLAD zapisu do pliku");
        }
    }

    /// <summary>Pokazuje sciezke pliku - przydatne przy pobieraniu przez adb.</summary>
    public void ShowFilePath()
    {
        Debug.Log($"[Logger] Plik: {filePath}");
        UpdateStatus(Path.GetFileName(filePath) + " | rekordow: " + recordCount);
    }

    void UpdateStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[Logger] " + msg);
    }
}