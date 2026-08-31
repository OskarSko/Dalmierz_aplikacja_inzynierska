using UnityEngine;
using Unity.InferenceEngine;

public struct PoseKeypoints
{
    public Vector2 nose;
    public Vector2 leftEye, rightEye;
    public Vector2 leftAnkle, rightAnkle;
    public float noseConfidence;
    public float leftAnkleConfidence, rightAnkleConfidence;

    public float leftEyeConfidence, rightEyeConfidence;
}

public class PoseKeypointUtils
{
    const int NOSE = 0, L_EYE = 1, R_EYE = 2, L_ANKLE = 15, R_ANKLE = 16;

    public static PoseKeypoints Parse(Tensor<float> tensor)
    {
        var data = tensor.DownloadToArray();

        Vector3 Get(int idx) => new Vector3(
            data[idx * 3 + 1],
            data[idx * 3 + 0],
            data[idx * 3 + 2]
        );

        var nose = Get(NOSE);
        var lEye = Get(L_EYE);
        var rEye = Get(R_EYE);
        var lAnkle = Get(L_ANKLE);
        var rAnkle = Get(R_ANKLE);

        return new PoseKeypoints
        {
            nose = new Vector2(nose.x, nose.y),
            leftEye = new Vector2(lEye.x, lEye.y),
            rightEye = new Vector2(rEye.x, rEye.y),
            leftAnkle = new Vector2(lAnkle.x, lAnkle.y),
            rightAnkle = new Vector2(rAnkle.x, rAnkle.y),
            noseConfidence = nose.z,
            leftAnkleConfidence = lAnkle.z,
            rightAnkleConfidence = rAnkle.z,
            leftEyeConfidence = lEye.z,
            rightEyeConfidence = rEye.z
        };
    }
}
public static class HeadFootEstimator
{
    public static Vector2 EstimateHeadTop(PoseKeypoints kp)
    {
        float eyeDistance = Vector2.Distance(kp.leftEye, kp.rightEye);
        Vector2 upOffset = new Vector2(0, -eyeDistance * 2.7f);
        return kp.nose + upOffset;
    }

    public static Vector2 EstimateFeet(PoseKeypoints kp)
    {
        if (kp.leftAnkleConfidence < 0.2f) return kp.rightAnkle;
        if (kp.rightAnkleConfidence < 0.2f) return kp.leftAnkle;
        return kp.leftAnkle.y > kp.rightAnkle.y ? kp.leftAnkle : kp.rightAnkle;
    }
}
