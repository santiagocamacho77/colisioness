using UnityEngine;

/// <summary>
/// Cámara en perspectiva con X y Z fijos. Sigue al objetivo solo en Y con SmoothDamp y nunca baja.
/// Soporta camera shake (offset temporal que no altera la posición base).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private GameConfig config;
    [SerializeField] private Transform target;

    private Camera cam;
    private float baseY;
    private float distance;
    private float velocityY;
    private float lastAspect;
    private float startBaseY;

    /// <summary>Offset vertical efectivo: cameraOffsetY limitado para que el jugador quede siempre dentro de la vista (p. ej. en pantallas anchas).</summary>
    public float OffsetY { get { return Mathf.Min(config.cameraOffsetY, Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * 0.55f); } }

    /// <summary>Si es true, la cámara sube sola según 'scrollSpeedCurve' (el jugador la empuja si sube más rápido).</summary>
    public bool AutoScroll { get; set; }
    /// <summary>Velocidad de auto-scroll actual (m/s).</summary>
    public float ScrollSpeed { get; private set; }
    /// <summary>Distancia en Z de la cámara al plano de juego.</summary>
    public float Distance { get { return distance; } }
    /// <summary>Metros que ha subido la cámara desde que empezó la partida.</summary>
    public float CameraHeight { get { return baseY - startBaseY; } }
    /// <summary>Y del borde inferior visible en el plano del jugador (sin shake).</summary>
    public float BottomEdgeY { get { return baseY - Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance; } }

    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeTimer;
    private float shakeFrequency;

    /// <summary>Y base de la cámara, sin el offset del shake.</summary>
    public float BaseY => baseY;

    /// <summary>Distancia en Z necesaria para que las paredes completas entren en el ancho de pantalla.</summary>
    public static float ComputeFitDistance(GameConfig cfg, float aspect)
    {
        float halfWidth = cfg.wallX + cfg.wallThickness * 0.5f + cfg.cameraSideMargin;
        float tanHalfFov = Mathf.Tan(cfg.cameraFov * 0.5f * Mathf.Deg2Rad);
        return halfWidth / (tanHalfFov * aspect);
    }

    /// <summary>Sacude la cámara (suave, con ruido Perlin) y se atenúa hasta 0 durante 'duration'.</summary>
    public void Shake(float amplitude, float duration, float frequency)
    {
        float currentAmp = shakeDuration > 0f ? shakeAmplitude * (shakeTimer / shakeDuration) : 0f;
        if (amplitude < currentAmp) return;
        shakeAmplitude = amplitude;
        shakeDuration = Mathf.Max(0.0001f, duration);
        shakeTimer = shakeDuration;
        shakeFrequency = frequency;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        baseY = transform.position.y;
        ApplyLens();
    }

private void Start()
    {
        if (target != null) baseY = target.position.y + OffsetY;
        startBaseY = baseY;
        ApplyPosition(Vector2.zero);
    }

private void LateUpdate()
    {
        if (!Mathf.Approximately(cam.aspect, lastAspect)) ApplyLens();

        // Auto-scroll: velocidad según la altura de la cámara (crece con el tiempo)
        float autoY = baseY;
        ScrollSpeed = 0f;
        if (AutoScroll)
        {
            ScrollSpeed = Mathf.Min(config.scrollSpeedCurve.Evaluate(CameraHeight), config.scrollMaxSpeed);
            autoY = baseY + ScrollSpeed * Time.deltaTime;
        }

        // Seguimiento del jugador: si sube más rápido que el scroll, la cámara lo sigue
        float followY = baseY;
        if (target != null)
        {
            float desiredY = target.position.y + OffsetY;
            followY = Mathf.SmoothDamp(baseY, desiredY, ref velocityY, config.cameraSmoothTime);
        }

        baseY = Mathf.Max(baseY, Mathf.Max(autoY, followY));   // nunca baja
        ApplyPosition(EvaluateShake());
    }

    private Vector2 EvaluateShake()
    {
        if (shakeTimer <= 0f) return Vector2.zero;

        shakeTimer -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(shakeTimer / shakeDuration);
        float amp = shakeAmplitude * t * t;
        float time = Time.unscaledTime * shakeFrequency;
        return new Vector2(
            (Mathf.PerlinNoise(time, 0.37f) - 0.5f) * 2f * amp,
            (Mathf.PerlinNoise(0.91f, time) - 0.5f) * 2f * amp);
    }

    private void ApplyPosition(Vector2 shakeOffset)
    {
        // X centrada entre las paredes, Z fija
        transform.position = new Vector3(shakeOffset.x, baseY + shakeOffset.y, -distance);
        transform.rotation = Quaternion.identity;
    }

    private void ApplyLens()
    {
        lastAspect = cam.aspect;
        cam.orthographic = false;
        cam.fieldOfView = config.cameraFov;

        distance = config.fitWallsToScreenWidth
            ? ComputeFitDistance(config, cam.aspect)
            : config.cameraDistance;
    }


/// <summary>Recoloca la cámara para una partida nueva (sin auto-scroll hasta que se active de nuevo).</summary>
    public void ResetTo(float newBaseY)
    {
        baseY = newBaseY;
        startBaseY = newBaseY;
        velocityY = 0f;
        AutoScroll = false;
        ScrollSpeed = 0f;
        shakeTimer = 0f;
        ApplyPosition(Vector2.zero);
    }
}
