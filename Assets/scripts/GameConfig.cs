using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración central del juego. Todos los valores ajustables viven aquí.
/// Crear con: Assets > Create > Climber > Game Config
/// </summary>
[CreateAssetMenu(menuName = "Climber/Game Config", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Arena (plano X/Y, Z bloqueado)")]
    [Tooltip("Posición X del centro de cada pared lateral (paredes en -X y +X).")]
    public float wallX = 4.5f;
    [Tooltip("Grosor de cada pared en X.")]
    public float wallThickness = 1f;
    [Tooltip("Altura total de cada pared lateral.")]
    public float wallHeight = 200f;
    [Tooltip("Y de la parte inferior de las paredes.")]
    public float wallBottomY = -10f;
    [Tooltip("Profundidad en Z de paredes, suelo y plataformas.")]
    public float depth = 2f;

    [Header("Suelo inicial")]
    public float floorY = -0.727f;
    public float floorHeight = 1f;

    [Header("Cámara")]
    [Range(20f, 90f)] public float cameraFov = 45f;
    [Tooltip("Si está activo, la distancia en Z se calcula para que ambas paredes completas entren en el ancho de pantalla (cualquier aspect ratio).")]
    public bool fitWallsToScreenWidth = true;
    [Tooltip("Distancia en Z a la cámara cuando 'Fit Walls To Screen Width' está desactivado.")]
    public float cameraDistance = 23f;
    [Tooltip("Espacio extra a cada lado, más allá del borde exterior de las paredes, al calcular la distancia.")]
    public float cameraSideMargin = 0.3f;
    [Tooltip("La cámara apunta a (Y del jugador + este valor). Positivo = el jugador queda por debajo del centro.")]
    public float cameraOffsetY = 4f;
    [Tooltip("Tiempo de suavizado vertical (SmoothDamp). Menor = sigue más rápido.")]
    public float cameraSmoothTime = 0.25f;

    [Header("Física del jugador")]
    [Tooltip("Rozamiento del PhysicsMaterial del jugador (0 = no se queda pegado a las paredes).")]
    [Min(0f)] public float playerFriction = 0f;

    [Header("Detección (layers)")]
    public LayerMask groundLayers;
    public LayerMask wallLayers;
    [Tooltip("Distancia bajo los pies para detectar suelo.")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("Distancia lateral desde el collider para detectar pared.")]
    public float wallCheckDistance = 0.08f;

    [Header("Movimiento lateral")]
    public float maxMoveSpeed = 8f;
    public float groundAcceleration = 70f;
    public float groundDeceleration = 80f;
    public float airAcceleration = 45f;
    public float airDeceleration = 20f;

    [Header("Gravedad")]
    [Tooltip("Gravedad propia del jugador (unidades/s²). El Rigidbody no usa la gravedad de Physics.")]
    public float gravity = 35f;
    [Tooltip("Multiplicador de gravedad al caer (>1 = cae más rápido de lo que sube).")]
    public float fallGravityMultiplier = 1.7f;
    public float maxFallSpeed = 25f;

    [Header("Salto")]
    public float jumpHeight = 3.5f;
    [Tooltip("Al soltar el botón mientras sube, la velocidad Y se multiplica por este valor.")]
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;
    [Tooltip("Tras cortar el salto (soltar el botón), multiplicador de gravedad mientras aún sube: corte suave en vez de seco.")]
    public float jumpCutGravityMult = 2.2f;
    [Tooltip("Por debajo de esta |velocidad Y| (cerca del punto más alto) la gravedad se reduce si se mantiene el salto.")]
    public float apexThreshold = 2.5f;
    [Range(0.2f, 1f)] public float apexGravityMult = 0.55f;

    [Header("Pared")]
    [Tooltip("Tiempo pegado a la pared (Y casi cero) antes de empezar a deslizar.")]
    public float wallStickTime = 0.4f;
    [Tooltip("Velocidad de caída mientras está pegado (casi cero).")]
    public float wallStickFallSpeed = 0.2f;
    [Tooltip("Velocidad inicial de deslizamiento por la pared.")]
    public float wallSlideInitialSpeed = 1.5f;
    [Tooltip("Aceleración del deslizamiento (unidades/s²).")]
    public float wallSlideAcceleration = 20f;
    public float wallSlideMaxSpeed = 14f;
    [Tooltip("Tiempo máximo deslizando; al cumplirse el jugador se suelta de la pared.")]
    public float wallSlideMaxTime = 0.8f;
    [Tooltip("Solo se pega a la pared si su velocidad Y de subida es menor o igual a este valor.")]
    public float wallAttachMaxRiseSpeed = 4f;
    [Tooltip("Velocidad hacia la pared mientras está pegado (mantiene el contacto).")]
    public float wallStickPushSpeed = 1f;
    public float wallJumpHeight = 4f;
    public float wallJumpHorizontalSpeed = 8f;
    [Tooltip("Si está activo, soltar el salto también recorta el wall jump. Desactivado = wall jump de altura fija (zigzag entre paredes más cómodo).")]
    public bool wallJumpVariableHeight = false;
    [Tooltip("Tiempo sin control lateral (y sin pegarse) tras un wall jump.")]
    public float wallJumpInputLockTime = 0.15f;
    [Tooltip("Tras el bloqueo del wall jump, segundos en que el control aéreo vuelve gradualmente al 100 %.")]
    public float wallJumpControlRampTime = 0.18f;
    [Tooltip("Frenada (m/s²) al pegarse a la pared: se detiene suavemente en vez de en seco.")]
    public float wallStickBrake = 70f;
    [Tooltip("Rapidez (m/s²) con que la velocidad de deslizamiento sigue a su objetivo.")]
    public float wallSlideBlend = 40f;

    [Header("Dash")]
    public float dashSpeed = 45f;
    public float dashDuration = 0.15f;
    [Tooltip("Al terminar el dash, la velocidad se multiplica por este valor (inercia).")]
    [Range(0f, 1f)] public float dashEndSpeedMultiplier = 0.3f;
    public float dashBufferTime = 0.1f;
    [Tooltip("Tiempo tras el dash antes de poder recargarlo al tocar suelo/pared.")]
    public float dashRechargeDelay = 0.1f;
    [Tooltip("Fracción final del dash en la que la velocidad baja suavemente hasta la de salida.")]
    [Range(0f, 0.9f)] public float dashEaseFraction = 0.4f;
    [Tooltip("Si al caer los pies quedan hasta esta distancia por debajo del borde de una plataforma, el jugador sube a ella (ayuda en bordes).")]
    public float ledgeSnapHeight = 0.35f;

    [Header("Táctil")]
    [Tooltip("Un toque más corto que esto (sin desplazarse) cuenta como tap = salto.")]
    public float touchTapMaxTime = 0.2f;
    [Tooltip("Tiempo que debe llevar el dedo apoyado antes de empezar a mover al jugador.")]
    public float touchMoveDelay = 0.1f;
    [Tooltip("Distancia mínima del swipe, como fracción del ancho de pantalla.")]
    [Range(0.02f, 0.5f)] public float swipeMinDistance = 0.1f;
    [Tooltip("Tiempo máximo en el que debe completarse un swipe.")]
    public float swipeMaxTime = 0.25f;
    [Tooltip("Tras un tap, el salto se considera 'mantenido' este tiempo (para no cortarlo).")]
    public float touchJumpHoldTime = 0.25f;
    [Tooltip("Solo para pruebas en editor: el ratón simula un dedo.")]
    public bool simulateTouchWithMouse = false;

    [Header("FX: squash & stretch (solo sobre 'Visual')")]
    [Tooltip("Rigidez del muelle que devuelve la escala a 1. Mayor = rebota más rápido.")]
    public float squashSpringStiffness = 220f;
    [Tooltip("Amortiguación del muelle. Menor = más rebotes.")]
    public float squashSpringDamping = 14f;
    [Tooltip("Escala Y al saltar (estiramiento).")]
    public float jumpStretchY = 1.35f;
    [Tooltip("Escala Y al aterrizar tras una caída corta.")]
    [Range(0.2f, 1f)] public float landSquashYMin = 0.8f;
    [Tooltip("Escala Y al aterrizar tras una caída larga.")]
    [Range(0.2f, 1f)] public float landSquashYMax = 0.5f;
    [Tooltip("Caída (unidades) a partir de la cual el aterrizaje tiene el efecto mínimo.")]
    public float landFallDistanceMin = 0.5f;
    [Tooltip("Caída (unidades) a partir de la cual el aterrizaje tiene el efecto máximo.")]
    public float landFallDistanceMax = 10f;
    [Tooltip("Caídas menores que esto no producen efecto de aterrizaje.")]
    public float landIgnoreFallDistance = 0.15f;
    [Tooltip("Cuánto se aplasta en X contra la pared mientras está pegado/deslizando.")]
    [Range(0f, 0.5f)] public float wallSquashAmount = 0.15f;
    [Tooltip("Escala X del impacto al pegarse a la pared.")]
    [Range(0.3f, 1f)] public float wallImpactSquashX = 0.7f;
    [Tooltip("Escala en el eje del dash (estiramiento).")]
    public float dashStretch = 1.4f;
    [Tooltip("Escala en el eje perpendicular al dash.")]
    public float dashSquash = 0.75f;

    [Header("FX: inclinación")]
    [Tooltip("Ángulo máximo de inclinación (grados) a velocidad horizontal máxima.")]
    public float tiltMaxAngle = 12f;
    [Tooltip("Rapidez con la que la inclinación sigue a la velocidad.")]
    public float tiltSmoothing = 18f;

    [Header("FX: partículas de polvo")]
    public Color dustColor = new Color(0.85f, 0.85f, 0.8f, 0.9f);
    public int jumpDustCount = 6;
    public int landDustMinCount = 4;
    public int landDustMaxCount = 24;
    public float dustSpeed = 2.5f;
    public float dustSize = 0.18f;
    public float dustLifetime = 0.5f;
    [Tooltip("Partículas/segundo al deslizar por la pared a la velocidad máxima de deslizamiento.")]
    public float wallDustMaxRate = 70f;
    [Tooltip("Partículas/segundo al deslizar por la pared a velocidad mínima.")]
    public float wallDustMinRate = 8f;
    public float wallDustSize = 0.1f;

    [Header("FX: dash")]
    [Tooltip("Duración (s, tiempo real) del freeze-frame al empezar el dash.")]
    public float dashFreezeTime = 0.05f;
    public float dashShakeAmplitude = 0.12f;
    public float dashShakeDuration = 0.2f;
    public float dashShakeFrequency = 35f;
    public float dashTrailTime = 0.25f;
    public float dashTrailWidth = 0.9f;
    public Color dashTrailColor = new Color(0.5f, 0.9f, 1f, 0.8f);

    [Header("FX: pulido")]
    [Tooltip("Estiramiento en el aire según la velocidad vertical.")]
    [Range(0f, 0.4f)] public float airStretchAmount = 0.12f;
    public float runBobAmount = 0.05f;
    public float runBobFrequency = 16f;
    [Tooltip("Inclinación (grados) hacia la pared mientras está pegado.")]
    public float wallLeanAngle = 9f;
    [Tooltip("Fracción final del tiempo en la pared en la que el personaje tiembla para avisar que se va a soltar.")]
    [Range(0f, 0.6f)] public float wallReleaseWarnFraction = 0.3f;
    public float wallReleaseJitter = 0.05f;
    [Tooltip("Cada cuántos segundos deja una imagen fantasma durante el dash.")]
    public float ghostInterval = 0.03f;
    public float ghostLifetime = 0.24f;
    public Color ghostColor = new Color(0.55f, 0.95f, 1f, 0.55f);
    [Tooltip("Brillo del cuerpo mientras el dash no está disponible (1 = igual).")]
    [Range(0.2f, 1f)] public float dashUsedTint = 0.55f;
    public float dashRechargeFlashTime = 0.18f;

[Header("Nivel: chunks y reciclaje")]
    [Tooltip("Altura de cada chunk vertical generado.")]
    public float chunkHeight = 12f;
    [Tooltip("Se genera nivel hasta esta distancia por encima del borde superior de la cámara.")]
    public float generateAheadDistance = 30f;
    [Tooltip("Chunks y segmentos de pared se reciclan al quedar esta distancia por debajo del borde inferior de la cámara.")]
    public float recycleBehindDistance = 12f;
    [Tooltip("Altura de cada segmento de pared reciclable.")]
    public float wallSegmentHeight = 20f;
    [Tooltip("Si está activo se usa 'fixedSeed' (para depurar). Si no, cada partida usa una semilla aleatoria.")]
    public bool useFixedSeed = false;
    public int fixedSeed = 12345;
    [Tooltip("Altura sobre el suelo inicial a la que la dificultad del nivel llega al máximo.")]
    public float difficultyMaxHeight = 300f;

    [Header("Nivel: plataformas")]
    public float platformThickness = 0.5f;
    [Tooltip("Ancho mínimo y máximo de las plataformas con dificultad 0.")]
    public float platformWidthMin = 2.2f;
    public float platformWidthMax = 4f;
    [Tooltip("Ancho mínimo y máximo con la dificultad al máximo (las plataformas se estrechan).")]
    public float platformWidthMinHard = 1.6f;
    public float platformWidthMaxHard = 2.6f;
    [Tooltip("Separación vertical mínima entre la superficie de dos plataformas consecutivas.")]
    public float minVerticalGap = 1.8f;
    [Tooltip("La separación vertical máxima es jumpHeight * este valor, para que siempre se alcance saltando.")]
    [Range(0.3f, 1f)] public float verticalReachSafety = 0.8f;
    [Tooltip("Desplazamiento horizontal mínimo entre plataformas consecutivas (evita torres rectas).")]
    public float minHorizontalShift = 1.2f;
    [Tooltip("Fracción del alcance horizontal teórico del salto que se usa para separar plataformas.")]
    [Range(0.2f, 1f)] public float horizontalReachSafety = 0.6f;
    [Tooltip("Fracción de la distancia del dash que se suma al alcance horizontal cuando la dificultad es máxima.")]
    [Range(0f, 1f)] public float dashReachUse = 0.6f;
    [Tooltip("Probabilidad de una plataforma extra (opcional) al lado opuesto de cada plataforma principal.")]
    [Range(0f, 1f)] public float extraPlatformChance = 0.35f;
    [Tooltip("Las plataformas son de una sola vía: solo colisionan si los pies del jugador están sobre su superficie menos esta tolerancia.")]
    public float oneWayTolerance = 0.12f;

    [Header("Nivel: huecos que piden dash")]
    public float dashGapStartHeight = 10f;
    [Range(0f, 0.6f)] public float dashGapMaxChance = 0.3f;
    public float dashGapRampHeight = 120f;
    [Tooltip("Separación vertical de un hueco de dash (mayor que el salto: hay que saltar + dash, o usar la pared).")]
    public float dashGapMin = 3.6f;
    public float dashGapMax = 5f;
    [Tooltip("Distancia horizontal máxima (borde a borde) en un hueco de dash.")]
    public float dashGapMaxShift = 1.8f;

    [Header("Nivel: tramos de pared (sin plataformas)")]
    public float wallShaftStartHeight = 25f;
    public float wallShaftIntervalMin = 30f;
    public float wallShaftIntervalMax = 50f;
    public float wallShaftHeightMin = 7f;
    public float wallShaftHeightMax = 11f;

    [Header("Nivel: tipos de plataforma (altura = metros sobre el suelo inicial)")]
    public float fallingStartHeight = 12f;
    [Range(0f, 0.6f)] public float fallingMaxChance = 0.3f;
    public float timedStartHeight = 35f;
    [Range(0f, 0.6f)] public float timedMaxChance = 0.25f;
    public float movingStartHeight = 60f;
    [Range(0f, 0.6f)] public float movingMaxChance = 0.25f;
    [Tooltip("Metros que tarda cada probabilidad en pasar de 0 a su máximo desde su altura de inicio.")]
    public float chanceRampHeight = 150f;

    [Header("Plataforma que cae")]
    [Tooltip("Tiempo que tiembla (aviso) desde que el jugador la pisa hasta que cae.")]
    public float fallingShakeTime = 0.5f;
    public float fallingShakeAmplitude = 0.07f;
    public float fallingGravity = 30f;
    [Tooltip("Distancia que recorre cayendo antes de desaparecer.")]
    public float fallingDespawnDistance = 14f;

    [Header("Plataforma temporizada")]
    [Tooltip("Tiempo que permanece sólida.")]
    public float timedVisibleTime = 2.5f;
    [Tooltip("Tiempo que permanece oculta (solo se ve su silueta).")]
    public float timedHiddenTime = 1.8f;
    [Tooltip("Duración del parpadeo de aviso antes de cada cambio de estado.")]
    public float timedWarnTime = 0.6f;
    public float timedBlinkHz = 12f;

    [Header("Plataforma móvil horizontal")]
    public float movingMinAmplitude = 1f;
    public float movingMaxAmplitude = 2.5f;
    [Tooltip("Velocidad angular (rad/s) del movimiento. Velocidad máxima = amplitud * omega.")]
    public float movingMinOmega = 0.8f;
    public float movingMaxOmega = 1.8f;

    [Header("Zona segura")]
    [Tooltip("Cada cuántos metros aparece una plataforma ancha para respirar.")]
    public float safeZoneInterval = 60f;
    public float safeZoneThickness = 0.8f;
    [Tooltip("Ancho de la plataforma segura como fracción del ancho interior entre paredes.")]
    [Range(0.4f, 1f)] public float safeZoneWidthFraction = 0.92f;

    [Header("Cámara: auto-scroll")]
    [Tooltip("X = altura de la cámara sobre su inicio (m). Y = velocidad de ascenso (m/s). Como la cámara sube, la velocidad crece con el tiempo.")]
    public AnimationCurve scrollSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f), new Keyframe(100f, 1.1f), new Keyframe(250f, 1.9f), new Keyframe(500f, 2.7f), new Keyframe(1000f, 3.3f));
    [Tooltip("Velocidad máxima del auto-scroll (m/s), aunque la curva diga más.")]
    public float scrollMaxSpeed = 3.4f;
    [Tooltip("Segundos de gracia al empezar antes de que la cámara suba sola.")]
    public float scrollStartDelay = 2.5f;

    [Header("Muerte y reinicio")]
    [Tooltip("Metros por debajo del borde inferior de la cámara a partir de los cuales el jugador muere.")]
    public float deathMargin = 0.5f;
    [Tooltip("Escala de tiempo de la cámara lenta al morir.")]
    [Range(0.05f, 1f)] public float deathSlowMoScale = 0.25f;
    [Tooltip("Segundos (tiempo real) de cámara lenta antes de mostrar la pantalla de puntuación.")]
    public float deathSlowMoTime = 0.8f;
    [Tooltip("Segundos (tiempo real) en que la pantalla de puntuación ignora toques/teclas (evita reintentos accidentales).")]
    public float retryInputDelay = 0.45f;
    [Tooltip("Permite reiniciar con la tecla R (pruebas en el editor).")]
    public bool restartWithKey = true;

    [Header("Dificultad del jugador (0 en el suelo, 1 a difficultyMaxHeight)")]
    [Tooltip("Aumento máximo de la velocidad lateral con la dificultad (0.25 = +25 %).")]
    [Range(0f, 1f)] public float difficultyMoveSpeedBonus = 0.25f;
    [Tooltip("Aumento máximo de la velocidad del dash con la dificultad.")]
    [Range(0f, 1f)] public float difficultyDashSpeedBonus = 0.15f;

    [Header("Fondo por altura")]
    [Tooltip("Metros antes de cada etapa en los que empieza la transición hacia ella.")]
    public float backgroundTransitionHeight = 80f;
    public List<BackgroundStage> backgroundStages = new List<BackgroundStage>
    {
        new BackgroundStage("Atardecer", 0f, new Color(0.33f, 0.27f, 0.58f), new Color(1f, 0.62f, 0.38f), 0f, 1f, new Color(1f, 0.86f, 0.76f), new Color(1f, 0.86f, 0.7f), 1f),
        new BackgroundStage("Noche", 150f, new Color(0.04f, 0.05f, 0.18f), new Color(0.22f, 0.2f, 0.45f), 0.55f, 0.5f, new Color(0.55f, 0.55f, 0.8f), new Color(0.6f, 0.65f, 1f), 0.7f),
        new BackgroundStage("Espacio", 350f, new Color(0f, 0f, 0.03f), new Color(0.05f, 0.02f, 0.14f), 1f, 0f, new Color(0.5f, 0.5f, 0.7f), new Color(0.8f, 0.85f, 1f), 0.6f),
        new BackgroundStage("Nebulosa", 700f, new Color(0.12f, 0.03f, 0.22f), new Color(0.02f, 0.12f, 0.25f), 1f, 0f, new Color(0.5f, 0.5f, 0.7f), new Color(0.85f, 0.75f, 1f), 0.6f)
    };

    [Header("Clima: cuándo ocurren los eventos")]
    [Tooltip("Altura (m) a partir de la cual puede aparecer el primer evento.")]
    public float weatherFirstHeight = 40f;
    [Tooltip("Cada cuántos metros se tira el dado para un evento (y mínimo de metros tras terminar uno).")]
    public float weatherRollInterval = 40f;
    [Range(0f, 1f)] public float weatherChance = 0.6f;
    public float weatherDurationMin = 15f;
    public float weatherDurationMax = 20f;
    [Tooltip("Segundos de aviso en pantalla antes de que empiece el evento.")]
    public float weatherWarnTime = 1f;
    [Tooltip("Segundos de entrada y salida gradual del efecto.")]
    public float weatherFadeTime = 1f;
    public float rainWeight = 1f;
    public float windWeight = 1f;
    public float fogWeight = 1f;

    [Header("Clima: lluvia")]
    [Tooltip("Multiplicador de la desaceleración en el suelo (menor = se desliza más al frenar).")]
    [Range(0.05f, 1f)] public float rainGroundDecelMult = 0.25f;
    [Range(0.2f, 1f)] public float rainGroundAccelMult = 0.7f;
    [Tooltip("Multiplicador del tiempo pegado a la pared.")]
    [Range(0.1f, 1f)] public float rainWallStickMult = 0.4f;
    [Tooltip("Multiplicador de la velocidad de deslizamiento por la pared.")]
    [Range(1f, 3f)] public float rainWallSlideSpeedMult = 1.5f;
    public float rainParticleRate = 350f;
    public Color rainColor = new Color(0.65f, 0.8f, 1f, 0.55f);

    [Header("Clima: viento")]
    [Tooltip("Velocidad lateral constante (m/s) con que el viento empuja al jugador.")]
    public float windStrength = 3.5f;
    public float windParticleRate = 45f;
    public Color windColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Clima: niebla")]
    [Tooltip("Opacidad máxima de la niebla en el borde superior de la pantalla.")]
    [Range(0f, 1f)] public float fogMaxAlpha = 0.85f;
    public Color fogColor = new Color(0.78f, 0.84f, 0.9f, 1f);

    [Header("Valor de ejecución (lo escribe GameTuning; no editar)")]
    [Tooltip("Velocidad lateral que añade el viento al jugador (m/s, con signo).")]
    public float windSpeed = 0f;

    [Header("Colores de plataforma")]
    public Color normalPlatformColor = new Color(0.35f, 0.6f, 0.95f, 1f);
    public Color fallingPlatformColor = new Color(0.95f, 0.55f, 0.2f, 1f);
    public Color timedPlatformColor = new Color(0.7f, 0.45f, 0.95f, 1f);
    public Color movingPlatformColor = new Color(0.2f, 0.85f, 0.75f, 1f);
    public Color safePlatformColor = new Color(0.45f, 0.85f, 0.4f, 1f);

    private void Reset()
    {
        groundLayers = LayerMask.GetMask("Ground");
        wallLayers = LayerMask.GetMask("Wall");
    }
}
