using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera las texturas (PNG) y materiales URP del escenario: un diseño por tipo de plataforma, ladrillos
/// para las paredes y césped para el suelo. Se puede volver a ejecutar: sobrescribe las texturas y
/// actualiza los materiales sin cambiar sus GUID. Menú: Climber > Generar texturas y materiales.
/// </summary>
public static class ClimberArtBaker
{
    private const string TexDir = "Assets/Art/Textures";
    private const string MatDir = "Assets/Art/Materials";
    private const int PW = 128;   // textura de plataforma: 1 m de ancho
    private const int PH = 64;    //                        0,5 m de alto

    private enum Detail { Rivets, Cracks, Dashes, Chevrons, Grass }

    [MenuItem("Climber/Generar texturas y materiales")]
    public static void Bake()
    {
        Directory.CreateDirectory(TexDir);
        Directory.CreateDirectory(MatDir);

        Texture2D normal = Save("T_Platform_Normal", Platform(new Color(0.52f, 0.72f, 1f), new Color(0.2f, 0.34f, 0.7f), 11, Detail.Rivets, false));
        Texture2D falling = Save("T_Platform_Falling", Platform(new Color(1f, 0.66f, 0.32f), new Color(0.68f, 0.28f, 0.1f), 23, Detail.Cracks, false));
        Texture2D timed = Save("T_Platform_Timed", Platform(new Color(0.76f, 0.56f, 1f), new Color(0.38f, 0.2f, 0.7f), 37, Detail.Dashes, false));
        Texture2D timedEmit = Save("T_Platform_Timed_Emit", Platform(Color.black, Color.black, 37, Detail.Dashes, true));
        Texture2D moving = Save("T_Platform_Moving", Platform(new Color(0.4f, 1f, 0.88f), new Color(0.08f, 0.48f, 0.5f), 53, Detail.Chevrons, false));
        Texture2D movingEmit = Save("T_Platform_Moving_Emit", Platform(Color.black, Color.black, 53, Detail.Chevrons, true));
        Texture2D safe = Save("T_Platform_Safe", Platform(new Color(0.58f, 0.4f, 0.25f), new Color(0.34f, 0.22f, 0.13f), 71, Detail.Grass, false));
        Texture2D wall = Save("T_Wall_Bricks", Bricks());

        Mat("M_Platform_Normal", normal, null, Color.black, Vector2.one, 0.3f);
        Mat("M_Platform_Falling", falling, null, Color.black, Vector2.one, 0.15f);
        Mat("M_Platform_Timed", timed, timedEmit, new Color(0.75f, 0.45f, 1f) * 1.6f, Vector2.one, 0.45f);
        Mat("M_Platform_Moving", moving, movingEmit, new Color(0.3f, 1f, 0.85f) * 1.3f, Vector2.one, 0.45f);
        Mat("M_Platform_Safe", safe, null, Color.black, Vector2.one, 0.1f);
        // Tiling negativo: la cara frontal del cubo de Unity tiene las UV giradas 180°
        Mat("M_Wall", wall, null, Color.black, new Vector2(-1f, -10f), 0.15f);
        Mat("M_Floor", safe, null, Color.black, new Vector2(-7f, -1f), 0.1f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ClimberArtBaker] Texturas y materiales generados en Assets/Art.");
    }

    // ------------------------------------------------------------------ Guardado

    private static Texture2D Save(string name, Texture2D tex)
    {
        string path = TexDir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.filterMode = FilterMode.Bilinear;
        imp.anisoLevel = 4;
        imp.mipmapEnabled = true;
        imp.sRGBTexture = true;
        imp.alphaSource = TextureImporterAlphaSource.None;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void Mat(string name, Texture2D baseMap, Texture2D emissionMap, Color emission, Vector2 tiling, float smoothness)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        m.SetTexture("_BaseMap", baseMap);
        m.SetTexture("_MainTex", baseMap);
        m.SetColor("_BaseColor", Color.white);
        m.SetTextureScale("_BaseMap", tiling);
        m.SetTextureScale("_MainTex", tiling);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);

        if (emissionMap != null)
        {
            m.SetTexture("_EmissionMap", emissionMap);
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.SetTexture("_EmissionMap", null);
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
    }

    // ------------------------------------------------------------------ Dibujo

    private static Color[] Fill(int w, int h, Color c)
    {
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        return px;
    }

    private static void Blend(Color[] px, int w, int h, int x, int y, Color c)
    {
        x = ((x % w) + w) % w;   // repite en horizontal: la textura encaja consigo misma
        if (y < 0 || y >= h) return;
        int i = y * w + x;
        Color o = px[i];
        px[i] = new Color(Mathf.Lerp(o.r, c.r, c.a), Mathf.Lerp(o.g, c.g, c.a), Mathf.Lerp(o.b, c.b, c.a), 1f);
    }

    private static void Line(Color[] px, int w, int h, float x0, float y0, float x1, float y1, float thick, Color c)
    {
        float len = Mathf.Max(1f, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
        int steps = Mathf.CeilToInt(len * 2f);
        int r = Mathf.CeilToInt(thick);
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            float cx = Mathf.Lerp(x0, x1, t), cy = Mathf.Lerp(y0, y1, t);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(thick * 0.5f + 0.5f - d) * c.a;
                    if (a > 0f) Blend(px, w, h, Mathf.RoundToInt(cx) + dx, Mathf.RoundToInt(cy) + dy, new Color(c.r, c.g, c.b, a));
                }
        }
    }

    private static void Disc(Color[] px, int w, int h, float cx, float cy, float rad, Color c)
    {
        int r = Mathf.CeilToInt(rad + 1);
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float a = Mathf.Clamp01(rad + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * c.a;
                if (a > 0f) Blend(px, w, h, Mathf.RoundToInt(cx) + dx, Mathf.RoundToInt(cy) + dy, new Color(c.r, c.g, c.b, a));
            }
    }

    private static Texture2D ToTexture(Color[] px, int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    /// <summary>Losa de plataforma (1 m x 0,5 m) con degradado, ruido, bisel y un detalle según el tipo.
    /// Con 'emissionMask' devuelve solo el detalle en blanco sobre negro (mapa de emisión).</summary>
    private static Texture2D Platform(Color top, Color bottom, int seed, Detail detail, bool emissionMask)
    {
        int w = PW, h = PH;
        Color[] px;
        if (emissionMask)
        {
            px = Fill(w, h, Color.black);
        }
        else
        {
            px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.07f + seed * 3.1f, y * 0.11f + seed * 1.7f);
                    float n2 = Mathf.PerlinNoise(x * 0.31f + seed, y * 0.31f - seed);
                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t)) * (0.9f + 0.14f * n + 0.05f * n2);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            // Bisel: borde superior claro, línea de sombra bajo él, borde inferior oscuro y juntas laterales
            for (int x = 0; x < w; x++)
            {
                for (int y = h - 5; y < h; y++) Blend(px, w, h, x, y, new Color(1f, 1f, 1f, 0.32f));
                Blend(px, w, h, x, h - 6, new Color(0f, 0f, 0f, 0.18f));
                for (int y = 0; y < 3; y++) Blend(px, w, h, x, y, new Color(0f, 0f, 0f, 0.35f));
            }
            for (int y = 0; y < h; y++)
            {
                Blend(px, w, h, 0, y, new Color(0f, 0f, 0f, 0.28f));
                Blend(px, w, h, w - 1, y, new Color(0f, 0f, 0f, 0.18f));
                Blend(px, w, h, 1, y, new Color(1f, 1f, 1f, 0.08f));
            }
        }

        System.Random rng = new System.Random(seed);
        switch (detail)
        {
            case Detail.Rivets:
                foreach (float rx in new[] { 18f, 110f })
                {
                    Disc(px, w, h, rx, 30f, 4.5f, new Color(0.08f, 0.12f, 0.25f, 0.55f));
                    Disc(px, w, h, rx - 0.6f, 30.6f, 3f, new Color(0.85f, 0.92f, 1f, 0.7f));
                }
                break;

            case Detail.Cracks:
                // Grietas quebradas: aviso de que se va a romper
                for (int k = 0; k < 3; k++)
                {
                    float x = 14f + k * 38f + (float)rng.NextDouble() * 14f;
                    float y = h - 8f;
                    for (int s = 0; s < 6 && y > 4f; s++)
                    {
                        float nx = x + ((float)rng.NextDouble() - 0.5f) * 18f;
                        float ny = y - 5f - (float)rng.NextDouble() * 8f;
                        Line(px, w, h, x, y, nx, ny, 2.2f, new Color(0.22f, 0.07f, 0.02f, 0.85f));
                        Line(px, w, h, x + 1f, y + 1f, nx + 1f, ny + 1f, 1f, new Color(1f, 0.85f, 0.6f, 0.25f));
                        if (rng.NextDouble() < 0.4)
                            Line(px, w, h, nx, ny, nx + ((float)rng.NextDouble() - 0.5f) * 14f, ny - 4f, 1.4f, new Color(0.22f, 0.07f, 0.02f, 0.7f));
                        x = nx; y = ny;
                    }
                }
                // Muescas en el borde inferior
                for (int k = 0; k < 4; k++) Disc(px, w, h, 10f + k * 31f, 1f, 3.5f, new Color(0.15f, 0.05f, 0.02f, 0.8f));
                break;

            case Detail.Dashes:
                // Banda central de energía punteada (brilla)
                for (int x = 0; x < w; x++)
                {
                    bool on = (x / 10) % 2 == 0;
                    if (!on) continue;
                    for (int y = 27; y <= 35; y++)
                    {
                        float edge = 1f - Mathf.Abs(y - 31f) / 4.5f;
                        Blend(px, w, h, x, y, new Color(1f, 0.92f, 1f, Mathf.Clamp01(edge) * (emissionMask ? 1f : 0.9f)));
                    }
                }
                break;

            case Detail.Chevrons:
                // Flechas ◄ ► : se mueve de lado a lado
                Color arrow = new Color(0.92f, 1f, 0.98f, emissionMask ? 1f : 0.95f);
                Line(px, w, h, 38f, 42f, 24f, 30f, 3.4f, arrow);
                Line(px, w, h, 24f, 30f, 38f, 18f, 3.4f, arrow);
                Line(px, w, h, 90f, 42f, 104f, 30f, 3.4f, arrow);
                Line(px, w, h, 104f, 30f, 90f, 18f, 3.4f, arrow);
                for (int x = 46; x <= 82; x += 9) Disc(px, w, h, x, 30f, 2f, new Color(arrow.r, arrow.g, arrow.b, arrow.a * 0.7f));
                break;

            case Detail.Grass:
                // Césped arriba (zona segura / suelo) y piedrecitas en la tierra
                for (int x = 0; x < w; x++)
                {
                    int blade = 12 + Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(x * 0.9f + seed)) * 4f + (float)rng.NextDouble() * 3f);
                    for (int y = h - blade; y < h; y++)
                    {
                        float t = (float)(y - (h - blade)) / blade;
                        Color g = Color.Lerp(new Color(0.24f, 0.55f, 0.22f), new Color(0.5f, 0.88f, 0.4f), t);
                        Blend(px, w, h, x, y, g);
                    }
                }
                for (int x = 0; x < w; x++) Blend(px, w, h, x, h - 1, new Color(0.85f, 1f, 0.7f, 0.5f));
                for (int k = 0; k < 9; k++)
                    Disc(px, w, h, (float)rng.NextDouble() * w, 6f + (float)rng.NextDouble() * 30f, 1.5f + (float)rng.NextDouble() * 2f,
                         new Color(0.75f, 0.66f, 0.55f, 0.55f));
                break;
        }

        return ToTexture(px, w, h);
    }

    /// <summary>Ladrillos de piedra oscura (1 m x 2 m por repetición), juntas incluidas y repetibles.</summary>
    private static Texture2D Bricks()
    {
        int w = 128, h = 256;
        int rowH = 32, brickW = 64, mortar = 3;
        Color[] px = Fill(w, h, new Color(0.09f, 0.1f, 0.15f));
        for (int y = 0; y < h; y++)
        {
            int row = y / rowH;
            int yIn = y % rowH;
            int offset = (row % 2) * (brickW / 2);
            for (int x = 0; x < w; x++)
            {
                int xs = (x + offset) % w;
                int col = xs / brickW;
                int xIn = xs % brickW;
                if (yIn < mortar || xIn < mortar) continue;   // junta

                float hash = Mathf.Abs(Mathf.Sin((row * 12.9898f + col * 78.233f) * 43758.5453f) % 1f);
                Color b = Color.Lerp(new Color(0.2f, 0.22f, 0.32f), new Color(0.3f, 0.32f, 0.44f), hash);
                float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                b *= 0.88f + 0.2f * n;
                if (yIn >= rowH - 3) b = Color.Lerp(b, Color.white, 0.12f);      // canto superior iluminado
                if (yIn < mortar + 2) b = Color.Lerp(b, Color.black, 0.25f);      // sombra inferior
                if (xIn < mortar + 2) b = Color.Lerp(b, Color.black, 0.12f);
                b.a = 1f;
                px[y * w + x] = b;
            }
        }
        return ToTexture(px, w, h);
    }
}
