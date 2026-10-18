using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fondo por altura: degradado de cielo que cambia por etapas (atardecer, noche, espacio...) con transición
/// suave, color de la cámara y de la luz, y capas de estrellas y nubes con parallax.
/// Todo se construye en código; solo necesita GameConfig y un material de efectos (URP Particles/Unlit).
/// </summary>
[DefaultExecutionOrder(100)]
public class BackgroundController : MonoBehaviour
{
    private class Layer
    {
        public Transform[] tf;
        public float[] a, b, size, phase;
        public Material material;
        public float z, parallax;
        public bool cloud;
    }

    private const float GradientZ = 60f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private GameConfig config;
    [SerializeField] private Material fxMaterial;

    private Camera cam;
    private CameraFollow camFollow;
    private Light sun;
    private Color sunBaseColor;
    private float sunBaseIntensity;

    private Transform root;
    private Mesh quad;
    private Mesh gradMesh;
    private Transform gradTf;
    private Material gradMat;
    private readonly Color[] gradColors = new Color[4];
    private readonly List<Layer> layers = new List<Layer>();

    /// <summary>Nombre de la etapa dominante (para depurar / HUD).</summary>
    public string CurrentStageName { get; private set; }

    private void Awake()
    {
        cam = Camera.main;
        if (config == null || fxMaterial == null || cam == null || config.backgroundStages.Count == 0)
        {
            enabled = false;
            return;
        }
        camFollow = cam.GetComponent<CameraFollow>();
        cam.clearFlags = CameraClearFlags.SolidColor;

        foreach (Light l in FindObjectsByType<Light>())
        {
            if (l.type != LightType.Directional) continue;
            sun = l;
            sunBaseColor = l.color;
            sunBaseIntensity = l.intensity;
            break;
        }

        root = new GameObject("Background").transform;
        quad = FxUtil.QuadMesh();

        // Degradado de cielo: quad hijo de la cámara, muy al fondo
        gradMesh = FxUtil.QuadMesh();
        gradMesh.MarkDynamic();
        gradMat = new Material(fxMaterial);
        gradMat.SetColor(BaseColorId, Color.white);
        GameObject g = new GameObject("SkyGradient");
        g.transform.SetParent(cam.transform, false);
        g.AddComponent<MeshFilter>().sharedMesh = gradMesh;
        MeshRenderer gr = g.AddComponent<MeshRenderer>();
        gr.sharedMaterial = gradMat;
        gr.sortingOrder = -50;
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gr.receiveShadows = false;
        gradTf = g.transform;

        Texture2D dot = FxUtil.SoftDot();
        Texture2D cloudTex = FxUtil.Cloud();
        System.Random rng = new System.Random(20260518);
        AddLayer("StarsFar", 70, 45f, 0.04f, 0.35f, 0.7f, false, dot, -40, rng);
        AddLayer("StarsNear", 35, 28f, 0.10f, 0.30f, 0.55f, false, dot, -35, rng);
        AddLayer("CloudsFar", 7, 22f, 0.20f, 7f, 11f, true, cloudTex, -30, rng);
        AddLayer("CloudsNear", 4, 10f, 0.35f, 6f, 9f, true, cloudTex, -25, rng);
    }

    private void AddLayer(string layerName, int count, float z, float parallax, float sizeMin, float sizeMax,
                          bool cloud, Texture2D tex, int sortingOrder, System.Random rng)
    {
        Layer l = new Layer
        {
            tf = new Transform[count],
            a = new float[count],
            b = new float[count],
            size = new float[count],
            phase = new float[count],
            z = z,
            parallax = parallax,
            cloud = cloud,
            material = new Material(fxMaterial)
        };
        l.material.SetTexture(BaseMapId, tex);

        Transform parent = new GameObject(layerName).transform;
        parent.SetParent(root, false);
        for (int i = 0; i < count; i++)
        {
            l.a[i] = (float)rng.NextDouble();
            l.b[i] = (float)rng.NextDouble();
            l.size[i] = Mathf.Lerp(sizeMin, sizeMax, (float)rng.NextDouble());
            l.phase[i] = (float)rng.NextDouble() * 6.28f;

            GameObject go = new GameObject(layerName + "_" + i);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = l.material;
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            l.tf[i] = go.transform;
        }
        layers.Add(l);
    }

    private void OnDisable()
    {
        if (sun != null)
        {
            sun.color = sunBaseColor;
            sun.intensity = sunBaseIntensity;
        }
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        float height = camFollow != null ? camFollow.CameraHeight : 0f;
        float camY = camFollow != null ? camFollow.BaseY : cam.transform.position.y;

        EvaluateStage(height, out BackgroundStage sa, out BackgroundStage sb, out float k);
        Color top = Color.Lerp(sa.topColor, sb.topColor, k);
        Color bottom = Color.Lerp(sa.bottomColor, sb.bottomColor, k);
        float stars = Mathf.Lerp(sa.stars, sb.stars, k);
        float clouds = Mathf.Lerp(sa.clouds, sb.clouds, k);
        Color cloudColor = Color.Lerp(sa.cloudColor, sb.cloudColor, k);
        CurrentStageName = k < 0.5f ? sa.name : sb.name;

        cam.backgroundColor = Color.Lerp(bottom, top, 0.5f);
        if (sun != null)
        {
            sun.color = Color.Lerp(sa.lightColor, sb.lightColor, k);
            sun.intensity = sunBaseIntensity * Mathf.Lerp(sa.lightIntensity, sb.lightIntensity, k);
        }

        float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float dist0 = Mathf.Abs(cam.transform.position.z);

        // Degradado
        gradColors[0] = bottom; gradColors[1] = bottom; gradColors[2] = top; gradColors[3] = top;
        gradMesh.SetColors(gradColors);
        float gd = dist0 + GradientZ;
        float gh = 2f * tanHalf * gd * 1.06f;
        gradTf.localPosition = new Vector3(0f, 0f, gd);
        gradTf.localScale = new Vector3(gh * cam.aspect, gh, 1f);

        // Estrellas y nubes con parallax
        float time = Time.time;
        for (int li = 0; li < layers.Count; li++)
        {
            Layer l = layers[li];
            Color c = l.cloud ? new Color(cloudColor.r, cloudColor.g, cloudColor.b, clouds * 0.85f) : new Color(1f, 1f, 1f, stars);
            l.material.SetColor(BaseColorId, c);

            float d = dist0 + l.z;
            float viewH = 2f * tanHalf * d;
            float viewW = viewH * cam.aspect;
            float range = viewH * 1.25f;
            for (int i = 0; i < l.tf.Length; i++)
            {
                float ly = Mathf.Repeat(l.a[i] * range - height * l.parallax, range) - range * 0.5f;
                float x = (l.b[i] - 0.5f) * viewW * 1.1f;
                l.tf[i].position = new Vector3(x, camY + ly, l.z);
                float s = l.size[i];
                if (l.cloud) l.tf[i].localScale = new Vector3(s, s * 0.45f, 1f);
                else
                {
                    s *= 1f + 0.25f * Mathf.Sin(time * 2f + l.phase[i]);
                    l.tf[i].localScale = new Vector3(s, s, 1f);
                }
            }
        }
    }

    /// <summary>Etapa actual y la siguiente, con el factor de mezcla (transición suave antes de cada etapa).</summary>
    private void EvaluateStage(float h, out BackgroundStage a, out BackgroundStage b, out float k)
    {
        List<BackgroundStage> s = config.backgroundStages;
        float t = Mathf.Max(1f, config.backgroundTransitionHeight);
        int idx = 0;
        for (int j = 1; j < s.Count; j++)
            if (h >= s[j].startHeight - t) idx = j;

        if (idx == 0)
        {
            a = s[0]; b = s[0]; k = 0f;
            return;
        }
        a = s[idx - 1];
        b = s[idx];
        k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.startHeight - t, b.startHeight, h));
    }
}
