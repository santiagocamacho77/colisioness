using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Estado de la partida: menú de inicio, auto-scroll tras un tiempo de gracia, altura (puntuación), récord en
/// PlayerPrefs, dificultad, muerte (jugador por debajo del borde inferior de la cámara) con animación, cámara
/// lenta y pantalla de puntuación, pausa, reinicio y vuelta al menú regenerando el nivel sin recargar la escena.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum RunState { Menu, Playing, Dying, GameOver }

    private const string BestKey = "climber_best_height";

    [SerializeField] private GameConfig config;

    public RunState State { get; private set; }
    public bool Paused { get; private set; }
    /// <summary>Altura máxima alcanzada por los pies del jugador (m sobre el inicio). Es la puntuación.</summary>
    public float Height { get; private set; }
    public float RunTime { get; private set; }
    public float Difficulty01 { get; private set; }
    /// <summary>Mejor altura guardada (incluye la partida actual si ya la superó al morir).</summary>
    public float BestHeight { get; private set; }
    /// <summary>Récord que había al empezar esta partida.</summary>
    public float PreviousBest { get; private set; }
    public bool IsNewRecord { get; private set; }
    /// <summary>Segundos (tiempo real) en el estado actual.</summary>
    public float StateTime { get; private set; }
    /// <summary>Momento (tiempo real) en que se regeneró el mundo por última vez; el HUD lo usa para la transición.</summary>
    public float LastResetRealtime { get; private set; }
    public bool CanRetry { get { return State == RunState.GameOver && StateTime >= config.retryInputDelay; } }

    public event Action OnMenu;
    public event Action OnRunStarted;
    public event Action OnPlayerDied;
    public event Action OnGameOver;
    public event Action<bool> OnPauseChanged;

    private PlayerController player;
    private PlayerVisualFX playerFx;
    private BoxCollider playerBox;
    private CameraFollow cameraFollow;
    private LevelGenerator level;
    private GameTuning tuning;
    private WeatherEventManager weather;

    private Vector3 playerStart;
    private float startFeetY;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        playerFx = player.GetComponent<PlayerVisualFX>();
        playerBox = player.GetComponent<BoxCollider>();
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        level = FindAnyObjectByType<LevelGenerator>();
        tuning = FindAnyObjectByType<GameTuning>();
        weather = FindAnyObjectByType<WeatherEventManager>();

        BestHeight = PlayerPrefs.GetFloat(BestKey, 0f);
        PreviousBest = BestHeight;
        playerStart = player.transform.position;
        startFeetY = playerBox.bounds.min.y;

        // El juego arranca en el menú de inicio (la escena ya está recién generada)
        State = RunState.Menu;
        StateTime = 0f;
        player.InputEnabled = false;
        LastResetRealtime = Time.unscaledTime;
        if (tuning != null) tuning.SetDifficulty(0f);
    }

    private void OnDisable()
    {
        TimeController.ResetAll();
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && State == RunState.Playing) SetPaused(true);
    }

    private void Update()
    {
        TimeController.Tick();
        if (player == null) return;

        StateTime += Time.unscaledDeltaTime;
        HandleKeys();
        if (Paused) return;

        switch (State)
        {
            case RunState.Playing:
                RunTime += Time.deltaTime;
                float feet = playerBox.bounds.min.y;
                Height = Mathf.Max(Height, feet - startFeetY);

                if (!cameraFollow.AutoScroll && RunTime >= config.scrollStartDelay)
                    cameraFollow.AutoScroll = true;

                Difficulty01 = Mathf.Clamp01(Mathf.Max(Height, cameraFollow.CameraHeight) / Mathf.Max(1f, config.difficultyMaxHeight));
                if (tuning != null) tuning.SetDifficulty(Difficulty01);

                if (feet < cameraFollow.BottomEdgeY - config.deathMargin) Die();
                break;

            case RunState.Dying:
                if (StateTime >= config.deathSlowMoTime)
                {
                    State = RunState.GameOver;
                    StateTime = 0f;
                    TimeController.SetSlowMotion(0f);
                    if (OnGameOver != null) OnGameOver();
                }
                break;
        }
    }

    private void HandleKeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        bool confirm = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (State == RunState.Menu)
        {
            if (confirm && StateTime > 0.3f) StartRun();
            return;
        }
        if (State == RunState.Playing && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
        {
            SetPaused(!Paused);
            return;
        }
        if (config.restartWithKey && kb.rKey.wasPressedThisFrame)
        {
            Restart();
            return;
        }
        if (CanRetry && confirm) Restart();
    }

    /// <summary>Empieza a jugar desde el menú de inicio.</summary>
    public void StartRun()
    {
        if (State != RunState.Menu) return;
        player.InputEnabled = true;
        player.BlockInput(0.2f);
        BeginRun();
    }

    /// <summary>Vuelve al menú de inicio con el nivel regenerado.</summary>
    public void GoToMenu()
    {
        ResetWorld();
        State = RunState.Menu;
        StateTime = 0f;
        player.InputEnabled = false;
        PreviousBest = BestHeight;
        Height = 0f;
        if (OnMenu != null) OnMenu();
    }

    public void SetPaused(bool value)
    {
        if (State != RunState.Playing) value = false;
        if (Paused == value) return;
        Paused = value;
        TimeController.SetPaused(value);
        player.InputEnabled = !value;
        if (!value) player.BlockInput(0.12f);
        if (OnPauseChanged != null) OnPauseChanged(value);
    }

    private void BeginRun()
    {
        State = RunState.Playing;
        StateTime = 0f;
        Height = 0f;
        RunTime = 0f;
        Difficulty01 = 0f;
        PreviousBest = BestHeight;
        IsNewRecord = false;
        if (tuning != null) tuning.SetDifficulty(0f);
        if (OnRunStarted != null) OnRunStarted();
    }

    private void Die()
    {
        State = RunState.Dying;
        StateTime = 0f;
        cameraFollow.AutoScroll = false;
        player.InputEnabled = false;

        IsNewRecord = Height >= 1f && Height > PreviousBest + 0.01f;
        if (Height > BestHeight)
        {
            BestHeight = Height;
            PlayerPrefs.SetFloat(BestKey, BestHeight);
            PlayerPrefs.Save();
        }

        // Animación de muerte: estalla en el borde inferior de la pantalla, visible para el jugador
        if (playerFx != null)
        {
            float x = Mathf.Clamp(player.transform.position.x, -config.wallX + 1f, config.wallX - 1f);
            playerFx.PlayDeath(new Vector3(x, cameraFollow.BottomEdgeY + 0.6f, 0f));
        }

        TimeController.SetSlowMotion(config.deathSlowMoScale);
        cameraFollow.Shake(0.3f, 0.45f, 28f);
        Debug.Log("[GameManager] Partida terminada. Altura: " + Mathf.FloorToInt(Height) + " m, récord: " + Mathf.FloorToInt(BestHeight) + " m" + (IsNewRecord ? " (¡nuevo récord!)" : ""));
        if (OnPlayerDied != null) OnPlayerDied();
    }

    /// <summary>Regenera el mundo: tiempo, jugador, cámara, nivel (semilla nueva y pools), clima y efectos.</summary>
    private void ResetWorld()
    {
        Paused = false;
        TimeController.ResetAll();
        player.ResetState(playerStart);
        if (playerFx != null) playerFx.ResetVisuals();
        cameraFollow.ResetTo(playerStart.y + cameraFollow.OffsetY);
        level.ResetLevel();
        if (weather != null) weather.ResetWeather();
        DebrisFx.ClearAll();
        LastResetRealtime = Time.unscaledTime;
    }

    /// <summary>Reinicia la partida y empieza a jugar de inmediato.</summary>
    public void Restart()
    {
        ResetWorld();
        player.InputEnabled = true;
        player.BlockInput(0.15f);
        BeginRun();
    }

    /// <summary>Borra el récord guardado (menú contextual del componente).</summary>
    [ContextMenu("Borrar récord")]
    public void ClearRecord()
    {
        BestHeight = 0f;
        PreviousBest = 0f;
        PlayerPrefs.DeleteKey(BestKey);
        PlayerPrefs.Save();
    }
}
