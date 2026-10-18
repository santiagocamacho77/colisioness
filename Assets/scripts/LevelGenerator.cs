using System.Collections.Generic;
using UnityEngine;
using Kind = ClimbPlatform.Kind;

/// <summary>
/// Generador infinito del nivel. Crea plataformas por chunks verticales delante de la cámara y recicla
/// los que quedan por debajo (object pooling). Las paredes laterales también se reciclan por segmentos.
/// Garantiza un camino alcanzable: la separación vertical máxima sale de la altura de salto real y la
/// horizontal, del alcance del salto (más una parte del dash al subir la dificultad). Nunca encadena
/// dos plataformas "arriesgadas" (que cae / temporizada). Cada partida usa una semilla aleatoria.
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    private class Chunk
    {
        public float bottomY;
        public float topY;
        public readonly List<ClimbPlatform> platforms = new List<ClimbPlatform>(16);
    }

    private struct WallPair
    {
        public Transform left;
        public Transform right;
    }

    [SerializeField] private GameConfig config;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private GameObject wallSegmentPrefab;
    [Tooltip("Suelo inicial (si se deja vacío se busca un objeto llamado 'Floor').")]
    [SerializeField] private Transform startFloor;
    [SerializeField] private Transform platformsRoot;
    [SerializeField] private Transform wallsRoot;
    [Tooltip("Plataformas que se crean al iniciar para no instanciar durante el juego.")]
    [SerializeField] private int platformPrewarm = 40;
    [Tooltip("Material con textura por tipo, en el orden de ClimbPlatform.Kind: Normal, Falling, Timed, Moving, Safe.")]
    [SerializeField] private Material[] kindMaterials;
    [Tooltip("Material transparente (URP Particles/Unlit) para la barra de tiempo y el riel de las plataformas.")]
    [SerializeField] private Material indicatorMaterial;

    /// <summary>Tipo de paso entre una plataforma principal y la siguiente.</summary>
    private enum Step { Jump, Dash, Shaft }

    /// <summary>Semilla de la partida actual.</summary>
    public int Seed { get; private set; }
    /// <summary>Y de la superficie de la última plataforma principal generada.</summary>
    public float HighestGeneratedY => chainTopY;

    private PlayerController player;
    private Camera cam;
    private CameraFollow camFollow;
    private System.Random rng;

    private readonly Stack<ClimbPlatform> platformPool = new Stack<ClimbPlatform>();
    private readonly Stack<Chunk> chunkPool = new Stack<Chunk>();
    private readonly Queue<Chunk> activeChunks = new Queue<Chunk>();
    private readonly Queue<WallPair> walls = new Queue<WallPair>();

    // Estado de la cadena de plataformas principales (el camino garantizado)
    private float chainTopY;
    private float chainX;
    private float chainWidth;
    private Kind chainKind;

    private float startTopY;
    private float nextChunkBottomY;
    private float nextSafeY;
    private float nextShaftY;
    private Step lastStep;
    private float wallTopY;

    private float InnerHalfWidth => config.wallX - config.wallThickness * 0.5f;

    // ------------------------------------------------------------------ Ciclo de vida

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerController>();
        cam = Camera.main;
        if (cam != null) camFollow = cam.GetComponent<CameraFollow>();

        if (startFloor == null)
        {
            GameObject floor = GameObject.Find("Floor");
            if (floor != null) startFloor = floor.transform;
        }
        if (platformsRoot == null) platformsRoot = transform;
        if (wallsRoot == null) wallsRoot = transform;

        ClimbPlatform.BindPlayer(player);
        ClimbPlatform.BindAssets(indicatorMaterial);
        DebrisFx.Init(indicatorMaterial);

        for (int i = 0; i < platformPrewarm; i++)
            platformPool.Push(CreatePlatform());
    }

    private void Start()
    {
        ResetLevel();
    }

    private void Update()
    {
        if (config == null || cam == null) return;

        GetCameraRange(out float camBottom, out float camTop);
        float recycleY = camBottom - config.recycleBehindDistance;

        int guard = 0;
        while (nextChunkBottomY < camTop + config.generateAheadDistance && guard++ < 40)
            GenerateChunk();

        while (activeChunks.Count > 0 && activeChunks.Peek().topY < recycleY)
            RecycleChunk(activeChunks.Dequeue());

        RecycleWalls(recycleY, camTop + config.generateAheadDistance);

        if (startFloor != null && startFloor.gameObject.activeSelf && startTopY < recycleY)
            startFloor.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ Reinicio

    /// <summary>Regenera el nivel desde cero con una semilla nueva (o la fija, si está activada).</summary>
    public void ResetLevel()
    {
        int seed = config.useFixedSeed ? config.fixedSeed : Random.Range(1, int.MaxValue);
        ResetLevel(seed);
    }

    public void ResetLevel(int seed)
    {
        Seed = seed;
        rng = new System.Random(seed);

        while (activeChunks.Count > 0) RecycleChunk(activeChunks.Dequeue());

        if (startFloor != null)
        {
            startFloor.gameObject.SetActive(true);
            startTopY = startFloor.position.y + startFloor.lossyScale.y * 0.5f;
            chainWidth = startFloor.lossyScale.x;
        }
        else
        {
            startTopY = 0f;
            chainWidth = InnerHalfWidth * 2f;
        }

        chainTopY = startTopY;
        chainX = 0f;
        chainKind = Kind.Normal;
        nextChunkBottomY = startTopY;
        nextSafeY = startTopY + config.safeZoneInterval;
        nextShaftY = startTopY + config.wallShaftStartHeight;
        lastStep = Step.Jump;

        // Paredes: se recolocan desde abajo y Update las rellena según la cámara
        WallPair[] old = walls.ToArray();
        walls.Clear();
        wallTopY = config.wallBottomY;
        for (int i = 0; i < old.Length; i++)
        {
            PlaceWallPair(old[i], wallTopY + config.wallSegmentHeight * 0.5f);
            wallTopY += config.wallSegmentHeight;
            walls.Enqueue(old[i]);
        }

        Debug.Log("[LevelGenerator] Semilla de la partida: " + Seed);

        if (cam != null)
        {
            GetCameraRange(out float camBottom, out float camTop);
            RecycleWalls(camBottom - config.recycleBehindDistance, camTop + config.generateAheadDistance);
            int guard = 0;
            while (nextChunkBottomY < camTop + config.generateAheadDistance && guard++ < 40)
                GenerateChunk();
        }
    }

    // ------------------------------------------------------------------ Cámara

    private void GetCameraRange(out float bottom, out float top)
    {
        float y = camFollow != null ? camFollow.BaseY : cam.transform.position.y;
        float half = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(cam.transform.position.z);
        bottom = y - half;
        top = y + half;
    }

    // ------------------------------------------------------------------ Chunks

    private void GenerateChunk()
    {
        Chunk c = chunkPool.Count > 0 ? chunkPool.Pop() : new Chunk();
        c.platforms.Clear();
        c.bottomY = nextChunkBottomY;
        c.topY = c.bottomY + config.chunkHeight;
        nextChunkBottomY = c.topY;

        int guard = 0;
        while (guard++ < 64)
        {
            float t = Difficulty(chainTopY);

            // Tipo de paso: tramo de pared (sin plataformas), hueco que pide dash, o salto normal
            Step step = Step.Jump;
            float gap;
            if (chainTopY >= nextShaftY)
            {
                step = Step.Shaft;
                gap = Mathf.Lerp(config.wallShaftHeightMin, config.wallShaftHeightMax, Next01());
            }
            else if (lastStep != Step.Dash && Next01() < DashGapChance(chainTopY))
            {
                step = Step.Dash;
                gap = Mathf.Lerp(config.dashGapMin, config.dashGapMax, Mathf.Clamp01(t * 0.5f + Next01() * 0.6f));
            }
            else
            {
                gap = RollGap(t);
            }

            float y = chainTopY + gap;
            if (y >= c.topY) break;   // esta fila pertenece al siguiente chunk

            if (y >= nextSafeY) PlaceSafe(c, y);
            else PlaceMain(c, y, gap, t, step);

            if (step == Step.Shaft)
                nextShaftY = y + Mathf.Lerp(config.wallShaftIntervalMin, config.wallShaftIntervalMax, Next01());
            lastStep = step;
        }

        activeChunks.Enqueue(c);
    }

    private void RecycleChunk(Chunk c)
    {
        for (int i = 0; i < c.platforms.Count; i++)
        {
            c.platforms[i].Despawn();
            platformPool.Push(c.platforms[i]);
        }
        c.platforms.Clear();
        chunkPool.Push(c);
    }

    // ------------------------------------------------------------------ Colocación

    private void PlaceMain(Chunk c, float y, float gap, float t, Step step)
    {
        Kind kind = step == Step.Shaft ? Kind.Normal : RollKind(y, chainKind);
        // Tras un hueco grande la plataforma de llegada debe estar ahí al llegar
        if (step == Step.Dash && kind == Kind.Timed) kind = Kind.Normal;

        float w = step == Step.Shaft
            ? Mathf.Lerp(Mathf.Max(3f, config.platformWidthMin), Mathf.Max(3.4f, config.platformWidthMax), Next01())
            : RollWidth(t);
        w = Mathf.Min(w, InnerHalfWidth * 2f - 0.6f);

        float x;
        if (step == Step.Shaft) x = Mathf.Lerp(-InnerHalfWidth + w * 0.5f, InnerHalfWidth - w * 0.5f, Next01());
        else if (step == Step.Dash) x = ChooseXWithin(w, config.dashGapMaxShift);
        else x = ChooseX(w, gap, t);

        float amplitude = 0f;
        float omega = 0f;
        if (kind == Kind.Moving)
        {
            float half = InnerHalfWidth;
            float lo = -half + w * 0.5f;
            float hi = half - w * 0.5f;
            const float minRoom = 0.9f;
            if (hi - lo < minRoom * 2f)
            {
                kind = Kind.Normal;   // la arena no deja espacio para moverse
            }
            else
            {
                x = Mathf.Clamp(x, lo + minRoom, hi - minRoom);
                float room = Mathf.Min(x - lo, hi - x);
                float desired = Mathf.Lerp(config.movingMinAmplitude, config.movingMaxAmplitude, Next01());
                amplitude = Mathf.Min(desired, room);
                omega = Mathf.Lerp(config.movingMinOmega, config.movingMaxOmega, Mathf.Clamp01(t * 0.6f + Next01() * 0.4f));
            }
        }

        SpawnPlatform(c, kind, x, y, w, config.platformThickness, amplitude, omega);

        chainTopY = y;
        chainX = x;
        chainWidth = w;
        chainKind = kind;

        if (kind != Kind.Moving) TryPlaceExtra(c, y, x, w, t);
    }

    private void PlaceSafe(Chunk c, float y)
    {
        float w = InnerHalfWidth * 2f * config.safeZoneWidthFraction;
        SpawnPlatform(c, Kind.Safe, 0f, y, w, config.safeZoneThickness, 0f, 0f);

        chainTopY = y;
        chainX = 0f;
        chainWidth = w;
        chainKind = Kind.Safe;
        nextSafeY = y + config.safeZoneInterval;
    }

    /// <summary>Plataforma opcional del lado opuesto. Al ser de una sola vía nunca estorba el camino principal.</summary>
    private void TryPlaceExtra(Chunk c, float y, float mainX, float mainW, float t)
    {
        if (Next01() >= config.extraPlatformChance) return;

        float w = RollWidth(t);
        float half = InnerHalfWidth;
        float lo, hi;
        if (mainX >= 0f)
        {
            lo = -half + w * 0.5f;
            hi = mainX - mainW * 0.5f - 0.6f - w * 0.5f;
        }
        else
        {
            lo = mainX + mainW * 0.5f + 0.6f + w * 0.5f;
            hi = half - w * 0.5f;
        }
        if (hi < lo) return;

        float x = Mathf.Lerp(lo, hi, Next01());
        float dy = (Next01() - 0.5f) * 0.8f;
        Kind kind = RollKind(y, Kind.Normal);
        if (kind == Kind.Moving) kind = Kind.Normal;

        SpawnPlatform(c, kind, x, y + dy, w, config.platformThickness, 0f, 0f);
    }

    private void SpawnPlatform(Chunk c, Kind kind, float x, float topY, float w, float thickness, float amplitude, float omega)
    {
        ClimbPlatform p = platformPool.Count > 0 ? platformPool.Pop() : CreatePlatform();
        p.Spawn(config, kind, x, topY, w, thickness, config.depth, amplitude, omega, Next01(), ColorFor(kind), MaterialFor(kind));
        c.platforms.Add(p);
    }

    private Material MaterialFor(Kind kind)
    {
        int i = (int)kind;
        return kindMaterials != null && i < kindMaterials.Length ? kindMaterials[i] : null;
    }

    // ------------------------------------------------------------------ Reglas de alcance

    private float Difficulty(float y)
    {
        return Mathf.Clamp01((y - startTopY) / Mathf.Max(1f, config.difficultyMaxHeight));
    }

    private float MaxVerticalGap => config.jumpHeight * config.verticalReachSafety;

    private float RollGap(float t)
    {
        float max = MaxVerticalGap;
        float min = Mathf.Min(config.minVerticalGap, max);
        float u = Mathf.Clamp01(t * 0.6f + Next01() * 0.5f);
        return Mathf.Lerp(min, max, u);
    }

    /// <summary>
    /// Distancia horizontal máxima (borde a borde) alcanzable al subir 'gap' con un salto completo,
    /// más una parte del dash a medida que sube la dificultad.
    /// </summary>
    private float MaxEdgeGap(float gap, float t)
    {
        float g = config.gravity;
        float h = config.jumpHeight;
        float v = Mathf.Sqrt(2f * g * h);
        float airTime = v / g + Mathf.Sqrt(2f * Mathf.Max(0f, h - gap) / (g * config.fallGravityMultiplier));
        float jumpReach = config.maxMoveSpeed * airTime * config.horizontalReachSafety;
        float dashBonus = config.dashSpeed * config.dashDuration * config.dashReachUse * t;
        return jumpReach + dashBonus;
    }

    private float ChooseX(float w, float gap, float t)
    {
        return ChooseXWithin(w, MaxEdgeGap(gap, t));
    }

    /// <summary>X de la nueva plataforma con una distancia horizontal máxima (borde a borde) respecto a la anterior.</summary>
    private float ChooseXWithin(float w, float maxEdge)
    {
        float half = InnerHalfWidth;
        float lo = -half + w * 0.5f;
        float hi = half - w * 0.5f;

        float maxDx = maxEdge + (chainWidth + w) * 0.5f;
        float minX = Mathf.Max(lo, chainX - maxDx);
        float maxX = Mathf.Min(hi, chainX + maxDx);
        if (maxX < minX) return Mathf.Clamp(chainX, lo, hi);

        float x = Mathf.Lerp(minX, maxX, Next01());
        for (int i = 0; i < 6 && Mathf.Abs(x - chainX) < config.minHorizontalShift; i++)
            x = Mathf.Lerp(minX, maxX, Next01());
        return x;
    }

    private float DashGapChance(float y)
    {
        float h = y - startTopY - config.dashGapStartHeight;
        return Mathf.Clamp01(h / Mathf.Max(1f, config.dashGapRampHeight)) * config.dashGapMaxChance;
    }

    private float RollWidth(float t)
    {
        float min = Mathf.Lerp(config.platformWidthMin, config.platformWidthMinHard, t);
        float max = Mathf.Lerp(config.platformWidthMax, config.platformWidthMaxHard, t);
        return Mathf.Lerp(min, Mathf.Max(min, max), Next01());
    }

    private float Chance(float y, float startHeight, float maxChance)
    {
        float h = y - startTopY;
        return Mathf.Clamp01((h - startHeight) / Mathf.Max(1f, config.chanceRampHeight)) * maxChance;
    }

    /// <summary>Elige el tipo. Nunca encadena dos plataformas arriesgadas seguidas en el camino principal.</summary>
    private Kind RollKind(float y, Kind previous)
    {
        bool prevRisky = previous == Kind.Falling || previous == Kind.Timed;
        float pMoving = Chance(y, config.movingStartHeight, config.movingMaxChance);
        float pFalling = prevRisky ? 0f : Chance(y, config.fallingStartHeight, config.fallingMaxChance);
        float pTimed = prevRisky ? 0f : Chance(y, config.timedStartHeight, config.timedMaxChance);

        float r = Next01();
        if (r < pMoving) return Kind.Moving;
        r -= pMoving;
        if (r < pFalling) return Kind.Falling;
        r -= pFalling;
        if (r < pTimed) return Kind.Timed;
        return Kind.Normal;
    }

    private Color ColorFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Falling: return config.fallingPlatformColor;
            case Kind.Timed: return config.timedPlatformColor;
            case Kind.Moving: return config.movingPlatformColor;
            case Kind.Safe: return config.safePlatformColor;
            default: return config.normalPlatformColor;
        }
    }

    private float Next01()
    {
        return (float)rng.NextDouble();
    }

    // ------------------------------------------------------------------ Pools y paredes

    private ClimbPlatform CreatePlatform()
    {
        GameObject go = Instantiate(platformPrefab, platformsRoot);
        go.SetActive(false);
        return go.GetComponent<ClimbPlatform>();
    }

    /// <summary>Recicla el segmento de pared más bajo hacia arriba (o crea uno si hace falta cubrir más).</summary>
    private void RecycleWalls(float recycleY, float neededTopY)
    {
        float h = config.wallSegmentHeight;
        int guard = 0;
        while (wallTopY < neededTopY && guard++ < 40)
        {
            WallPair pair;
            float bottomTop = wallTopY - walls.Count * h;
            if (walls.Count > 0 && bottomTop < recycleY)
            {
                pair = walls.Dequeue();
            }
            else
            {
                pair = new WallPair
                {
                    left = Instantiate(wallSegmentPrefab, wallsRoot).transform,
                    right = Instantiate(wallSegmentPrefab, wallsRoot).transform
                };
            }
            PlaceWallPair(pair, wallTopY + h * 0.5f);
            wallTopY += h;
            walls.Enqueue(pair);
        }
    }

    private void PlaceWallPair(WallPair pair, float centerY)
    {
        Vector3 scale = new Vector3(config.wallThickness, config.wallSegmentHeight, config.depth);
        pair.left.SetPositionAndRotation(new Vector3(-config.wallX, centerY, 0f), Quaternion.identity);
        pair.right.SetPositionAndRotation(new Vector3(config.wallX, centerY, 0f), Quaternion.identity);
        pair.left.localScale = scale;
        pair.right.localScale = scale;
    }
}
