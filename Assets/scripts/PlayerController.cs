using System;
using UnityEngine;

/// <summary>
/// Física del jugador en el plano X/Y: movimiento con aceleración, salto variable (coyote + buffer, corte suave,
/// flote en el punto más alto), wall stick/slide/jump con transiciones suaves, dash con salida gradual y
/// ayuda en bordes. La gravedad se aplica a mano (el Rigidbody no usa gravedad de Physics).
/// Todo el tuning vive en GameConfig. Anima solo el hijo "Visual", nunca este objeto.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(PlayerInputReader))]
public class PlayerController : MonoBehaviour
{
    public enum MoveState { Normal, WallStick, WallSlide, Dash }

    [SerializeField] private GameConfig config;

    // ---- Eventos para otras fases (VFX, audio, generación, puntuación...) ----
    public event Action OnJump;
    public event Action OnLand;
    /// <summary>Parámetro: lado de la pared (-1 izquierda, +1 derecha).</summary>
    public event Action<int> OnWallStick;
    /// <summary>Parámetro: lado de la pared de la que salta (-1 izquierda, +1 derecha).</summary>
    public event Action<int> OnWallJump;
    /// <summary>Parámetro: dirección normalizada del dash.</summary>
    public event Action<Vector2> OnDash;

    public MoveState State { get; private set; } = MoveState.Normal;
    public bool IsGrounded { get; private set; }
    /// <summary>-1 pared a la izquierda, +1 a la derecha, 0 ninguna.</summary>
    public int WallDirection { get; private set; }
    public bool DashAvailable { get { return dashAvailable; } }
    public Vector2 Velocity { get { return rb.linearVelocity; } }
    /// <summary>Progreso 0..1 del tiempo total en la pared (pegado + deslizamiento). 1 = a punto de soltarse.</summary>
    public float WallProgress01 { get; private set; }
    /// <summary>Si es false, el jugador ignora la entrada (pausa, muerte).</summary>
    public bool InputEnabled { get; set; } = true;

    private static readonly Collider[] groundHits = new Collider[8];

    private Rigidbody rb;
    private BoxCollider box;
    private PlayerInputReader input;

    // Entrada latcheada en Update
    private Vector2 moveInput;
    private bool jumpHeld;
    private Vector2 bufferedDashDir;

    // Timers (se decrementan en FixedUpdate)
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float dashBufferTimer;
    private float lockTimer;
    private float dashRechargeTimer;
    private float stateTimer;

    private bool jumpRising;
    private bool jumpCut;
    private bool dashAvailable = true;
    private int facing = 1;
    private int releasedWallDir;
    private Vector2 dashDir;
    private float appliedWind;
    private float controlRamp = 1f;
    private float inputBlockedUntil;

    private float JumpVelocity { get { return Mathf.Sqrt(2f * config.gravity * config.jumpHeight); } }
    private float WallJumpVelocity { get { return Mathf.Sqrt(2f * config.gravity * config.wallJumpHeight); } }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();
        input = GetComponent<PlayerInputReader>();
        rb.useGravity = false;
    }

    /// <summary>Ignora la entrada durante unos segundos (tiempo real), p. ej. justo tras reintentar.</summary>
    public void BlockInput(float seconds)
    {
        inputBlockedUntil = Time.unscaledTime + seconds;
    }

    private void Update()
    {
        if (!InputEnabled || Time.unscaledTime < inputBlockedUntil)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            return;
        }

        moveInput = input.Move;
        jumpHeld = input.JumpHeld;
        if (Mathf.Abs(moveInput.x) > 0.2f) facing = moveInput.x > 0f ? 1 : -1;

        if (input.JumpPressed) jumpBufferTimer = config.jumpBufferTime;
        if (input.DashPressed)
        {
            dashBufferTimer = config.dashBufferTime;
            bufferedDashDir = input.DashDirection;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;
        v.x -= appliedWind;   // quita el viento aplicado el paso anterior: v es solo la velocidad propia

        jumpBufferTimer -= dt;
        dashBufferTimer -= dt;
        lockTimer -= dt;
        dashRechargeTimer -= dt;
        if (lockTimer <= 0f)
            controlRamp = Mathf.MoveTowards(controlRamp, 1f, dt / Mathf.Max(0.01f, config.wallJumpControlRampTime));

        // ---- Detección ----
        bool wasGrounded = IsGrounded;
        IsGrounded = CheckGround(v.y);
        if (!IsGrounded && v.y <= 0f && State == MoveState.Normal && TryLedgeSnap())
        {
            IsGrounded = true;
            v.y = 0f;
        }
        WallDirection = CheckWall();

        if (IsGrounded)
        {
            coyoteTimer = config.coyoteTime;
            releasedWallDir = 0;
            jumpCut = false;
            if (!wasGrounded && OnLand != null) OnLand();
        }
        else
        {
            coyoteTimer -= dt;
        }
        if (WallDirection == 0) releasedWallDir = 0;

        if (State != MoveState.Dash && dashRechargeTimer <= 0f && (IsGrounded || WallDirection != 0))
            dashAvailable = true;

        // ---- Dash ----
        if (dashBufferTimer > 0f && dashAvailable && State != MoveState.Dash)
            StartDash();

        // ---- Estado ----
        switch (State)
        {
            case MoveState.Dash: v = TickDash(dt); break;
            case MoveState.WallStick:
            case MoveState.WallSlide: v = TickWall(v, dt); break;
            default: v = TickNormal(v, dt); break;
        }
        if (State != MoveState.WallStick && State != MoveState.WallSlide) WallProgress01 = 0f;

        // El viento (clima) empuja lateralmente solo en estado normal (suelo y aire)
        float wind = State == MoveState.Normal ? config.windSpeed : 0f;
        appliedWind = wind;
        rb.linearVelocity = new Vector3(v.x + wind, v.y, 0f);
    }

    // ------------------------------------------------------------------ Normal

    private Vector2 TickNormal(Vector2 v, float dt)
    {
        TryJump(ref v);

        // Pegarse a la pared (la frenada vertical es progresiva, en TickWall)
        if (State == MoveState.Normal && !IsGrounded && WallDirection != 0 && WallDirection != releasedWallDir
            && lockTimer <= 0f && v.y <= config.wallAttachMaxRiseSpeed)
        {
            EnterWallStick();
            v.x = WallDirection * config.wallStickPushSpeed;
            return v;
        }

        // Horizontal con aceleración / desaceleración (bloqueado y luego progresivo tras wall jump)
        if (lockTimer <= 0f)
        {
            float x = Mathf.Abs(moveInput.x) > 0.2f ? moveInput.x : 0f;
            float target = x * config.maxMoveSpeed;
            float rate = Mathf.Abs(target) > 0.01f
                ? (IsGrounded ? config.groundAcceleration : config.airAcceleration)
                : (IsGrounded ? config.groundDeceleration : config.airDeceleration);
            if (!IsGrounded) rate *= Mathf.Lerp(0.3f, 1f, controlRamp);
            v.x = Mathf.MoveTowards(v.x, target, rate * dt);
        }

        // Salto variable: al soltar el botón se recorta la subida y se aplica más gravedad (corte suave)
        if (jumpRising)
        {
            if (v.y <= 0f) jumpRising = false;
            else if (!jumpHeld && !jumpCut)
            {
                v.y *= config.jumpCutMultiplier;
                jumpCut = true;
            }
        }

        // Gravedad: más fuerte al caer, extra tras cortar el salto, menor cerca del punto más alto
        if (IsGrounded && v.y <= 0f)
        {
            v.y = 0f;
        }
        else
        {
            float mult = 1f;
            if (v.y < 0f) mult = config.fallGravityMultiplier;
            else if (jumpCut) mult = config.jumpCutGravityMult;
            if (jumpHeld && !jumpCut && Mathf.Abs(v.y) < config.apexThreshold) mult *= config.apexGravityMult;
            v.y = Mathf.Max(v.y - config.gravity * mult * dt, -config.maxFallSpeed);
        }

        return v;
    }

    private bool TryJump(ref Vector2 v)
    {
        if (jumpBufferTimer <= 0f) return false;

        // Salto desde suelo (con coyote time)
        if (coyoteTimer > 0f)
        {
            v.y = JumpVelocity;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            jumpRising = true;
            jumpCut = false;
            State = MoveState.Normal;
            if (OnJump != null) OnJump();
            return true;
        }

        // Wall jump
        if (!IsGrounded && WallDirection != 0 && lockTimer <= 0f)
        {
            int wall = WallDirection;
            v = new Vector2(-wall * config.wallJumpHorizontalSpeed, WallJumpVelocity);
            State = MoveState.Normal;
            lockTimer = config.wallJumpInputLockTime;
            controlRamp = 0f;
            releasedWallDir = wall;
            facing = -wall;
            jumpBufferTimer = 0f;
            jumpRising = config.wallJumpVariableHeight;   // fijo por defecto: no se recorta al soltar
            jumpCut = false;
            if (OnWallJump != null) OnWallJump(wall);
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------- Wall

    private void EnterWallStick()
    {
        State = MoveState.WallStick;
        stateTimer = 0f;
        jumpRising = false;
        jumpCut = false;
        facing = -WallDirection;
        if (OnWallStick != null) OnWallStick(WallDirection);
    }

    private Vector2 TickWall(Vector2 v, float dt)
    {
        if (TryJump(ref v)) return v;

        // Perdió la pared o tocó suelo -> vuelve a Normal
        if (IsGrounded || WallDirection == 0)
        {
            State = MoveState.Normal;
            return TickNormal(v, dt);
        }

        stateTimer += dt;
        v.x = WallDirection * config.wallStickPushSpeed;
        float total = Mathf.Max(0.01f, config.wallStickTime + config.wallSlideMaxTime);

        if (State == MoveState.WallStick)
        {
            // Frena con suavidad hasta quedar casi quieto
            v.y = Mathf.MoveTowards(v.y, -config.wallStickFallSpeed, config.wallStickBrake * dt);
            WallProgress01 = Mathf.Clamp01(stateTimer / total);
            if (stateTimer >= config.wallStickTime)
            {
                State = MoveState.WallSlide;
                stateTimer = 0f;
            }
        }
        else
        {
            WallProgress01 = Mathf.Clamp01((config.wallStickTime + stateTimer) / total);
            if (stateTimer >= config.wallSlideMaxTime)
            {
                // Se suelta de la pared con un pequeño empujón; no puede volver a pegarse a ella hasta separarse
                int wall = WallDirection;
                releasedWallDir = wall;
                State = MoveState.Normal;
                return TickNormal(new Vector2(-wall * 0.6f, v.y), dt);
            }
            // Desliza acelerando poco a poco (la velocidad sigue a su objetivo sin saltos)
            float target = Mathf.Min(config.wallSlideInitialSpeed + config.wallSlideAcceleration * stateTimer, config.wallSlideMaxSpeed);
            v.y = Mathf.MoveTowards(v.y, -target, config.wallSlideBlend * dt);
        }

        return v;
    }

    // -------------------------------------------------------------------- Dash

    private void StartDash()
    {
        Vector2 dir;
        if (bufferedDashDir.sqrMagnitude > 0.01f) dir = SnapTo8(bufferedDashDir);
        else if (moveInput.sqrMagnitude > 0.3f * 0.3f) dir = SnapTo8(moveInput);
        else dir = new Vector2(facing, 0f);

        dashDir = dir;
        State = MoveState.Dash;
        stateTimer = config.dashDuration;
        dashAvailable = false;
        dashBufferTimer = 0f;
        jumpRising = false;
        jumpCut = false;
        if (OnDash != null) OnDash(dashDir);
    }

    private Vector2 TickDash(float dt)
    {
        stateTimer -= dt;
        float endSpeed = config.dashSpeed * config.dashEndSpeedMultiplier;
        if (stateTimer <= 0f)
        {
            State = MoveState.Normal;
            dashRechargeTimer = config.dashRechargeDelay;
            return dashDir * endSpeed;
        }

        // Velocidad constante y, en el tramo final, bajada suave (smoothstep) hasta la velocidad de salida
        float progress = 1f - stateTimer / Mathf.Max(0.0001f, config.dashDuration);
        float ease = config.dashEaseFraction;
        float speed = config.dashSpeed;
        if (ease > 0f && progress > 1f - ease)
        {
            float k = Mathf.Clamp01((progress - (1f - ease)) / ease);
            speed = Mathf.Lerp(config.dashSpeed, endSpeed, k * k * (3f - 2f * k));
        }
        return dashDir * speed;
    }

    private static Vector2 SnapTo8(Vector2 v)
    {
        float angle = Mathf.Round(Mathf.Atan2(v.y, v.x) / (Mathf.PI * 0.25f)) * (Mathf.PI * 0.25f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // --------------------------------------------------------------- Detección

    private bool CheckGround(float velocityY)
    {
        if (velocityY > 0.01f) return false;
        Bounds b = box.bounds;
        float halfH = (config.groundCheckDistance + 0.04f) * 0.5f;
        Vector3 center = new Vector3(b.center.x, b.min.y - config.groundCheckDistance + halfH, b.center.z);
        Vector3 half = new Vector3(b.extents.x * 0.9f, halfH, b.extents.z * 0.9f);
        int count = Physics.OverlapBoxNonAlloc(center, half, groundHits, Quaternion.identity, config.groundLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            // Las plataformas son de una sola vía: solo cuentan como suelo si los pies están sobre ellas
            ClimbPlatform platform;
            if (groundHits[i].TryGetComponent(out platform) && !platform.SupportsFrom(b.min.y)) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ayuda en bordes: si al caer los pies quedan apenas por debajo de la superficie de una plataforma
    /// (dentro de ledgeSnapHeight) y hay solapamiento horizontal suficiente, sube al jugador encima.
    /// </summary>
    private bool TryLedgeSnap()
    {
        if (config.ledgeSnapHeight <= 0f) return false;
        Bounds b = box.bounds;
        float feet = b.min.y;
        Vector3 center = new Vector3(b.center.x, feet + config.ledgeSnapHeight * 0.5f, b.center.z);
        Vector3 half = new Vector3(b.extents.x * 0.9f, config.ledgeSnapHeight * 0.5f, b.extents.z * 0.9f);
        int count = Physics.OverlapBoxNonAlloc(center, half, groundHits, Quaternion.identity, config.groundLayers, QueryTriggerInteraction.Ignore);

        float bestTop = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            ClimbPlatform platform;
            if (!groundHits[i].TryGetComponent(out platform)) continue;
            float top = platform.TopY;
            if (top <= feet || top - feet > config.ledgeSnapHeight) continue;
            Bounds pb = groundHits[i].bounds;
            float overlap = Mathf.Min(b.max.x, pb.max.x) - Mathf.Max(b.min.x, pb.min.x);
            if (overlap < b.size.x * 0.3f) continue;
            if (top < bestTop) bestTop = top;
        }
        if (bestTop == float.MaxValue) return false;

        Vector3 p = rb.position;
        p.y += bestTop - feet + 0.005f;
        rb.position = p;
        transform.position = p;
        return true;
    }

    private int CheckWall()
    {
        int first = facing >= 0 ? 1 : -1;
        if (WallCast(first)) return first;
        if (WallCast(-first)) return -first;
        return 0;
    }

    private bool WallCast(int dir)
    {
        Bounds b = box.bounds;
        Vector3 half = new Vector3(0.02f, b.extents.y * 0.8f, b.extents.z * 0.8f);
        float distance = b.extents.x - half.x + config.wallCheckDistance;
        return Physics.BoxCast(b.center, half, new Vector3(dir, 0f, 0f), Quaternion.identity, distance,
            config.wallLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Devuelve al jugador al estado inicial en 'position' (reinicio de partida).</summary>
    public void ResetState(Vector3 position)
    {
        position.z = 0f;
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        State = MoveState.Normal;
        IsGrounded = false;
        WallDirection = 0;
        WallProgress01 = 0f;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        dashBufferTimer = 0f;
        lockTimer = 0f;
        dashRechargeTimer = 0f;
        stateTimer = 0f;
        jumpRising = false;
        jumpCut = false;
        dashAvailable = true;
        releasedWallDir = 0;
        appliedWind = 0f;
        controlRamp = 1f;
        facing = 1;
        dashDir = Vector2.zero;
    }
}
