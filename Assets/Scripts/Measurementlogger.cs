using UnityEngine;
using TMPro;
using System.IO;
using System.Text;
using System.Globalization;


public class MeasurementLogger : MonoBehaviour
{
    [Header("Referencje")]
    public RangeFinder rangeFinder;
    public CalibrationProfile calibrationProfile;

    [Header("UI - dane pomiaru")]
    public TMP_InputField realDistanceInput;   // odleglosc rzeczywista w metrach
    public TMP_InputField seriesLabelInput;    // etykieta serii, np. "A_reczny"
    public TextMeshProUGUI statusText;

    [Header("UI - warunki pomiaru (panel ustawien)")]
    public TMP_InputField lightingInput;       // np. dzienne / sztuczne / zmierzch
    public TMP_InputField locationInput;       // np. pomieszczenie / na zewnatrz
    public TMP_InputField referencePersonInput; // np. osoba_A

    [Header("Wartosci domyslne warunkow")]
    public string defaultLighting = "dzienne";
    public string defaultLocation = "pomieszczenie";
    public string defaultReferencePerson = "osoba_A";

    private string filePath;
    private int recordCount = 0;

    void Start()
    {
        if (lightingInput != null && string.IsNullOrEmpty(lightingInput.text))
            lightingInput.text = defaultLighting;
        if (locationInput != null && string.IsNullOrEmpty(locationInput.text))
            locationInput.text = defaultLocation;
        if (referencePersonInput != null && string.IsNullOrEmpty(referencePersonInput.text))
            referencePersonInput.text = defaultReferencePerson;

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
            "osoba_odniesienia",
            "focal_normalized",
            "profil_kalibracji",
            "urzadzenie_rozpoznane",
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
            UpdateStatus("<color=#FF6B6B>BLAD: brak referencji do RangeFinder</color>");
            return;
        }

        float realDistance;
        string rawInput = realDistanceInput != null ? realDistanceInput.text : "";
        rawInput = rawInput.Replace(',', '.');

        if (!float.TryParse(rawInput, NumberStyles.Float, CultureInfo.InvariantCulture, out realDistance))
        {
            UpdateStatus("<color=#FF6B6B>BLAD: wpisz odleglosc rzeczywista</color>");
            return;
        }

        float measured = rangeFinder.lastDistance;
        float error = measured - realDistance;
        float errorPct = realDistance > 0.001f ? (error / realDistance) * 100f : 0f;

        string series = Field(seriesLabelInput, "brak");
        string lighting = Field(lightingInput, defaultLighting);
        string location = Field(locationInput, defaultLocation);
        string person = Field(referencePersonInput, defaultReferencePerson);

        string profileLabel = calibrationProfile != null ? calibrationProfile.activeProfileLabel : "n/d";
        string recognized = calibrationProfile != null
            ? (calibrationProfile.deviceRecognized ? "tak" : "nie")
            : "n/d";

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
            person,
            rangeFinder.focalNormalized.ToString("F6", ci),
            profileLabel,
            recognized,
            rangeFinder.lastZoom.ToString("F2", ci),
            rangeFinder.lastRatio.ToString("F4", ci),
            rangeFinder.lastNoseConf.ToString("F2", ci),
            rangeFinder.lastAnkleL.ToString("F2", ci),
            rangeFinder.lastAnkleR.ToString("F2", ci),
            rangeFinder.debugImageInfo,
            SystemInfo.deviceModel,
            lighting,
            location
        });

        try
        {
            File.AppendAllText(filePath, line + "\n", Encoding.UTF8);
            UpdateStatus($"Zapisano #{recordCount}: {measured:F2} m (blad {error:+0.00;-0.00} m) | {series}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Logger] Blad zapisu: {e}");
            UpdateStatus("<color=#FF6B6B>BLAD zapisu do pliku</color>");
        }
    }

    string Field(TMP_InputField input, string fallback)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.text)) return fallback;
        // srednik rozbilby strukture CSV
        return input.text.Replace(';', ',').Trim();
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