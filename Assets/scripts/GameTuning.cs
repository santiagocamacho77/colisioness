using UnityEngine;

/// <summary>
/// Único punto que escribe valores de ejecución en GameConfig. Guarda los valores base al empezar,
/// aplica sobre ellos la dificultad (velocidad lateral y de dash) y los modificadores temporales del clima,
/// y los restaura al desactivarse (salir de Play). Así los eventos no dejan el asset modificado.
/// </summary>
public class GameTuning : MonoBehaviour
{
    /// <summary>Multiplicadores (y viento) que aplica el clima activo. 'None' = sin efecto.</summary>
    public struct WeatherMods
    {
        public float groundAccelMult;
        public float groundDecelMult;
        public float wallStickMult;
        public float wallSlideSpeedMult;
        public float windSpeed;

        public static WeatherMods None
        {
            get
            {
                return new WeatherMods
                {
                    groundAccelMult = 1f, groundDecelMult = 1f, wallStickMult = 1f, wallSlideSpeedMult = 1f, windSpeed = 0f
                };
            }
        }

        /// <summary>Mezcla de 'sin efecto' (k = 0) a 'target' (k = 1).</summary>
        public static WeatherMods Blend(WeatherMods target, float k)
        {
            WeatherMods n = None;
            return new WeatherMods
            {
                groundAccelMult = Mathf.Lerp(n.groundAccelMult, target.groundAccelMult, k),
                groundDecelMult = Mathf.Lerp(n.groundDecelMult, target.groundDecelMult, k),
                wallStickMult = Mathf.Lerp(n.wallStickMult, target.wallStickMult, k),
                wallSlideSpeedMult = Mathf.Lerp(n.wallSlideSpeedMult, target.wallSlideSpeedMult, k),
                windSpeed = Mathf.Lerp(n.windSpeed, target.windSpeed, k)
            };
        }
    }

    [SerializeField] private GameConfig config;

    private bool taken;
    private float baseMove, baseDash, baseGroundAcc, baseGroundDec, baseStick, baseSlideInit, baseSlideAcc, baseSlideMax, baseWind;
    private float difficulty;
    private WeatherMods weather = WeatherMods.None;

    public float Difficulty01 { get { return difficulty; } }

    private void Awake()
    {
        if (config == null) { enabled = false; return; }
        baseMove = config.maxMoveSpeed;
        baseDash = config.dashSpeed;
        baseGroundAcc = config.groundAcceleration;
        baseGroundDec = config.groundDeceleration;
        baseStick = config.wallStickTime;
        baseSlideInit = config.wallSlideInitialSpeed;
        baseSlideAcc = config.wallSlideAcceleration;
        baseSlideMax = config.wallSlideMaxSpeed;
        baseWind = config.windSpeed;
        taken = true;
        Apply();
    }

    private void OnDisable()
    {
        Restore();
    }

    /// <summary>Dificultad 0..1: sube ligeramente la velocidad lateral y la del dash.</summary>
    public void SetDifficulty(float t)
    {
        t = Mathf.Clamp01(t);
        if (Mathf.Approximately(t, difficulty)) return;
        difficulty = t;
        Apply();
    }

    public void SetWeather(WeatherMods mods)
    {
        weather = mods;
        Apply();
    }

    private void Apply()
    {
        if (!taken) return;
        config.maxMoveSpeed = baseMove * (1f + difficulty * config.difficultyMoveSpeedBonus);
        config.dashSpeed = baseDash * (1f + difficulty * config.difficultyDashSpeedBonus);
        config.groundAcceleration = baseGroundAcc * weather.groundAccelMult;
        config.groundDeceleration = baseGroundDec * weather.groundDecelMult;
        config.wallStickTime = baseStick * weather.wallStickMult;
        config.wallSlideInitialSpeed = baseSlideInit * weather.wallSlideSpeedMult;
        config.wallSlideAcceleration = baseSlideAcc * weather.wallSlideSpeedMult;
        config.wallSlideMaxSpeed = baseSlideMax * weather.wallSlideSpeedMult;
        config.windSpeed = baseWind + weather.windSpeed;
    }

    private void Restore()
    {
        if (!taken) return;
        config.maxMoveSpeed = baseMove;
        config.dashSpeed = baseDash;
        config.groundAcceleration = baseGroundAcc;
        config.groundDeceleration = baseGroundDec;
        config.wallStickTime = baseStick;
        config.wallSlideInitialSpeed = baseSlideInit;
        config.wallSlideAcceleration = baseSlideAcc;
        config.wallSlideMaxSpeed = baseSlideMax;
        config.windSpeed = baseWind;
        taken = false;
    }
}
