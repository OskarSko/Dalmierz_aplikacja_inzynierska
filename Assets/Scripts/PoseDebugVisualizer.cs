using UnityEngine;
using UnityEngine.UI;

public class PoseDebugVisualizer : MonoBehaviour
{
    [Header("Podgląd obrazu wejściowego modelu")]
    public RawImage previewImage;

    [Header("Punkty charakterystyczne (kropki UI)")]
    public RectTransform noseDot;
    public RectTransform leftEyeDot;
    public RectTransform rightEyeDot;
    public RectTransform leftAnkleDot;
    public RectTransform rightAnkleDot;

    public void ShowFrame(Texture2D tex)
    {
        if (previewImage != null)
        {
            previewImage.texture = tex;
        }
    }

    public void ShowKeypoints(PoseKeypoints kp)
    {
        PlaceDot(noseDot, kp.nose, kp.noseConfidence);
        PlaceDot(leftEyeDot, kp.leftEye, kp.leftEyeConfidence);
        PlaceDot(rightEyeDot, kp.rightEye, kp.rightEyeConfidence);
        PlaceDot(leftAnkleDot, kp.leftAnkle, kp.leftAnkleConfidence);
        PlaceDot(rightAnkleDot, kp.rightAnkle, kp.rightAnkleConfidence);
    }

    void PlaceDot(RectTransform dot, Vector2 normalizedPos, float confidence)
    {
        if (dot == null || previewImage == null) return;

        float width = previewImage.rectTransform.rect.width;
        float height = previewImage.rectTransform.rect.height;

        float x = normalizedPos.x * width;
        float y = (1f - normalizedPos.y) * height; // UI liczy Y od dołu, model od góry

        dot.anchoredPosition = new Vector2(x, y);

        Image img = dot.GetComponent<Image>();
        if (img != null)
        {
            img.color = Color.Lerp(Color.red, Color.green, Mathf.Clamp01(confidence));
        }
    }
}