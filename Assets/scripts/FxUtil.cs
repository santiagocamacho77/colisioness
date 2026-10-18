using UnityEngine;

/// <summary>Utilidades para generar en código las mallas y texturas de los efectos (fondo, clima).</summary>
public static class FxUtil
{
    /// <summary>Quad de 1x1 en el plano XY, cara visible hacia -Z, con colores de vértice blancos.</summary>
    public static Mesh QuadMesh()
    {
        Mesh m = new Mesh { name = "FxQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    private static Texture2D NewTexture(int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    /// <summary>Punto blanco con borde suave (estrellas, gotas, rachas de viento).</summary>
    public static Texture2D SoftDot(int size = 32)
    {
        Texture2D t = NewTexture(size, size);
        Color[] px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    /// <summary>Nube achatada formada por varias manchas suaves, con la base plana.</summary>
    public static Texture2D Cloud(int w = 128, int h = 64)
    {
        // cx, cy en [0,1]; r en unidades de alto
        float[,] blobs =
        {
            { 0.22f, 0.38f, 0.28f }, { 0.38f, 0.50f, 0.36f }, { 0.55f, 0.52f, 0.38f },
            { 0.72f, 0.42f, 0.30f }, { 0.86f, 0.36f, 0.22f }, { 0.50f, 0.30f, 0.30f }
        };
        Texture2D t = NewTexture(w, h);
        Color[] px = new Color[w * h];
        float aspect = (float)w / h;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            float flat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.25f, v));
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);
                float a = 0f;
                for (int i = 0; i < blobs.GetLength(0); i++)
                {
                    float dx = (u - blobs[i, 0]) * aspect;
                    float dy = v - blobs[i, 1];
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / blobs[i, 2];
                    a = Mathf.Max(a, Mathf.Pow(Mathf.Clamp01(1f - d), 0.7f));
                }
                px[y * w + x] = new Color(1f, 1f, 1f, a * flat);
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    /// <summary>Degradado vertical de alfa: transparente abajo, opaco arriba (niebla).</summary>
    public static Texture2D VerticalFade(int h = 64)
    {
        Texture2D t = NewTexture(4, h);
        Color[] px = new Color[4 * h];
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, v));
            for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a);
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }


/// <summary>Rectángulo blanco con esquinas redondeadas y borde suavizado (paneles del HUD, 9-slice).</summary>
    public static Texture2D RoundedRect(int size, int radius)
    {
        Texture2D t = NewTexture(size, size);
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, size - radius);
                float cy = Mathf.Clamp(fy, radius, size - radius);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f));
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    /// <summary>Cubo de 1x1x1 con normales y colores de vértice blancos (para shaders de partículas).</summary>
    public static Mesh CubeMesh()
    {
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        foreach (Vector3 n in dirs)
        {
            Vector3 a = Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up);
            Vector3 b = Vector3.Cross(n, a);
            int s = verts.Count;
            verts.Add((n - a - b) * 0.5f); verts.Add((n + a - b) * 0.5f);
            verts.Add((n + a + b) * 0.5f); verts.Add((n - a + b) * 0.5f);
            for (int k = 0; k < 4; k++) norms.Add(n);
            tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        Mesh m = new Mesh { name = "FxCube" };
        m.SetVertices(verts);
        m.SetNormals(norms);
        Color[] colors = new Color[verts.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        m.colors = colors;
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }
}
