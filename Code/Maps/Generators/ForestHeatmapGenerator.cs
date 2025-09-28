using System;
using Godot;

namespace tacticals.Code.Maps.Generators;

public static class ForestHeatmapGenerator
{
    public static Image GenerateBiomes(Vector2I resolution)
    {
        int width = resolution.X, height = resolution.Y;
        float[,] slope = new float[height, width];
        float[,] elev  = new float[height, width];
        float[,] distW = new float[height, width];

// Fill with your real data. For a quick demo:
        // for (int y = 0; y < height; y++)
        // for (int x = 0; x < width; x++)
        // {
        // 	elev[y,x]  = 3 + 1 * (float)Math.Sin(x * 0.004) * (float)Math.Cos(y * 0.003);
        // 	slope[y,x] = 1 + 1 * (float)Math.Abs(Math.Sin(x * 0.01) * Math.Cos(y * 0.01));
        // 	distW[y,x] = 5 + 20 * (float)Math.Abs(Math.Sin((x+y) * 0.002));
        // }

        var freq = (float) new Random().NextDouble() / 100;
			
        var bmp = ForestHeatmapGenerator.GenerateHeatmapBitmap(
            slope, elev, distW,
            baseFreq: freq, octaves: 5,
            slopeMin: 0, slopeMax: 100,
            elevMid: 10, elevWidth: 10,
            waterNear: 300, waterFar: 1000,
            blurRadius: 1, rngSeed: 1337);
		
        return bmp;
    }

    /// <summary>
    /// Generates a forest heat map bitmap in ARGB32.
    /// Inputs are per-tile arrays: slope in degrees, elevation (meters), and distance to water (meters).
    /// </summary>
    /// <param name="slopeDeg">[height,width] slope in degrees</param>
    /// <param name="elevation">[height,width] elevation in meters</param>
    /// <param name="distToWater">[height,width] distance to nearest water in meters</param>
    /// <param name="baseFreq">Noise base frequency (tiles^-1). Try 0.003f.</param>
    /// <param name="octaves">fBM octaves (1–6). Try 3.</param>
    /// <param name="slopeMin">Slope start for clearing (deg). Trees prefer below this. e.g., 10</param>
    /// <param name="slopeMax">Slope fully cleared (deg). e.g., 25</param>
    /// <param name="elevMid">Preferred elevation center (m). e.g., 450</param>
    /// <param name="elevWidth">Preferred elevation half-width (sigma). e.g., 150</param>
    /// <param name="waterNear">Distance where water effect is strongest (m). e.g., 30</param>
    /// <param name="waterFar">Distance where water effect fades out (m). e.g., 150</param>
    /// <param name="blurRadius">Gaussian blur radius in pixels (0–3). Try 1.</param>
    /// <param name="rngSeed">Deterministic seed for noise.</param>
    public static Image GenerateHeatmapBitmap(
        float[,] slopeDeg,
        float[,] elevation,
        float[,] distToWater,
        float baseFreq = 0.003f,
        int octaves = 3,
        float slopeMin = 10f,
        float slopeMax = 25f,
        float elevMid = 450f,
        float elevWidth = 150f,
        float waterNear = 30f,
        float waterFar = 150f,
        int blurRadius = 1,
        int rngSeed = 1337)
    {
        if (slopeDeg == null || elevation == null || distToWater == null)
            throw new ArgumentNullException("Input arrays cannot be null.");
        int h = slopeDeg.GetLength(0);
        int w = slopeDeg.GetLength(1);
        if (elevation.GetLength(0) != h || elevation.GetLength(1) != w ||
            distToWater.GetLength(0) != h || distToWater.GetLength(1) != w)
            throw new ArgumentException("All input arrays must have identical [height,width].");

        var rnd = new Random(rngSeed);
        // Per-octave offsets to avoid axis-aligned artifacts (domain warping lite)
        var off = new (float ox, float oy)[Math.Max(1, octaves)];
        for (int i = 0; i < off.Length; i++)
            off[i] = ((float)rnd.NextDouble() * 1000f, (float)rnd.NextDouble() * 1000f);

        float[,] H = new float[h, w];

        // 1) Build heat map H
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // fBM noise in [0,1]
                float n = FbmNoise(x, y, baseFreq, octaves, off);
                // Slightly bias toward higher values for denser clumps
                float N = 0.6f * n + 0.4f * n * n;

                // Masks
                float S = 1f - Smoothstep(slopeMin, slopeMax, slopeDeg[y, x]); // trees dislike high slope
                float E = BellCurve(elevation[y, x], elevMid, elevWidth);      // prefer mid elevation
                float W = 1f - Smoothstep(waterNear, waterFar, distToWater[y, x]); // prefer near water

                float v = Clamp01(N * S * E * W);
                H[y, x] = v;
            }
        }

        // 2) Optional tiny blur to soften blockiness
        if (blurRadius > 0)
            H = GaussianBlur(H, blurRadius, sigma: blurRadius * 0.66f + 0.5f);

        // 3) Normalize to [0,1]
        Normalize01InPlace(H);

        // 4) Convert to bitmap with a readable gradient
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(Colors.White); // Optional

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = H[y, x];
                var c = HeatGradient(v); // blue->green->yellow->red
                img.SetPixel(x, y, c);
            }
        }
        return img;
    }

    // ===== Math helpers =====

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float BellCurve(float x, float mean, float sigma)
    {
        float d = x - mean;
        float s2 = 2f * sigma * sigma + 1e-6f;
        return (float)Math.Exp(-(d * d) / s2);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private static void Normalize01InPlace(float[,] a)
    {
        int h = a.GetLength(0), w = a.GetLength(1);
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            { float v = a[y, x]; if (v < min) min = v; if (v > max) max = v; }
        float span = Math.Max(1e-6f, max - min);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                a[y, x] = (a[y, x] - min) / span;
    }

    // ===== Noise (value/perlin-like gradient noise) + fBM =====

    private static float FbmNoise(int x, int y, float baseFreq, int octaves, (float ox, float oy)[] offs)
    {
        float amp = 0.5f;
        float sum = 0f;
        float norm = 0f;
        float freq = baseFreq;
        for (int i = 0; i < Math.Max(1, octaves); i++)
        {
            float nx = (x * freq) + offs[i].ox;
            float ny = (y * freq) + offs[i].oy;
            float n = Perlin(nx, ny) * 0.5f + 0.5f; // [-1,1] -> [0,1]
            sum += n * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / Math.Max(1e-6f, norm);
    }

    // 2D Perlin-style gradient noise
    private static float Perlin(float x, float y)
    {
        int x0 = FastFloor(x), y0 = FastFloor(y);
        int x1 = x0 + 1, y1 = y0 + 1;

        float sx = x - x0, sy = y - y0;

        float n00 = Grad(Hash(x0, y0), x - x0, y - y0);
        float n10 = Grad(Hash(x1, y0), x - x1, y - y0);
        float n01 = Grad(Hash(x0, y1), x - x0, y - y1);
        float n11 = Grad(Hash(x1, y1), x - x1, y - y1);

        float u = Fade(sx);
        float v = Fade(sy);

        float ix0 = Lerp(n00, n10, u);
        float ix1 = Lerp(n01, n11, u);
        float value = Lerp(ix0, ix1, v);
        // clamp tiny overshoots
        if (value < -1f) value = -1f; else if (value > 1f) value = 1f;
        return value;
    }

    private static int FastFloor(float x) => (x >= 0f) ? (int)x : (int)x - 1;

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // Hash to get repeatable gradients per lattice point
    private static int Hash(int x, int y)
    {
        const int PRIME1 = 73856093;
        const int PRIME2 = 19349663;
        int h = x * PRIME1 ^ y * PRIME2;
        // final shuffle
        h ^= (h >> 13);
        h *= 0x5bd1e995;
        h ^= (h >> 15);
        return h;
    }

    private static float Grad(int hash, float x, float y)
    {
        // 8 gradient directions
        switch (hash & 7)
        {
            case 0: return  x + y;
            case 1: return  x - y;
            case 2: return -x + y;
            case 3: return -x - y;
            case 4: return  x;
            case 5: return -x;
            case 6: return  y;
            default: return -y;
        }
    }

    // ===== Gaussian blur (separable) =====

    private static float[,] GaussianBlur(float[,] src, int radius, float sigma)
    {
        if (radius <= 0) return src;
        int h = src.GetLength(0), w = src.GetLength(1);
        float[] kernel = BuildGaussianKernel(radius, sigma);

        float[,] tmp = new float[h, w];
        // Horizontal
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                float norm = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int xi = Clamp(x + k, 0, w - 1);
                    float kv = kernel[k + radius];
                    acc += src[y, xi] * kv;
                    norm += kv;
                }
                tmp[y, x] = acc / norm;
            }
        }
        float[,] dst = new float[h, w];
        // Vertical
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                float norm = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int yi = Clamp(y + k, 0, h - 1);
                    float kv = kernel[k + radius];
                    acc += tmp[yi, x] * kv;
                    norm += kv;
                }
                dst[y, x] = acc / norm;
            }
        }
        return dst;
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static float[] BuildGaussianKernel(int radius, float sigma)
    {
        int size = radius * 2 + 1;
        float[] k = new float[size];
        float s2 = 2f * sigma * sigma + 1e-6f;
        float norm = 0f;
        for (int i = -radius; i <= radius; i++)
        {
            float v = (float)Math.Exp(-(i * i) / s2);
            k[i + radius] = v;
            norm += v;
        }
        // normalize once
        for (int i = 0; i < size; i++) k[i] /= norm;
        return k;
    }

    // ===== Color mapping (blue->green->yellow->red) =====

    private static Color HeatGradient(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);

        if (t < 1f / 3f)
        {
            float k = t / (1f / 3f);
            return LerpColor(new Color(0f, 0f, 0.7f, 1f), new Color(0f, 0.7f, 0f, 1f), k); // blue → green
        }
        else if (t < 2f / 3f)
        {
            float k = (t - 1f / 3f) / (1f / 3f);
            return LerpColor(new Color(0f, 0.7f, 0f, 1f), new Color(1f, 1f, 0f, 1f), k);   // green → yellow
        }
        else
        {
            float k = (t - 2f / 3f) / (1f / 3f);
            return LerpColor(new Color(1f, 1f, 0f, 1f), new Color(0.9f, 0f, 0f, 1f), k);   // yellow → red
        }
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        return new Color(
            Mathf.Lerp(a.R, b.R, t),
            Mathf.Lerp(a.G, b.G, t),
            Mathf.Lerp(a.B, b.B, t),
            Mathf.Lerp(a.A, b.A, t) // keep alpha = 1
        );
    }
}