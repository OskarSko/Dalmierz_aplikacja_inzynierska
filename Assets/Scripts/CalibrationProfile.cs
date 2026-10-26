using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CalibrationProfile : MonoBehaviour
{
    [System.Serializable]
    public class DeviceProfile
    {
        [Tooltip("Fragment nazwy urzadzenia, np. SM-S911B lub iPhone13. Porownanie ignoruje wielkosc liter.")]
        public string deviceModelPattern = "SM-S911B";

        [Tooltip("Nazwa czytelna - trafia do pliku CSV i do opisu w pracy")]
        public string label = "Samsung Galaxy S23";

        [Header("Wynik kalibracji")]
        public float focalNormalized = 0.673625f;

        [Header("Dane zrodlowe - dokumentacja do pracy")]
        public int calibrationWidth = 1920;
        public int calibrationHeight = 1080;
        public float fx = 1293.360f;
        public float fy = 1294.588f;
        public float cx = 957.978f;
        public float cy = 530.621f;
        public float rmsReprojectionError = 0.27f;
        public float horizontalFovDegrees = 73.17f;
        public int imagesUsed = 24;
        [TextArea(2, 4)]
        public string notes = "Srednia z dwoch niezaleznych serii po 24 zdjecia szachownicy.";
    }

    [Header("Referencje")]
    public RangeFinder rangeFinder;

    [Header("UI (opcjonalne)")]
    public TextMeshProUGUI profileStatusText;

    [Header("Profile urzadzen")]
    public List<DeviceProfile> profiles = new List<DeviceProfile>();

    [Header("Zachowanie przy nieznanym urzadzeniu")]
    [Tooltip("Wartosc uzywana, gdy model urzadzenia nie pasuje do zadnego profilu.")]
    public float fallbackFocalNormalized = 0.673625f;

    /// <summary>Nazwa zastosowanego profilu - czytana przez rejestrator pomiarow.</summary>
    [HideInInspector] public string activeProfileLabel = "BRAK PROFILU";

    /// <summary>True, gdy urzadzenie zostalo rozpoznane w liscie profili.</summary>
    [HideInInspector] public bool deviceRecognized = false;

    void Awake()
    {
        ApplyProfile();
    }

    public void ApplyProfile()
    {
        if (rangeFinder == null)
        {
            SetStatus("<color=#FF6B6B>BRAK referencji do RangeFinder</color>");
            return;
        }

        DeviceProfile match = FindProfile(SystemInfo.deviceModel);

        if (match != null)
        {
            rangeFinder.focalNormalized = match.focalNormalized;
            activeProfileLabel = match.label;
            deviceRecognized = true;

            SetStatus(
                $"<color=#22D3EE>Profil: {match.label}</color>\n" +
                $"f_n = {match.focalNormalized:F6}   FOV = {match.horizontalFovDegrees:F2}°   " +
                $"blad reproj. = {match.rmsReprojectionError:F2} px" +
                CheckProfileConsistency(match));
        }
        else
        {
            rangeFinder.focalNormalized = fallbackFocalNormalized;
            activeProfileLabel = "BRAK PROFILU";
            deviceRecognized = false;

            SetStatus(
                "<color=#FFD166>UWAGA: urzadzenie nieskalibrowane.</color>\n" +
                $"Uzyto wartosci zapasowej f_n = {fallbackFocalNormalized:F6}.\n" +
                "Pomiary moga byc obarczone bledem systematycznym.");
        }
    }

    /// <summary>
    /// Sprawdza, czy dane zrodlowe profilu sa uzupelnione i zgodne z f_n = fx / szerokosc.
    /// Zwraca ostrzezenie do panelu ustawien albo pusty tekst.
    /// </summary>
    string CheckProfileConsistency(DeviceProfile p)
    {
        if (p.calibrationWidth <= 0 || p.calibrationHeight <= 0 || p.fx <= 0f || p.imagesUsed <= 0)
        {
            return "\n<color=#FFD166>Profil: uzupelnij dane zrodlowe (rozdzielczosc, fx, liczba zdjec).</color>";
        }

        float fnFromFx = p.fx / p.calibrationWidth;
        if (Mathf.Abs(fnFromFx - p.focalNormalized) > 0.0001f)
        {
            return $"\n<color=#FFD166>Niezgodnosc: fx/szerokosc = {fnFromFx:F6}, a f_n = {p.focalNormalized:F6}</color>";
        }
        return "";
    }

    DeviceProfile FindProfile(string device)
    {
        string d = device.ToLowerInvariant();
        foreach (var p in profiles)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.deviceModelPattern)) continue;
            if (d.Contains(p.deviceModelPattern.ToLowerInvariant())) return p;
        }
        return null;
    }
    public void CopyDeviceModelToClipboard()
    {
        GUIUtility.systemCopyBuffer = SystemInfo.deviceModel;
        Debug.Log($"[Kalibracja] Skopiowano do schowka: {SystemInfo.deviceModel}");
        if (profileStatusText != null)
        {
            profileStatusText.text =
                $"<color=#22D3EE>Skopiowano nazwe urzadzenia:</color>\n{SystemInfo.deviceModel}";
        }
    }

    void SetStatus(string msg)
    {
        string full = $"{msg}\n<size=70%><color=#B0B0B0>Urzadzenie: {SystemInfo.deviceModel}</color></size>";
        if (profileStatusText != null) profileStatusText.text = full;
        Debug.Log($"[Kalibracja] {msg} | urzadzenie: {SystemInfo.deviceModel}");
    }
}