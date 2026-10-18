using UnityEngine;

/// <summary>
/// Única fuente de Time.timeScale: combina pausa, cámara lenta y freeze-frame sin que se pisen.
/// También escala fixedDeltaTime en cámara lenta para que la física siga fluida.
/// </summary>
public static class TimeController
{
    private static bool paused;
    private static float slowScale = 1f;
    private static float freezeUntil;
    private static float baseFixedDelta = -1f;

    public static bool Paused { get { return paused; } }
    public static bool Frozen { get { return Time.unscaledTime < freezeUntil; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        paused = false;
        slowScale = 1f;
        freezeUntil = 0f;
        baseFixedDelta = -1f;
    }

    public static void SetPaused(bool value)
    {
        paused = value;
        Apply();
    }

    /// <summary>1 = normal, 0 = detenido.</summary>
    public static void SetSlowMotion(float scale)
    {
        slowScale = Mathf.Clamp01(scale);
        Apply();
    }

    /// <summary>Congela el tiempo unos instantes (impacto). Se ignora en pausa o cámara lenta.</summary>
    public static void Freeze(float seconds)
    {
        if (paused || slowScale < 1f || seconds <= 0f) return;
        freezeUntil = Mathf.Max(freezeUntil, Time.unscaledTime + seconds);
        Apply();
    }

    /// <summary>Llamar cada frame (termina el freeze a tiempo).</summary>
    public static void Tick()
    {
        Apply();
    }

    public static void ResetAll()
    {
        paused = false;
        slowScale = 1f;
        freezeUntil = 0f;
        Apply();
    }

    private static void Apply()
    {
        if (baseFixedDelta < 0f) baseFixedDelta = Time.fixedDeltaTime;
        float s = paused ? 0f : (Time.unscaledTime < freezeUntil ? 0f : slowScale);
        Time.timeScale = s;
        Time.fixedDeltaTime = (s > 0f && s < 1f) ? baseFixedDelta * s : baseFixedDelta;
    }
}
