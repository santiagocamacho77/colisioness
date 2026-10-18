using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Plataforma de una sola vía (el jugador la atraviesa desde abajo y se posa encima).
/// Tipos: Normal, Falling (tiembla en rojo y cae), Timed (aparece/desaparece en ciclos; barra de tiempo),
/// Moving (se desplaza horizontalmente sobre un riel visible y arrastra al jugador) y Safe (zona segura ancha).
/// La crea y recicla LevelGenerator con object pooling. Estructura esperada del prefab:
/// ClimbPlatform (Rigidbody kinematic + BoxCollider) > Visual > Solid, Ghost.
/// </summary>
[DisallowMultipleComponent]
public class ClimbPlatform : MonoBehaviour
{
    public enum Kind { Normal, Falling, Timed, Moving, Safe }

    private enum FallState { Idle, Shaking, Falling }
    private enum TimedPhase { Visible, WarnHide, Hidden, WarnShow }

    // Compartido por todas las plataformas (lo asigna LevelGenerator)
    private static PlayerController sPlayer;
    private static BoxCollider sPlayerCol;
    private static Rigidbody sPlayerBody;
    private static Material sIndicatorMat;
    private static Mesh sQuad;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly Color FallWarnColor = new Color(1f, 0.32f, 0.25f, 1f);

    public static void BindPlayer(PlayerController player)
    {
        sPlayer = player;
        sPlayerCol = player != null ? player.GetComponent<BoxCollider>() : null;
        sPlayerBody = player != null ? player.GetComponent<Rigidbody>() : null;
    }

    /// <summary>Material transparente (URP Particles/Unlit) para la barra de tiempo y el riel.</summary>
    public static void BindAssets(Material indicatorMaterial)
    {
        sIndicatorMat = indicatorMaterial;
    }

    private GameConfig cfg;
    private BoxCollider col;
    private Rigidbody body;
    private Transform visualRoot;
    private Renderer solid;
    private Renderer ghost;
    private Material defaultSolidMat;
    private MaterialPropertyBlock solidBlock;
    private MaterialPropertyBlock ghostBlock;
    private Renderer timerBar;
    private Renderer track;
    private MaterialPropertyBlock barBlock;
    private MaterialPropertyBlock trackBlock;
    private bool cached;

    private float width;
    private float thickness;
    private float depth;
    private Color baseColor;
    private Color tint = Color.white;
    private bool collisionOn = true;

    // Moving
    private float baseX, amplitude, omega, moveTime, phase;
    // Falling
    private FallState fall;
    private float fallTimer, fallVelocity, fallenDistance;
    // Timed
    private TimedPhase timedPhase;
    private float cycleTime;
    private float lastGhostAlpha = -1f;
    private float popTimer;
    private float fallSpin = 1f;
    private const float PopTime = 0.32f;
    private const float FallShrinkTime = 0.55f;

    public Kind PlatformKind { get; private set; }
    /// <summary>Y de la superficie superior.</summary>
    public float TopY { get { return transform.position.y + thickness * 0.5f; } }

    private float TimedCycle { get { return cfg.timedVisibleTime + cfg.timedHiddenTime + cfg.timedWarnTime * 2f; } }

    /// <summary>¿Esta plataforma sirve de suelo para un jugador con los pies en feetY?</summary>
    public bool SupportsFrom(float feetY)
    {
        return col != null && col.enabled && feetY >= TopY - cfg.oneWayTolerance;
    }

    private void Cache()
    {
        if (cached) return;
        col = GetComponent<BoxCollider>();
        body = GetComponent<Rigidbody>();
        visualRoot = transform.Find("Visual");
        solid = visualRoot.Find("Solid").GetComponent<Renderer>();
        ghost = visualRoot.Find("Ghost").GetComponent<Renderer>();
        defaultSolidMat = solid.sharedMaterial;
        solidBlock = new MaterialPropertyBlock();
        ghostBlock = new MaterialPropertyBlock();
        ghost.shadowCastingMode = ShadowCastingMode.Off;
        ghost.receiveShadows = false;
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        if (sIndicatorMat != null)
        {
            if (sQuad == null) sQuad = FxUtil.QuadMesh();
            barBlock = new MaterialPropertyBlock();
            trackBlock = new MaterialPropertyBlock();
            timerBar = MakeIndicator("TimerBar");
            track = MakeIndicator("Track");
        }
        cached = true;
    }

    private Renderer MakeIndicator(string indicatorName)
    {
        GameObject go = new GameObject(indicatorName);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = sQuad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sIndicatorMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = false;
        return mr;
    }

    /// <summary>Configura y activa la plataforma. Se llama con el objeto inactivo (viene del pool).</summary>
    public void Spawn(GameConfig config, Kind kind, float x, float topY, float platformWidth, float platformThickness,
                      float platformDepth, float moveAmplitude, float moveOmega, float random01, Color color, Material material)
    {
        Cache();
        cfg = config;
        gameObject.SetActive(false);

        PlatformKind = kind;
        width = platformWidth;
        thickness = platformThickness;
        depth = platformDepth;
        baseColor = color;

        baseX = x;
        amplitude = kind == Kind.Moving ? moveAmplitude : 0f;
        omega = moveOmega;
        moveTime = 0f;
        phase = random01 * Mathf.PI * 2f;

        fall = FallState.Idle;
        fallTimer = 0f;
        fallVelocity = 0f;
        fallenDistance = 0f;

        timedPhase = TimedPhase.Visible;
        cycleTime = random01 * TimedCycle;
        lastGhostAlpha = -1f;

        float startX = x + (kind == Kind.Moving ? Mathf.Sin(phase) * amplitude : 0f);
        transform.SetPositionAndRotation(new Vector3(startX, topY - thickness * 0.5f, 0f), Quaternion.identity);

        col.size = new Vector3(width, thickness, depth);
        col.center = Vector3.zero;
        col.enabled = true;

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localScale = new Vector3(width, thickness, depth);
        visualRoot.localRotation = Quaternion.identity;
        popTimer = 0f;
        fallSpin = random01 < 0.5f ? -1f : 1f;
        solid.enabled = true;
        ghost.enabled = false;

        // Material con textura por tipo (la textura se repite una vez por metro de ancho)
        bool textured = material != null;
        solid.sharedMaterial = textured ? material : defaultSolidMat;
        tint = textured ? Color.white : color;
        solidBlock.Clear();
        // La cara frontal del cubo de Unity tiene las UV giradas 180°: el tiling negativo la endereza
        if (textured) solidBlock.SetVector(BaseMapStId, new Vector4(-Mathf.Max(1f, Mathf.Round(width)), -1f, 0f, 0f));
        SetSolidTint(tint);

        if (timerBar != null) timerBar.enabled = kind == Kind.Timed;
        if (track != null)
        {
            track.enabled = kind == Kind.Moving;
            if (kind == Kind.Moving)
            {
                Color tc = color;
                tc.a = 0.55f;
                trackBlock.SetColor(BaseColorId, tc);
                track.SetPropertyBlock(trackBlock);
                track.transform.localScale = new Vector3(amplitude * 2f + width, 0.16f, 1f);
            }
        }

        if (kind == Kind.Timed) EvaluateTimed(0f);

        gameObject.SetActive(true);
        ResetOneWay();
        UpdateIndicators();
    }

    public void Despawn()
    {
        gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ Física

    private void FixedUpdate()
    {
        if (cfg == null) return;
        float dt = Time.fixedDeltaTime;

        switch (PlatformKind)
        {
            case Kind.Moving: TickMoving(dt); break;
            case Kind.Falling: TickFalling(dt); break;
            case Kind.Timed: EvaluateTimed(dt); break;
        }

        UpdateOneWay();
    }

    private void TickMoving(float dt)
    {
        bool riding = PlayerStandingOn();
        moveTime += dt;
        float nx = baseX + Mathf.Sin(moveTime * omega + phase) * amplitude;
        float dx = nx - body.position.x;
        body.MovePosition(new Vector3(nx, body.position.y, 0f));
        // Arrastra al jugador que está encima (el rozamiento del jugador es 0)
        if (riding && sPlayerBody != null)
            sPlayerBody.position += new Vector3(dx, 0f, 0f);
    }

    private void TickFalling(float dt)
    {
        switch (fall)
        {
            case FallState.Idle:
                if (PlayerStandingOn())
                {
                    fall = FallState.Shaking;
                    fallTimer = cfg.fallingShakeTime;
                }
                break;

            case FallState.Shaking:
                fallTimer -= dt;
                if (fallTimer <= 0f)
                {
                    // Se rompe: suelta trozos y la losa cae girando mientras se encoge
                    fall = FallState.Falling;
                    col.enabled = false;
                    fallVelocity = 0f;
                    fallTimer = 0f;
                    visualRoot.localPosition = Vector3.zero;
                    Vector3 center = new Vector3(body.position.x, body.position.y, -depth * 0.5f - 0.1f);
                    DebrisFx.Burst(center, new Vector3(width * 0.45f, thickness * 0.4f, 0.15f),
                        Mathf.Clamp(Mathf.RoundToInt(width * 3.5f), 6, 14), baseColor, new Vector3(0f, 1.5f, 0f), 3.5f, 0.9f, 0.24f);
                }
                break;

            case FallState.Falling:
                fallTimer += dt;
                fallVelocity += cfg.fallingGravity * dt;
                float step = fallVelocity * dt;
                fallenDistance += step;
                Vector3 p = body.position;
                p.y -= step;
                body.position = p;
                if (fallTimer >= FallShrinkTime) Despawn();
                break;
        }
    }

    private void EvaluateTimed(float dt)
    {
        float cycle = TimedCycle;
        cycleTime = Mathf.Repeat(cycleTime + dt, cycle);

        float visible = cfg.timedVisibleTime;
        float warn = cfg.timedWarnTime;
        float hidden = cfg.timedHiddenTime;

        if (cycleTime < visible) timedPhase = TimedPhase.Visible;
        else if (cycleTime < visible + warn) timedPhase = TimedPhase.WarnHide;
        else if (cycleTime < visible + warn + hidden) timedPhase = TimedPhase.Hidden;
        else timedPhase = TimedPhase.WarnShow;

        bool solidNow = timedPhase == TimedPhase.Visible || timedPhase == TimedPhase.WarnHide;
        if (col.enabled != solidNow)
        {
            col.enabled = solidNow;
            if (gameObject.activeInHierarchy)
            {
                if (solidNow)
                {
                    ResetOneWay();
                    popTimer = PopTime;   // aparece con un rebote
                }
                else
                {
                    // Se deshace en chispas en vez de apagarse en seco
                    Color sc = baseColor;
                    sc.a = 0.9f;
                    DebrisFx.Burst(new Vector3(body.position.x, body.position.y, -depth * 0.5f - 0.1f),
                        new Vector3(width * 0.45f, thickness * 0.4f, 0.1f), Mathf.Clamp(Mathf.RoundToInt(width * 2.5f), 4, 10),
                        sc, new Vector3(0f, 1.2f, 0f), 2.2f, 0.55f, 0.15f);
                }
            }
        }
    }

    /// <summary>Plataforma de una sola vía: solo colisiona si los pies del jugador están sobre su superficie.</summary>
    private void UpdateOneWay()
    {
        if (sPlayerCol == null || !col.enabled) return;
        bool collide = sPlayerCol.bounds.min.y >= TopY - cfg.oneWayTolerance;
        if (collide != collisionOn)
        {
            Physics.IgnoreCollision(sPlayerCol, col, !collide);
            collisionOn = collide;
        }
    }

    // Al (re)activar el collider se limpia cualquier estado de ignorado previo
    private void ResetOneWay()
    {
        collisionOn = true;
        if (sPlayerCol != null && col.enabled && sPlayerCol.enabled)
            Physics.IgnoreCollision(sPlayerCol, col, false);
    }

    private bool PlayerStandingOn()
    {
        if (sPlayer == null || sPlayerCol == null || !sPlayer.IsGrounded) return false;
        Bounds pb = sPlayerCol.bounds;
        if (Mathf.Abs(pb.min.y - TopY) > 0.25f) return false;
        float cx = body.position.x;
        float half = width * 0.5f;
        return pb.max.x > cx - half && pb.min.x < cx + half;
    }

    // ----------------------------------------------------------- Visuales

    private void Update()
    {
        if (cfg == null) return;
        if (PlatformKind == Kind.Falling) AnimateFalling();
        else if (PlatformKind == Kind.Timed) AnimateTimed();
        UpdateIndicators();
    }

    private void AnimateFalling()
    {
        if (fall == FallState.Shaking)
        {
            // Tiembla y parpadea en rojo: aviso claro de que se va a caer
            Vector3 jitter = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * cfg.fallingShakeAmplitude;
            visualRoot.localPosition = jitter;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 45f);
            SetSolidTint(Color.Lerp(tint, FallWarnColor, 0.35f + 0.5f * pulse));
        }
        else if (fall == FallState.Falling)
        {
            // Cae girando, se encoge y se oscurece hasta desaparecer (sin cortes bruscos)
            float k = Mathf.Clamp01(fallTimer / FallShrinkTime);
            float s = 1f - k * k;
            visualRoot.localScale = new Vector3(width * Mathf.Lerp(0.3f, 1f, s), thickness * s, depth * Mathf.Lerp(0.5f, 1f, s));
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, fallSpin * 35f * k);
            SetSolidTint(Color.Lerp(tint, new Color(0.35f, 0.35f, 0.4f, 1f), k));
        }
    }

    private void AnimateTimed()
    {
        bool blinkOn = Mathf.Repeat(Time.time * cfg.timedBlinkHz, 1f) < 0.5f;
        switch (timedPhase)
        {
            case TimedPhase.Visible:
                solid.enabled = true;
                ghost.enabled = false;
                break;
            case TimedPhase.WarnHide:   // avisa que va a desaparecer
                solid.enabled = blinkOn;
                ghost.enabled = !blinkOn;
                SetGhostAlpha(0.35f);
                break;
            case TimedPhase.Hidden:     // solo se ve la silueta
                solid.enabled = false;
                ghost.enabled = true;
                SetGhostAlpha(0.18f);
                break;
            case TimedPhase.WarnShow:   // avisa que va a aparecer
                solid.enabled = false;
                ghost.enabled = true;
                SetGhostAlpha(blinkOn ? 0.6f : 0.18f);
                break;
        }

        // Rebote al reaparecer (escala con sobrepaso)
        float m = 1f;
        if (popTimer > 0f)
        {
            popTimer -= Time.deltaTime;
            float x = Mathf.Clamp01(1f - popTimer / PopTime);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float back = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
            m = Mathf.LerpUnclamped(0.55f, 1f, back);
        }
        visualRoot.localScale = new Vector3(width * m, thickness * m, depth);
    }

    private void UpdateIndicators()
    {
        // Barra de tiempo (temporizada): se vacía mientras es sólida y se rellena mientras está oculta
        if (timerBar != null && timerBar.enabled)
        {
            float solidSpan = cfg.timedVisibleTime + cfg.timedWarnTime;
            float hiddenSpan = cfg.timedHiddenTime + cfg.timedWarnTime;
            float frac;
            Color c;
            if (cycleTime < solidSpan)
            {
                frac = 1f - cycleTime / solidSpan;
                c = Color.Lerp(new Color(1f, 0.3f, 0.25f, 0.95f), new Color(1f, 0.95f, 0.55f, 0.95f), Mathf.InverseLerp(0.15f, 0.6f, frac));
            }
            else
            {
                frac = Mathf.Clamp01((cycleTime - solidSpan) / hiddenSpan);
                c = new Color(0.85f, 0.75f, 1f, 0.5f);
            }
            float w = Mathf.Max(0.001f, width * frac);
            Transform bt = timerBar.transform;
            bt.localPosition = new Vector3(-width * 0.5f + w * 0.5f, -thickness * 0.5f - 0.13f, -depth * 0.5f);
            bt.localScale = new Vector3(w, 0.1f, 1f);
            barBlock.SetColor(BaseColorId, c);
            timerBar.SetPropertyBlock(barBlock);
        }

        // Riel (móvil): fijo en el mundo, marca el recorrido completo
        if (track != null && track.enabled)
            track.transform.position = new Vector3(baseX, transform.position.y, depth * 0.5f + 0.05f);
    }

    private void SetSolidTint(Color c)
    {
        solidBlock.SetColor(BaseColorId, c);
        solid.SetPropertyBlock(solidBlock);
    }

    private void SetGhostAlpha(float alpha)
    {
        if (Mathf.Approximately(alpha, lastGhostAlpha)) return;
        lastGhostAlpha = alpha;
        Color c = baseColor;
        c.a = alpha;
        ghostBlock.SetColor(BaseColorId, c);
        ghost.SetPropertyBlock(ghostBlock);
    }
}
