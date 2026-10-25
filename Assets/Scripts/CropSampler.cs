using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;


public struct SensorCrop
{
    public float centerX;
    public float centerY;
    public float side;

    public SensorCrop(float centerX, float centerY, float side)
    {
        this.centerX = centerX;
        this.centerY = centerY;
        this.side = side;
    }

    public float Left { get { return centerX - side * 0.5f; } }
    public float Top { get { return centerY - side * 0.5f; } }

    public static SensorCrop Centered(int imageWidth, int imageHeight, float side)
    {
        return new SensorCrop(imageWidth * 0.5f, imageHeight * 0.5f, side);
    }
}


public static class CropSampler
{
    public static Vector2 ModelToSensor(SensorCrop crop, float u, float v, bool rotateClockwise)
    {
        float sx, sy;
        if (rotateClockwise)
        {
            sx = crop.Left + (1f - v) * crop.side;
            sy = crop.Top + (1f - u) * crop.side;
        }
        else
        {
            sx = crop.Left + v * crop.side;
            sy = crop.Top + u * crop.side;
        }
        return new Vector2(sx, sy);
    }


    public static bool Sample(XRCpuImage image, SensorCrop crop, int outSize, bool rotateClockwise,
                              byte padValue, byte[] output)
    {
        int imgW = image.width;
        int imgH = image.height;
        int S = outSize;

        // --- 1. Zakres kolumn obrazu potrzebny dla wycinka -------------------------------
        // Wiersze konwertujemy zawsze w calosci (y = 0..imgH). Dzieki temu nie zalezymy
        // od tego, czy RectInt.y w inputRect liczy sie od gory czy od dolu obrazu
        // (dokumentacja tego nie precyzuje). Os X (kolumny) nie ma tej niejednoznacznosci.
        int x0 = Mathf.FloorToInt(crop.Left) - 1;
        int x1 = Mathf.CeilToInt(crop.Left + crop.side) + 1;
        x0 = Mathf.Clamp(x0, 0, imgW);
        x1 = Mathf.Clamp(x1, 0, imgW);
        x0 &= ~1;                                   // parzyste wspolrzedne (YUV 4:2:0)
        if (((x1 - x0) & 1) != 0 && x1 < imgW) x1++;
        int regionW = x1 - x0;

        bool cropTouchesImage = regionW >= 2 &&
                                crop.Top < imgH && crop.Top + crop.side > 0f;
        if (!cropTouchesImage)
        {
            Fill(output, S, padValue);
            return false;
        }

        // --- 2. Poziom zmniejszenia w Convert (1, 2 lub 4) ------------------------------
        // Zostawiamy co najmniej 2 piksele bufora na 1 piksel wyjscia, a reszte
        // skalowania robi interpolacja (lagodzi aliasing przy duzych wycinkach).
        int level = 1;
        while (level < 4 && crop.side / (level * 2) >= 2f * S) level *= 2;

        int outW = Mathf.Max(1, regionW / level);
        int outH = Mathf.Max(1, imgH / level);

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(x0, 0, regionW, imgH),
            outputDimensions = new Vector2Int(outW, outH),
            outputFormat = TextureFormat.RGB24,
            transformation = XRCpuImage.Transformation.None
        };

        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.TempJob);
        try
        {
            image.Convert(conversionParams, buffer);

            int stride = size / outH;                // bajty na wiersz bufora
            float scaleX = outW / (float)regionW;    // piksele bufora na piksel sensora
            float scaleY = outH / (float)imgH;

            // --- 3. Tablice pomocnicze (wiersz wyjscia -> kolumna bufora, kolumna wyjscia -> wiersz bufora)
            var rowValid = new bool[S];
            var rx0 = new int[S];
            var rx1 = new int[S];
            var rfx = new float[S];

            var colValid = new bool[S];
            var cy0 = new int[S];
            var cy1 = new int[S];
            var cfy = new float[S];

            float left = crop.Left;
            float top = crop.Top;
            float side = crop.side;
            float invS = 1f / S;

            for (int i = 0; i < S; i++)
            {
                float v = (i + 0.5f) * invS;
                float sx = rotateClockwise ? left + (1f - v) * side : left + v * side;
                rowValid[i] = sx >= 0f && sx < imgW;

                float bx = (sx - x0) * scaleX - 0.5f;
                int ix = Mathf.FloorToInt(bx);
                rfx[i] = bx - ix;
                rx0[i] = Mathf.Clamp(ix, 0, outW - 1) * 3;
                rx1[i] = Mathf.Clamp(ix + 1, 0, outW - 1) * 3;
            }

            for (int j = 0; j < S; j++)
            {
                float u = (j + 0.5f) * invS;
                float sy = rotateClockwise ? top + (1f - u) * side : top + u * side;
                colValid[j] = sy >= 0f && sy < imgH;

                float by = sy * scaleY - 0.5f;
                int iy = Mathf.FloorToInt(by);
                cfy[j] = by - iy;
                cy0[j] = Mathf.Clamp(iy, 0, outH - 1) * stride;
                cy1[j] = Mathf.Clamp(iy + 1, 0, outH - 1) * stride;
            }

            // --- 4. Interpolacja dwuliniowa -------------------------------------------------
            int o = 0;
            for (int i = 0; i < S; i++)
            {
                bool rv = rowValid[i];
                int xa = rx0[i], xb = rx1[i];
                float fx = rfx[i];
                float gx = 1f - fx;

                for (int j = 0; j < S; j++)
                {
                    if (!rv || !colValid[j])
                    {
                        output[o] = padValue;
                        output[o + 1] = padValue;
                        output[o + 2] = padValue;
                        o += 3;
                        continue;
                    }

                    float fy = cfy[j];
                    float gy = 1f - fy;
                    float w00 = gx * gy, w01 = fx * gy, w10 = gx * fy, w11 = fx * fy;

                    int a = cy0[j] + xa;
                    int b = cy0[j] + xb;
                    int c = cy1[j] + xa;
                    int d = cy1[j] + xb;

                    for (int ch = 0; ch < 3; ch++)
                    {
                        float val = buffer[a + ch] * w00 + buffer[b + ch] * w01 +
                                    buffer[c + ch] * w10 + buffer[d + ch] * w11;
                        output[o + ch] = (byte)Mathf.Clamp(val + 0.5f, 0f, 255f);
                    }
                    o += 3;
                }
            }
        }
        finally
        {
            buffer.Dispose();
        }
        return true;
    }

    static void Fill(byte[] output, int S, byte value)
    {
        int n = S * S * 3;
        for (int k = 0; k < n; k++) output[k] = value;
    }
}