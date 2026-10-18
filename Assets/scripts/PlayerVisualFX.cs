using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animaciones y efectos procedurales del jugador. Solo transforma el hijo "Visual" (nunca el objeto con física).
/// Se conecta a los eventos de PlayerController: no toca la física.
/// - Squash & stretch con muelle, estiramiento en el aire según la velocidad y rebote al correr.
/// - Pared: aplaste e inclinación hacia la pared, polvo al deslizar y temblor de aviso antes de soltarse.
/// - Dash: estela, imágenes fantasma, freeze-frame y camera shake. El cuerpo se oscurece mientras el dash
///   no está disponible y destella al recargarse.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(PlayerController), typeof(Rigidbody))]
public class PlayerVisualFX : MonoBehaviour
{
    // Los efectos se dibujan ligeramente delante del cubo (la cámara mira hacia +Z desde -Z).
    private const float FxZ = -0.6f;
    private const int GhostPoolSize = 14;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // La cara del modelo está en +Z; se gira 180° para que mire a la cámara.
    private static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

    private class Ghost
    {
        public Transform tf;
        public MeshRenderer renderer;
        public Vector3 baseScale;
        public float age;
        public bool alive;
    }

    [SerializeField] private GameConfig config;
    [SerializeField] private Transform visual;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem wallDust;
    [SerializeField] private TrailRenderer dashTrail;

    private PlayerController controller;
    private Rigidbody rb;
    private CameraFollow cameraFollow;

    // Muelle de escala
    private Vector3 scale = Vector3.one;
    private Vector3 scaleVelocity;
    private float anchorX;
    private int wallSide;

    private float tilt;
    private float peakY;
    private float wallDustAccumulator;
    private float bobPhase;
    private float jitterTime;
    private PlayerController.MoveState lastState;

    // Fantasmas del dash
    private readonly List<Ghost> ghosts = new List<Ghost>(GhostPoolSize);
    private Transform ghostRoot;
    private MaterialPropertyBlock ghostBlock;
    private float ghostTimer;

    // Color del cuerpo (indicador de dash)
    private Renderer body;
    private MaterialPropertyBlock bodyBlock;
    private Color bodyBaseColor = Color.white;
    private bool lastDashAvailable = true;
    private float tintK = 1f;
    private float flashTimer;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();
        cameraFollow = FindAnyObjectByType<CameraFollow>();

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.time = config.dashTrailTime;
            dashTrail.widthMultiplier = config.dashTrailWidth;
            dashTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            Color end = config.dashTrailColor;
            end.a = 0f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(config.dashTrailColor, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(config.dashTrailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            dashTrail.colorGradient = gradient;
        }

        Transform bodyTf = visual != null ? visual.Find("Body") : null;
        if (bodyTf != null)
        {
            body = bodyTf.GetComponent<Renderer>();
            bodyBlock = new MaterialPropertyBlock();
            if (body.sharedMaterial != null && body.sharedMaterial.HasProperty(BaseColorId))
                bodyBaseColor = body.sharedMaterial.GetColor(BaseColorId);
        }
        BuildGhosts(bodyTf);
    }

    private void BuildGhosts(Transform bodyTf)
    {
        if (bodyTf == null || dust == null) return;
        MeshFilter mf = bodyTf.GetComponent<MeshFilter>();
        ParticleSystemRenderer pr = dust.GetComponent<ParticleSystemRenderer>();
        if (mf == null || pr == null || pr.sharedMaterial == null) return;

        // Copia del cubo con colores de vértice blancos (el shader de partículas los multiplica)
        Mesh mesh = Instantiate(mf.sharedMesh);
        Color[] colors = new Color[mesh.vertexCount];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        mesh.colors = colors;

        Material mat = new Material(pr.sharedMaterial);
        ghostBlock = new MaterialPropertyBlock();
        ghostRoot = new GameObject("PlayerDashGhosts").transform;
        for (int i = 0; i < GhostPoolSize; i++)
        {
            GameObject go = new GameObject("Ghost");
            go.transform.SetParent(ghostRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            ghosts.Add(new Ghost { tf = go.transform, renderer = mr });
        }
    }

    private void OnEnable()
    {
        controller.OnJump += HandleJump;
        controller.OnLand += HandleLand;
        controller.OnWallStick += HandleWallStick;
        controller.OnWallJump += HandleWallJump;
        controller.OnDash += HandleDash;
        peakY = rb.position.y;
    }

    private void OnDisable()
    {
        controller.OnJump -= HandleJump;
        controller.OnLand -= HandleLand;
        controller.OnWallStick -= HandleWallStick;
        controller.OnWallJump -= HandleWallJump;
        controller.OnDash -= HandleDash;
        if (dashTrail != null) dashTrail.emitting = false;
    }

    private void OnDestroy()
    {
        if (ghostRoot != null) Destroy(ghostRoot.gameObject);
    }

    /// <summary>Deja el aspecto del jugador como al empezar (reinicio de partida).</summary>
    public void ResetVisuals()
    {
        scale = Vector3.one;
        scaleVelocity = Vector3.zero;
        anchorX = 0f;
        tilt = 0f;
        bobPhase = 0f;
        wallDustAccumulator = 0f;
        peakY = rb.position.y;
        tintK = 1f;
        flashTimer = 0f;
        lastDashAvailable = true;
        lastState = PlayerController.MoveState.Normal;
        if (visual != null)
        {
            visual.gameObject.SetActive(true);
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = FaceCamera;
        }
        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.Clear();
        }
        if (dust != null) dust.Clear();
        if (wallDust != null) wallDust.Clear();
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].alive = false;
            ghosts[i].tf.gameObject.SetActive(false);
        }
        ApplyBodyColor(bodyBaseColor);
    }

    // ------------------------------------------------------------ Animación

    /// <summary>
    /// Muerte: el personaje estalla en trozos de sus colores que salen disparados hacia arriba desde 'at'
    /// (normalmente el borde inferior de la pantalla) y desaparece.
    /// </summary>
    public void PlayDeath(Vector3 at)
    {
        at.z = FxZ;
        if (visual != null) visual.gameObject.SetActive(false);
        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.Clear();
        }
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].alive = false;
            ghosts[i].tf.gameObject.SetActive(false);
        }
        Vector3 box = new Vector3(0.45f, 0.45f, 0.2f);
        DebrisFx.Burst(at, box, 16, bodyBaseColor, new Vector3(0f, 11f, 0f), 6f, 1.4f, 0.3f);
        DebrisFx.Burst(at, box, 7, Color.white, new Vector3(0f, 9f, 0f), 5f, 1.2f, 0.2f);
        DebrisFx.Burst(at, box, 5, new Color(0.08f, 0.08f, 0.1f, 1f), new Vector3(0f, 8f, 0f), 4f, 1.1f, 0.18f);
        if (dust != null)
        {
            for (int i = 0; i < 16; i++)
            {
                float ang = i / 16f * Mathf.PI * 2f;
                Vector3 vel = new Vector3(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) + 0.3f, 0f) * config.dustSpeed * 2.2f;
                Emit(dust, at, vel, config.dustSize * 1.6f);
            }
        }
    }

    private void LateUpdate()
    {
        TimeController.Tick();
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        if (dt <= 0f) return;   // freeze-frame o pausa

        // Altura máxima alcanzada en el aire (para medir la caída al aterrizar)
        if (controller.IsGrounded) peakY = rb.position.y;
        else peakY = Mathf.Max(peakY, rb.position.y);

        PlayerController.MoveState state = controller.State;
        bool onWall = state == PlayerController.MoveState.WallStick || state == PlayerController.MoveState.WallSlide;
        if (onWall) wallSide = controller.WallDirection != 0 ? controller.WallDirection : wallSide;

        // Se soltó de la pared sin saltar: pequeño soplo de polvo
        if (lastState == PlayerController.MoveState.WallSlide && state == PlayerController.MoveState.Normal && !controller.IsGrounded)
            EmitWallDust(3, wallSide, 0.6f, 0.9f);
        lastState = state;

        AnimateScale(dt, onWall);
        AnimateTilt(dt, onWall);
        UpdateWallDust(dt);
        UpdateGhosts(dt);
        UpdateBodyColor(dt);

        if (dashTrail != null) dashTrail.emitting = state == PlayerController.MoveState.Dash;
    }

    private void AnimateScale(float dt, bool onWall)
    {
        PlayerController.MoveState state = controller.State;
        Vector3 target;
        if (onWall)
        {
            target = new Vector3(1f - config.wallSquashAmount, 1f + config.wallSquashAmount * 0.5f, 1f);
        }
        else if (!controller.IsGrounded && state != PlayerController.MoveState.Dash)
        {
            // En el aire se estira un poco según la velocidad vertical (se ve más rápido y fluido)
            float k = Mathf.Clamp01(Mathf.Abs(controller.Velocity.y) / 20f) * config.airStretchAmount;
            target = VolumeY(1f + k);
        }
        else
        {
            target = Vector3.one;
        }

        // Muelle amortiguado: rebota alrededor del objetivo hasta volver a escala 1
        Vector3 accel = -config.squashSpringStiffness * (scale - target) - config.squashSpringDamping * scaleVelocity;
        scaleVelocity += accel * dt;
        scale += scaleVelocity * dt;
        scale = new Vector3(Mathf.Clamp(scale.x, 0.2f, 2.5f), Mathf.Clamp(scale.y, 0.2f, 2.5f), Mathf.Clamp(scale.z, 0.2f, 2.5f));

        // Ancla: los pies siempre tocan el suelo; contra la pared, el lado de la pared no se separa de ella
        anchorX = Mathf.Lerp(anchorX, onWall ? wallSide : 0f, 1f - Mathf.Exp(-20f * dt));
        Vector3 pos = new Vector3(anchorX * 0.5f * (1f - scale.x), 0.5f * (scale.y - 1f), 0f);

        // Rebote al correr
        float run = controller.IsGrounded && state == PlayerController.MoveState.Normal
            ? Mathf.Clamp01(Mathf.Abs(controller.Velocity.x) / Mathf.Max(0.01f, config.maxMoveSpeed))
            : 0f;
        if (run > 0.15f)
        {
            bobPhase += dt * config.runBobFrequency;
            pos.y += Mathf.Abs(Mathf.Sin(bobPhase)) * config.runBobAmount * run;
        }
        else
        {
            bobPhase = 0f;
        }

        // Aviso: tiembla cuando está a punto de soltarse de la pared
        float warnStart = 1f - config.wallReleaseWarnFraction;
        if (state == PlayerController.MoveState.WallSlide && controller.WallProgress01 > warnStart)
        {
            jitterTime += dt;
            float a = config.wallReleaseJitter * Mathf.InverseLerp(warnStart, 1f, controller.WallProgress01);
            pos.x += Mathf.Sin(jitterTime * 95f) * a;
            pos.y += Mathf.Sin(jitterTime * 71f) * a * 0.5f;
        }

        visual.localScale = scale;
        visual.localPosition = pos;
    }

    private void AnimateTilt(float dt, bool onWall)
    {
        float target;
        if (onWall)
        {
            // Se inclina hacia la pared (la parte de arriba se apoya en ella)
            target = -wallSide * config.wallLeanAngle;
        }
        else
        {
            float vx = Mathf.Clamp(controller.Velocity.x / Mathf.Max(0.01f, config.maxMoveSpeed), -1f, 1f);
            target = -vx * config.tiltMaxAngle;   // hacia la derecha = giro horario
        }
        tilt = Mathf.Lerp(tilt, target, 1f - Mathf.Exp(-config.tiltSmoothing * dt));
        visual.localRotation = Quaternion.Euler(0f, 0f, tilt) * FaceCamera;
    }

    private void SetScaleImpulse(Vector3 newScale)
    {
        scale = newScale;
        scaleVelocity = Vector3.zero;
    }

    /// <summary>Escala con volumen aproximadamente constante a partir de la escala en Y.</summary>
    private static Vector3 VolumeY(float y)
    {
        float xz = 1f / Mathf.Sqrt(Mathf.Max(0.01f, y));
        return new Vector3(xz, y, xz);
    }

    // ---------------------------------------------------------- Color del cuerpo

    private void UpdateBodyColor(float dt)
    {
        if (body == null) return;
        bool available = controller.DashAvailable;
        if (available && !lastDashAvailable)
        {
            flashTimer = config.dashRechargeFlashTime;
            EmitGroundDust(4, 0.6f, 0.7f);
        }
        lastDashAvailable = available;

        tintK = Mathf.Lerp(tintK, available ? 1f : config.dashUsedTint, 1f - Mathf.Exp(-25f * dt));
        flashTimer = Mathf.Max(0f, flashTimer - dt);
        float flash = config.dashRechargeFlashTime > 0f ? flashTimer / config.dashRechargeFlashTime : 0f;

        Color c = bodyBaseColor * tintK;
        c.a = bodyBaseColor.a;
        c = Color.Lerp(c, Color.white, flash * 0.75f);
        ApplyBodyColor(c);
    }

    private void ApplyBodyColor(Color c)
    {
        if (body == null) return;
        bodyBlock.SetColor(BaseColorId, c);
        body.SetPropertyBlock(bodyBlock);
    }

    // -------------------------------------------------------------- Fantasmas

    private void UpdateGhosts(float dt)
    {
        if (ghosts.Count == 0) return;

        if (controller.State == PlayerController.MoveState.Dash)
        {
            ghostTimer -= dt;
            if (ghostTimer <= 0f)
            {
                SpawnGhost();
                ghostTimer = config.ghostInterval;
            }
        }
        else
        {
            ghostTimer = 0f;
        }

        float life = Mathf.Max(0.01f, config.ghostLifetime);
        for (int i = 0; i < ghosts.Count; i++)
        {
            Ghost g = ghosts[i];
            if (!g.alive) continue;
            g.age += dt;
            float k = g.age / life;
            if (k >= 1f)
            {
                g.alive = false;
                g.tf.gameObject.SetActive(false);
                continue;
            }
            Color c = config.ghostColor;
            c.a *= (1f - k) * (1f - k);
            ghostBlock.SetColor(BaseColorId, c);
            g.renderer.SetPropertyBlock(ghostBlock);
            g.tf.localScale = g.baseScale * (1f - 0.3f * k);
        }
    }

    private void SpawnGhost()
    {
        Ghost g = null;
        float oldest = -1f;
        for (int i = 0; i < ghosts.Count; i++)
        {
            if (!ghosts[i].alive) { g = ghosts[i]; break; }
            if (ghosts[i].age > oldest) { oldest = ghosts[i].age; g = ghosts[i]; }
        }
        if (g == null) return;

        g.alive = true;
        g.age = 0f;
        g.baseScale = visual.lossyScale;
        g.tf.SetPositionAndRotation(visual.position + new Vector3(0f, 0f, 0.15f), visual.rotation);
        g.tf.localScale = g.baseScale;
        g.tf.gameObject.SetActive(true);
    }

    // -------------------------------------------------------------- Eventos

    private void HandleJump()
    {
        SetScaleImpulse(VolumeY(config.jumpStretchY));
        EmitGroundDust(config.jumpDustCount, 1f, 1f);
    }

    private void HandleLand()
    {
        float drop = peakY - rb.position.y;
        if (drop < config.landIgnoreFallDistance) return;   // incluye el primer aterrizaje al empezar

        float t = Mathf.InverseLerp(config.landFallDistanceMin, config.landFallDistanceMax, drop);
        SetScaleImpulse(VolumeY(Mathf.Lerp(config.landSquashYMin, config.landSquashYMax, t)));

        int count = Mathf.RoundToInt(Mathf.Lerp(config.landDustMinCount, config.landDustMaxCount, t));
        EmitGroundDust(count, Mathf.Lerp(1f, 1.6f, t), Mathf.Lerp(1f, 1.6f, t));
        if (t > 0.6f && cameraFollow != null) cameraFollow.Shake(0.06f * t, 0.15f, 30f);
    }

    private void HandleWallStick(int wallDir)
    {
        wallSide = wallDir;
        jitterTime = 0f;
        SetScaleImpulse(new Vector3(config.wallImpactSquashX, 1f + (1f - config.wallImpactSquashX) * 0.5f, 1f));
        EmitWallDust(Mathf.Max(3, config.jumpDustCount / 2 + 1), wallDir, 1f, 1f);
    }

    private void HandleWallJump(int wallDir)
    {
        SetScaleImpulse(VolumeY(config.jumpStretchY));
        tilt = wallDir * config.wallLeanAngle;   // gira un poco al despegar de la pared
        EmitWallDust(config.jumpDustCount + 2, wallDir, 1.2f, 1.2f);
    }

    private void HandleDash(Vector2 dir)
    {
        // Estira en el eje dominante del dash y aplasta en el otro
        bool horizontal = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        SetScaleImpulse(horizontal
            ? new Vector3(config.dashStretch, config.dashSquash, 1f)
            : new Vector3(config.dashSquash, config.dashStretch, 1f));

        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
        }

        ghostTimer = 0f;
        SpawnGhost();

        // Soplo de polvo en sentido contrario al dash
        if (dust != null)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = new Vector3(rb.position.x, rb.position.y, FxZ);
                Vector3 vel = new Vector3(-dir.x, -dir.y, 0f) * config.dustSpeed * Random.Range(0.8f, 1.6f)
                            + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f), 0f);
                Emit(dust, pos, vel, config.dustSize * Random.Range(0.7f, 1.1f));
            }
        }

        if (cameraFollow != null)
            cameraFollow.Shake(config.dashShakeAmplitude, config.dashShakeDuration, config.dashShakeFrequency);

        TimeController.Freeze(config.dashFreezeTime);
    }

    // ------------------------------------------------------------ Partículas

    private void EmitGroundDust(int count, float speedMul, float sizeMul)
    {
        if (dust == null) return;
        Vector3 feet = rb.position + new Vector3(0f, -0.5f, 0f);
        for (int i = 0; i < count; i++)
        {
            float side = Random.Range(-1f, 1f);
            Vector3 pos = new Vector3(feet.x + side * 0.4f, feet.y + 0.05f, FxZ);
            float dir = side >= 0f ? 1f : -1f;
            Vector3 vel = new Vector3(dir * Random.Range(0.4f, 1f), Random.Range(0.1f, 0.6f), 0f) * (config.dustSpeed * speedMul);
            Emit(dust, pos, vel, config.dustSize * sizeMul * Random.Range(0.6f, 1.2f));
        }
    }

    private void EmitWallDust(int count, int wallDir, float speedMul, float sizeMul)
    {
        if (wallDust == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(rb.position.x + wallDir * 0.5f, rb.position.y + Random.Range(-0.4f, 0.4f), FxZ);
            Vector3 vel = new Vector3(-wallDir * Random.Range(0.3f, 1f), Random.Range(-0.3f, 0.8f), 0f) * (config.dustSpeed * speedMul);
            Emit(wallDust, pos, vel, config.wallDustSize * sizeMul * Random.Range(0.7f, 1.3f));
        }
    }

    private void UpdateWallDust(float dt)
    {
        if (controller.State != PlayerController.MoveState.WallSlide || wallDust == null)
        {
            wallDustAccumulator = 0f;
            return;
        }

        // Más intenso cuanto más rápido baja
        float speed = Mathf.Max(0f, -controller.Velocity.y);
        float t = Mathf.Clamp01(speed / config.wallSlideMaxSpeed);
        wallDustAccumulator += Mathf.Lerp(config.wallDustMinRate, config.wallDustMaxRate, t) * dt;

        int n = Mathf.FloorToInt(wallDustAccumulator);
        if (n <= 0) return;
        wallDustAccumulator -= n;
        EmitWallDust(n, wallSide, Mathf.Lerp(0.5f, 1f, t), Mathf.Lerp(0.8f, 1.3f, t));
    }

    private void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startSize = size,
            startLifetime = config.dustLifetime * Random.Range(0.7f, 1.2f),
            startColor = config.dustColor,
            rotation = Random.Range(0f, 360f)
        };
        ps.Emit(p, 1);
    }
}
