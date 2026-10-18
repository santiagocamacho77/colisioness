using UnityEngine;

public enum WeatherType { None, Rain, Wind, Fog }

/// <summary>
/// Eventos de clima temporales (lluvia, viento, niebla). Cada cierto número de metros hay una probabilidad
/// de evento; se avisa antes (weatherWarnTime) y dura 15–20 s con entrada/salida gradual. Los efectos sobre
/// el jugador se aplican a través de GameTuning (modifica GameConfig y lo restaura). Las partículas y la
/// niebla se construyen en código como hijos de la cámara.
/// </summary>
[DefaultExecutionOrder(100)]
public class WeatherEventManager : MonoBehaviour
{
    private enum Phase { Idle, Warning, Running }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private GameConfig config;
    [SerializeField] private Material fxMaterial;

    /// <summary>Evento anunciado que aún no ha empezado.</summary>
    public WeatherType Pending { get; private set; }
    /// <summary>Evento en curso (None si no hay).</summary>
    public WeatherType Active { get; private set; }
    public float WarnTimeLeft { get; private set; }
    public float TimeLeft { get { return phase == Phase.Running ? Mathf.Max(0f, duration - elapsed) : 0f; } }
    /// <summary>Fuerza actual del efecto, 0..1 (sube y baja gradualmente).</summary>
    public float Intensity { get; private set; }
    /// <summary>Dirección del viento: -1 hacia la izquierda, +1 hacia la derecha.</summary>
    public int WindDirection { get; private set; }
    public bool IsWarning { get { return phase == Phase.Warning; } }

    private Phase phase;
    private float duration, elapsed, nextRollHeight;
    private WeatherType lastType;

    private GameManager game;
    private GameTuning tuning;
    private Camera cam;

    private ParticleSystem rain, wind;
    private MeshRenderer fogRenderer;
    private Material fogMat;
    private Transform fogTf;

    private void Awake()
    {
        cam = Camera.main;
        if (config == null || fxMaterial == null || cam == null) { enabled = false; return; }

        Texture2D dot = FxUtil.SoftDot();
        rain = BuildSystem("Rain", config.rainColor, 0.05f, 0.09f, 6f, dot);
        wind = BuildSystem("Wind", config.windColor, 0.05f, 0.08f, 9f, dot);
        BuildFog();
        nextRollHeight = config.weatherFirstHeight;
        WindDirection = 1;
    }

    private void Start()
    {
        game = FindAnyObjectByType<GameManager>();
        tuning = FindAnyObjectByType<GameTuning>();
    }

    // ------------------------------------------------------------------ Construcción

    private ParticleSystem BuildSystem(string systemName, Color color, float sizeMin, float sizeMax, float lengthScale, Texture2D tex)
    {
        GameObject go = new GameObject(systemName);
        go.transform.SetParent(cam.transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.maxParticles = 3000;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(10f, 0.1f, 3f);

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        Material m = new Material(fxMaterial);
        m.SetTexture(BaseMapId, tex);
        m.SetColor(BaseColorId, Color.white);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = lengthScale;
        r.velocityScale = 0f;
        r.sortingOrder = 10;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private void BuildFog()
    {
        GameObject go = new GameObject("FogOverlay");
        go.transform.SetParent(cam.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = FxUtil.QuadMesh();
        fogRenderer = go.AddComponent<MeshRenderer>();
        fogMat = new Material(fxMaterial);
        fogMat.SetTexture(BaseMapId, FxUtil.VerticalFade());
        fogMat.SetColor(BaseColorId, new Color(1f, 1f, 1f, 0f));
        fogRenderer.sharedMaterial = fogMat;
        fogRenderer.sortingOrder = 50;
        fogRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fogRenderer.receiveShadows = false;
        fogRenderer.enabled = false;
        fogTf = go.transform;
    }

    // ------------------------------------------------------------------ Lógica

    private void Update()
    {
        if (game == null || tuning == null) return;
        if (game.State != GameManager.RunState.Playing) return;

        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Idle:
                if (game.Height >= nextRollHeight)
                {
                    nextRollHeight = game.Height + config.weatherRollInterval;
                    if (Random.value < config.weatherChance) StartWarning(PickType());
                }
                break;

            case Phase.Warning:
                WarnTimeLeft -= dt;
                if (WarnTimeLeft <= 0f) StartEvent();
                break;

            case Phase.Running:
                elapsed += dt;
                float fade = Mathf.Max(0.01f, config.weatherFadeTime);
                Intensity = Mathf.Clamp01(Mathf.Min(elapsed / fade, (duration - elapsed) / fade));
                if (elapsed >= duration) EndEvent();
                break;
        }

        ApplyEffects();
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float dist = Mathf.Abs(cam.transform.position.z);
        float halfH = tanHalf * dist;
        float halfW = halfH * cam.aspect;

        if (Active == WeatherType.Rain)
        {
            rain.transform.localPosition = new Vector3(0f, halfH + 1.5f, dist);
            ParticleSystem.ShapeModule sh = rain.shape;
            sh.scale = new Vector3(halfW * 2f + 4f, 0.1f, 3f);
            ParticleSystem.MainModule main = rain.main;
            main.startLifetime = (halfH * 2f + 3f) / 20f;
            ParticleSystem.VelocityOverLifetimeModule vel = rain.velocityOverLifetime;
            vel.y = new ParticleSystem.MinMaxCurve(-24f, -20f);
        }
        else if (Active == WeatherType.Wind)
        {
            wind.transform.localPosition = new Vector3(-WindDirection * (halfW + 2f), 0f, dist);
            ParticleSystem.ShapeModule sh = wind.shape;
            sh.scale = new Vector3(0.2f, halfH * 2f + 2f, 3f);
            ParticleSystem.MainModule main = wind.main;
            main.startLifetime = (halfW * 2f + 5f) / 28f;
            ParticleSystem.VelocityOverLifetimeModule vel = wind.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(WindDirection * 26f, WindDirection * 32f);
        }

        if (fogRenderer.enabled)
        {
            float fd = 1.5f;
            float fh = 2f * tanHalf * fd * 1.06f;
            fogTf.localPosition = new Vector3(0f, 0f, fd);
            fogTf.localScale = new Vector3(fh * cam.aspect, fh, 1f);
        }
    }

    private WeatherType PickType()
    {
        float wr = config.rainWeight, ww = config.windWeight, wf = config.fogWeight;
        if (lastType == WeatherType.Rain) wr = 0f;
        else if (lastType == WeatherType.Wind) ww = 0f;
        else if (lastType == WeatherType.Fog) wf = 0f;
        float sum = wr + ww + wf;
        if (sum <= 0f) { wr = config.rainWeight; ww = config.windWeight; wf = config.fogWeight; sum = wr + ww + wf; }
        if (sum <= 0f) return WeatherType.Rain;

        float r = Random.value * sum;
        if (r < wr) return WeatherType.Rain;
        if (r < wr + ww) return WeatherType.Wind;
        return WeatherType.Fog;
    }

    private void StartWarning(WeatherType type)
    {
        Pending = type;
        WarnTimeLeft = config.weatherWarnTime;
        if (type == WeatherType.Wind) WindDirection = Random.value < 0.5f ? -1 : 1;
        phase = Phase.Warning;
    }

    private void StartEvent()
    {
        Active = Pending;
        lastType = Active;
        Pending = WeatherType.None;
        duration = Random.Range(config.weatherDurationMin, config.weatherDurationMax);
        elapsed = 0f;
        Intensity = 0f;
        phase = Phase.Running;
    }

    private void EndEvent()
    {
        Active = WeatherType.None;
        Intensity = 0f;
        phase = Phase.Idle;
        nextRollHeight = game.Height + config.weatherRollInterval;
    }

    private void ApplyEffects()
    {
        float k = Intensity;

        GameTuning.WeatherMods mods = GameTuning.WeatherMods.None;
        if (Active == WeatherType.Rain)
        {
            GameTuning.WeatherMods rainMods = new GameTuning.WeatherMods
            {
                groundAccelMult = config.rainGroundAccelMult,
                groundDecelMult = config.rainGroundDecelMult,
                wallStickMult = config.rainWallStickMult,
                wallSlideSpeedMult = config.rainWallSlideSpeedMult,
                windSpeed = 0f
            };
            mods = GameTuning.WeatherMods.Blend(rainMods, k);
        }
        else if (Active == WeatherType.Wind)
        {
            mods.windSpeed = WindDirection * config.windStrength * k;
        }
        if (tuning != null) tuning.SetWeather(mods);

        SetEmission(rain, Active == WeatherType.Rain ? config.rainParticleRate * k : 0f);
        SetEmission(wind, Active == WeatherType.Wind ? config.windParticleRate * k : 0f);

        float fogAlpha = Active == WeatherType.Fog ? config.fogMaxAlpha * k : 0f;
        Color fc = config.fogColor;
        fc.a = fogAlpha;
        fogMat.SetColor(BaseColorId, fc);
        fogRenderer.enabled = fogAlpha > 0.003f;
    }

    private static void SetEmission(ParticleSystem ps, float rate)
    {
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        if (rate > 0.01f)
        {
            if (!ps.isPlaying) ps.Play();
        }
        else if (ps.isPlaying)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // ------------------------------------------------------------------ API

    /// <summary>Anuncia y lanza un evento ya (útil para probar). Si hay uno en curso se ignora.</summary>
    public void ForceEvent(WeatherType type)
    {
        if (phase != Phase.Idle || type == WeatherType.None) return;
        StartWarning(type);
    }

    /// <summary>Cancela cualquier evento y restaura todo (reinicio de partida).</summary>
    public void ResetWeather()
    {
        phase = Phase.Idle;
        Active = WeatherType.None;
        Pending = WeatherType.None;
        Intensity = 0f;
        elapsed = 0f;
        WarnTimeLeft = 0f;
        lastType = WeatherType.None;
        nextRollHeight = config.weatherFirstHeight;
        if (tuning != null) tuning.SetWeather(GameTuning.WeatherMods.None);
        SetEmission(rain, 0f);
        SetEmission(wind, 0f);
        rain.Clear();
        wind.Clear();
        fogRenderer.enabled = false;
    }

    public static string NameOf(WeatherType type, int windDirection)
    {
        switch (type)
        {
            case WeatherType.Rain: return "LLUVIA";
            case WeatherType.Wind: return windDirection > 0 ? "VIENTO >>>" : "<<< VIENTO";
            case WeatherType.Fog: return "NIEBLA";
            default: return string.Empty;
        }
    }
}
