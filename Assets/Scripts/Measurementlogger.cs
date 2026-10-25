using UnityEngine;
using TMPro;
using System.IO;
using System.Text;
using System.Globalization;


public class MeasurementLogger : MonoBehaviour
{
    [Header("Referencje")]
    public RangeFinder rangeFinder;
    [Tooltip("Tryb AR. Puste = wyszukiwane automatycznie w scenie.")]
    public ArRangefinder arRangefinder;
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
        if (arRangefinder == null)
        {
            arRangefinder = FindFirstObjectByType<ArRangefinder>(FindObjectsInactive.Include);
        }

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
            "miejsce",
            "wiek_odczytu_s"
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
        bool arMode = arRangefinder != null && arRangefinder.isActiveAndEnabled;
        bool heightMode = !arMode && rangeFinder != null && rangeFinder.isActiveAndEnabled;

        if (!arMode && !heightMode)
        {
            UpdateStatus("<color=#FF6B6B>BLAD: brak aktywnego trybu pomiaru</color>");
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

        var ci = CultureInfo.InvariantCulture;

        float measured;
        string mode;
        string age = "";
        string zoom = "", ratio = "", noseConf = "", ankleL = "", ankleR = "", imageInfo = "";

        if (arMode)
        {
            if (!arRangefinder.HasFreshHit)
            {
                UpdateStatus("<color=#FF6B6B>BRAK TRAFIENIA AR - nie zapisano. Wyceluj w osobe/podloze.</color>");
                return;
            }
            measured = arRangefinder.lastDistance;
            mode = "AR";
            age = arRangefinder.HitAge.ToString("F2", ci);
        }
        else
        {
            if (rangeFinder.autoDetectMode && !rangeFinder.HasFreshDetection)
            {
                UpdateStatus("<color=#FF6B6B>BRAK OSOBY W KADRZE - nie zapisano.</color>");
                return;
            }
            measured = rangeFinder.lastDistance;
            mode = rangeFinder.autoDetectMode ? "auto" : "reczny";
            zoom = rangeFinder.lastZoom.ToString("F2", ci);
            ratio = rangeFinder.lastRatio.ToString("F4", ci);

            if (rangeFinder.autoDetectMode)
            {
                age = rangeFinder.DetectionAge.ToString("F2", ci);
                noseConf = rangeFinder.lastNoseConf.ToString("F2", ci);
                ankleL = rangeFinder.lastAnkleL.ToString("F2", ci);
                ankleR = rangeFinder.lastAnkleR.ToString("F2", ci);
                imageInfo = rangeFinder.debugImageInfo;
            }
        }

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

        string refHeight = rangeFinder != null ? rangeFinder.referenceHeight.ToString("F3", ci) : "";
        string focal = rangeFinder != null ? rangeFinder.focalNormalized.ToString("F6", ci) : "";

        recordCount++;

        string line = string.Join(";", new string[]
        {
            recordCount.ToString(),
            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            series,
            realDistance.ToString("F3", ci),
            measured.ToString("F3", ci),
            error.ToString("F3", ci),
            errorPct.ToString("F2", ci),
            mode,
            refHeight,
            person,
            focal,
            profileLabel,
            recognized,
            zoom,
            ratio,
            noseConf,
            ankleL,
            ankleR,
            imageInfo,
            SystemInfo.deviceModel,
            lighting,
            location,
            age
        });

        try
        {
            File.AppendAllText(filePath, line + "\n", Encoding.UTF8);
            UpdateStatus($"Zapisano #{recordCount} [{mode}]: {measured:F2} m (blad {error:+0.00;-0.00} m) | {series}");
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