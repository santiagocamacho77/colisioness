using UnityEngine;

/// <summary>
/// HUD con IMGUI (sin dependencias de UI): pantalla de inicio (Dasher Square), altura y récord, botón de pausa,
/// aviso e indicador de clima, menú de pausa, pantalla de puntuación y transiciones de pantalla.
/// Escala con la resolución; los toques sobre el botón de pausa no llegan al jugador.
/// </summary>
public class GameHud : MonoBehaviour
{
    private static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 1f);
    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.14f, 0.62f);
    private static readonly Color AccentColor = new Color(0.33f, 0.62f, 1f, 0.95f);
    private static readonly Color MutedButton = new Color(0.3f, 0.32f, 0.42f, 0.95f);
    private static readonly Color HeroGreen = new Color(0.45f, 0.82f, 0.27f, 1f);
    private static readonly Color TitleAccent = new Color(0.45f, 0.95f, 0.6f, 1f);

    private const string GameTitleTop = "DASHER";
    private const string GameTitleBottom = "SQUARE";
    private const string Credits = "Hecho por Santiago Camacho";

    private GameManager game;
    private WeatherEventManager weather;
    private Texture2D round;
    private GUIStyle panel, button, buttonSmall, title, logo, big, huge, mid, small, tiny, smallLeft, bigLeft, banner;
    private float styleUnit = -1f;

    private GameManager.RunState lastState = GameManager.RunState.Menu;
    private float menuCloseTime = -10f;

    private static float Unit { get { return Mathf.Max(0.5f, Mathf.Min(Screen.width, Screen.height) / 540f); } }

    private void Start()
    {
        game = FindAnyObjectByType<GameManager>();
        weather = FindAnyObjectByType<WeatherEventManager>();
        round = FxUtil.RoundedRect(64, 18);
    }

    private static Rect PauseRect(float u)
    {
        return new Rect(Screen.width - 16f * u - 70f * u, 16f * u, 70f * u, 70f * u);
    }

    private void Update()
    {
        if (game == null) return;

        // Al salir del menú, el título se desvanece en vez de cortarse
        if (lastState == GameManager.RunState.Menu && game.State != GameManager.RunState.Menu) menuCloseTime = Time.unscaledTime;
        lastState = game.State;

        // Los toques sobre el botón de pausa no deben mover/saltar al jugador
        PlayerInputReader.BlockedScreenRects.Clear();
        if (game.State != GameManager.RunState.Playing || game.Paused) return;
        float u = Unit;
        Rect r = PauseRect(u);
        float pad = 12f * u;
        // GUI (origen arriba-izquierda) -> pantalla del Input System (origen abajo-izquierda)
        PlayerInputReader.BlockedScreenRects.Add(new Rect(r.x - pad, Screen.height - r.yMax - pad, r.width + pad * 2f, r.height + pad * 2f));
    }

    // ------------------------------------------------------------------ Estilos

    private GUIStyle Label(float size, TextAnchor anchor)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.fontSize = Mathf.RoundToInt(size);
        s.fontStyle = FontStyle.Bold;
        s.alignment = anchor;
        s.wordWrap = false;
        s.clipping = TextClipping.Overflow;
        s.normal.textColor = Color.white;
        return s;
    }

    private void EnsureStyles(float u)
    {
        if (panel != null && Mathf.Approximately(styleUnit, u)) return;
        styleUnit = u;
        panel = new GUIStyle();
        panel.normal.background = round;
        panel.border = new RectOffset(20, 20, 20, 20);

        button = Label(30f * u, TextAnchor.MiddleCenter);
        button.normal.background = null;
        button.hover.textColor = Color.white;
        button.active.textColor = Color.white;
        buttonSmall = new GUIStyle(button);
        buttonSmall.fontSize = Mathf.RoundToInt(22f * u);

        logo = Label(88f * u, TextAnchor.MiddleCenter);
        title = Label(46f * u, TextAnchor.MiddleCenter);
        huge = Label(96f * u, TextAnchor.MiddleCenter);
        big = Label(40f * u, TextAnchor.MiddleCenter);
        mid = Label(28f * u, TextAnchor.MiddleCenter);
        small = Label(20f * u, TextAnchor.MiddleCenter);
        tiny = Label(16f * u, TextAnchor.MiddleCenter);
        smallLeft = Label(18f * u, TextAnchor.MiddleLeft);
        bigLeft = Label(40f * u, TextAnchor.MiddleLeft);
        banner = Label(40f * u, TextAnchor.MiddleCenter);
    }

    private void Panel(Rect r, Color c)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color old = GUI.color;
        GUI.color = c;
        panel.Draw(r, false, false, false, false);
        GUI.color = old;
    }

    private void Text(Rect r, string text, GUIStyle style, Color color)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f * color.a);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    private bool Button(Rect r, string text, Color c, GUIStyle style = null)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        Panel(r, hover ? Color.Lerp(c, Color.white, 0.18f) : c);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, c.a);
        bool clicked = GUI.Button(r, text, style ?? button);
        GUI.color = old;
        return clicked;
    }

    private void Fill(Color c)
    {
        if (c.a <= 0.001f) return;
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static Color WeatherColor(WeatherType t)
    {
        switch (t)
        {
            case WeatherType.Rain: return new Color(0.2f, 0.45f, 0.9f, 0.9f);
            case WeatherType.Wind: return new Color(0.2f, 0.66f, 0.6f, 0.9f);
            case WeatherType.Fog: return new Color(0.5f, 0.56f, 0.66f, 0.9f);
            default: return PanelColor;
        }
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a *= a;
        return c;
    }

    // ------------------------------------------------------------------ Dibujo

    private void OnGUI()
    {
        if (game == null) return;
        float u = Unit;
        EnsureStyles(u);
        float now = Time.unscaledTime;
        GameManager.RunState state = game.State;
        bool playing = state == GameManager.RunState.Playing;

        // ---- Transición: tras regenerar el mundo la pantalla aparece desde oscuro (oculta el salto del nivel) ----
        float sinceReset = now - game.LastResetRealtime;
        Fill(new Color(0.02f, 0.02f, 0.06f, 0.95f * (1f - Mathf.Clamp01(sinceReset / 0.45f))));

        if (state == GameManager.RunState.Menu)
        {
            DrawMenu(u, now, 1f);
            return;
        }

        // ---- Altura y récord ----
        float hudIn = Mathf.Clamp01((now - menuCloseTime) / 0.35f);
        Rect hr = new Rect(16f * u, 16f * u - (1f - hudIn) * 30f * u, 230f * u, 92f * u);
        Panel(hr, WithAlpha(PanelColor, hudIn));
        Text(new Rect(hr.x + 18f * u, hr.y + 6f * u, hr.width, 52f * u), Mathf.FloorToInt(game.Height) + " m", bigLeft, new Color(1f, 1f, 1f, hudIn));
        float best = Mathf.Max(game.BestHeight, game.Height);
        Text(new Rect(hr.x + 18f * u, hr.y + 56f * u, hr.width, 28f * u), "RÉCORD  " + Mathf.FloorToInt(best) + " m", smallLeft, new Color(1f, 1f, 1f, 0.75f * hudIn));

        if (playing && game.PreviousBest >= 1f && game.Height > game.PreviousBest)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(now * 8f);
            Rect nr = new Rect(hr.x, hr.yMax + 8f * u, hr.width, 38f * u);
            Panel(nr, new Color(Gold.r, Gold.g, Gold.b, 0.9f * pulse));
            Text(nr, "¡NUEVO RÉCORD!", small, new Color(0.15f, 0.1f, 0f, 1f));
        }

        // ---- Botón de pausa ----
        if (playing && !game.Paused)
        {
            Rect pr = PauseRect(u);
            pr.y -= (1f - hudIn) * 30f * u;
            if (Button(pr, "", WithAlpha(PanelColor, hudIn))) game.SetPaused(true);
            float bw = 9f * u, bh = 30f * u, gap = 9f * u;
            float bx = pr.center.x - gap * 0.5f - bw, by = pr.center.y - bh * 0.5f;
            Panel(new Rect(bx, by, bw, bh), new Color(1f, 1f, 1f, hudIn));
            Panel(new Rect(bx + bw + gap, by, bw, bh), new Color(1f, 1f, 1f, hudIn));
        }

        // ---- Clima ----
        if (weather != null && playing)
        {
            if (weather.IsWarning)
            {
                float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(now * 9f));
                Rect br = new Rect(Screen.width * 0.08f, Screen.height * 0.2f, Screen.width * 0.84f, 90f * u);
                Color bc = WeatherColor(weather.Pending);
                bc.a *= pulse;
                Panel(br, bc);
                Text(br, "¡" + WeatherEventManager.NameOf(weather.Pending, weather.WindDirection) + "!", banner, new Color(1f, 1f, 1f, pulse));
            }
            else if (weather.Active != WeatherType.None)
            {
                Rect ar = new Rect(Screen.width - 16f * u - 240f * u, 96f * u, 240f * u, 46f * u);
                Panel(ar, WeatherColor(weather.Active));
                Text(ar, WeatherEventManager.NameOf(weather.Active, weather.WindDirection) + "   " + Mathf.CeilToInt(weather.TimeLeft) + " s", small, Color.white);
            }
        }

        // ---- Destello rojo al caer ----
        if (state == GameManager.RunState.Dying)
            Fill(new Color(1f, 0.15f, 0.1f, 0.35f * (1f - Mathf.Clamp01(game.StateTime / 0.6f))));

        if (game.Paused) DrawPause(u);
        else if (state == GameManager.RunState.GameOver) DrawGameOver(u, now);

        // ---- El título se desvanece al empezar a jugar ----
        float menuFade = 1f - Mathf.Clamp01((now - menuCloseTime) / 0.35f);
        if (menuFade > 0f) DrawMenu(u, now, menuFade);
    }

    private Rect CenterPanel(float u, float height)
    {
        float w = Mathf.Min(Screen.width * 0.86f, 460f * u);
        return new Rect((Screen.width - w) * 0.5f, (Screen.height - height) * 0.5f, w, height);
    }

    // ------------------------------------------------------------------ Menú de inicio

    private void DrawMenu(float u, float now, float a)
    {
        bool interactive = a >= 0.999f && game.State == GameManager.RunState.Menu;
        float W = Screen.width, H = Screen.height;
        Fill(new Color(0.03f, 0.03f, 0.09f, 0.5f * a));

        // Logo: el cuadrado protagonista haciendo dash (líneas de velocidad + leve balanceo)
        float s = 96f * u;
        float cx = W * 0.5f + Mathf.Sin(now * 1.6f) * 18f * u;
        float cy = H * 0.17f + Mathf.Sin(now * 2.4f) * 8f * u;
        for (int i = 1; i <= 3; i++)
        {
            float lw = (64f - i * 10f) * u;
            Rect line = new Rect(cx - s * 0.5f - 18f * u - i * 6f * u - lw, cy - 34f * u + i * 17f * u, lw, 9f * u);
            Panel(line, new Color(0.55f, 0.95f, 1f, (0.75f - i * 0.18f) * a));
        }
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Sin(now * 2.4f) * 7f - 4f, new Vector2(cx, cy));
        Rect sq = new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s);
        Panel(new Rect(sq.x + 5f * u, sq.y + 7f * u, s, s), new Color(0f, 0f, 0f, 0.3f * a));
        Panel(sq, WithAlpha(HeroGreen, a));
        Panel(new Rect(sq.x + s * 0.14f, sq.y + s * 0.36f, s * 0.72f, s * 0.2f), new Color(1f, 1f, 1f, a));
        Panel(new Rect(sq.x + s * 0.26f, sq.y + s * 0.4f, s * 0.14f, s * 0.12f), new Color(0.08f, 0.08f, 0.1f, a));
        Panel(new Rect(sq.x + s * 0.6f, sq.y + s * 0.4f, s * 0.14f, s * 0.12f), new Color(0.08f, 0.08f, 0.1f, a));
        Panel(new Rect(sq.x + s * 0.34f, sq.y + s * 0.68f, s * 0.32f, s * 0.08f), new Color(0.08f, 0.08f, 0.1f, 0.8f * a));
        GUI.matrix = m;

        // Título
        float ty = H * 0.27f;
        float pop = 1f + 0.03f * Mathf.Sin(now * 3f);
        Matrix4x4 m2 = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(W * 0.5f, ty + 90f * u));
        Text(new Rect(0f, ty, W, 96f * u), GameTitleTop, logo, new Color(1f, 1f, 1f, a));
        Text(new Rect(0f, ty + 84f * u, W, 96f * u), GameTitleBottom, logo, WithAlpha(TitleAccent, a));
        GUI.matrix = m2;
        Text(new Rect(0f, ty + 186f * u, W, 30f * u), Credits, small, new Color(1f, 1f, 1f, 0.72f * a));

        // Récord
        if (game.BestHeight >= 1f)
            Text(new Rect(0f, H * 0.53f, W, 36f * u), "RÉCORD  " + Mathf.FloorToInt(game.BestHeight) + " m", mid, WithAlpha(Gold, 0.95f * a));

        // Botón jugar
        float bw = Mathf.Min(W * 0.72f, 380f * u);
        Rect play = new Rect((W - bw) * 0.5f, H * 0.6f, bw, 88f * u);
        float breathe = 0.9f + 0.1f * Mathf.Sin(now * 4f);
        Color pc = AccentColor;
        pc.a = a;
        if (Button(play, "JUGAR", Color.Lerp(pc * 0.85f, pc, breathe)) && interactive)
        {
            game.StartRun();
            return;
        }
        Text(new Rect(0f, play.yMax + 14f * u, W, 28f * u), "Toca la pantalla o pulsa Espacio", small, new Color(1f, 1f, 1f, (0.4f + 0.2f * Mathf.Sin(now * 3f)) * a));

        // Controles
        Text(new Rect(0f, H - 70f * u, W, 24f * u), "A / D  mover   ·   ESPACIO  saltar   ·   SHIFT  dash", tiny, new Color(1f, 1f, 1f, 0.5f * a));

        // Tocar en cualquier parte también empieza
        Event e = Event.current;
        if (interactive && e.type == EventType.MouseDown && !play.Contains(e.mousePosition) && game.StateTime > 0.3f)
        {
            e.Use();
            game.StartRun();
        }
    }

    // ------------------------------------------------------------------ Pausa y puntuación

    private void DrawPause(float u)
    {
        Fill(new Color(0.02f, 0.02f, 0.06f, 0.55f));
        Rect p = CenterPanel(u, 410f * u);
        Panel(p, new Color(0.08f, 0.09f, 0.18f, 0.94f));
        Text(new Rect(p.x, p.y + 24f * u, p.width, 60f * u), "PAUSA", title, Color.white);

        Rect b1 = new Rect(p.x + 36f * u, p.y + 110f * u, p.width - 72f * u, 70f * u);
        if (Button(b1, "CONTINUAR", AccentColor)) game.SetPaused(false);
        Rect b2 = new Rect(b1.x, b1.yMax + 16f * u, b1.width, 64f * u);
        if (Button(b2, "REINICIAR", MutedButton)) game.Restart();
        Rect b3 = new Rect(b1.x, b2.yMax + 16f * u, b1.width, 56f * u);
        if (Button(b3, "MENÚ", new Color(0.22f, 0.24f, 0.32f, 0.95f), buttonSmall)) game.GoToMenu();
        Text(new Rect(p.x, p.yMax - 40f * u, p.width, 30f * u), "Esc / P para continuar", small, new Color(1f, 1f, 1f, 0.5f));
    }

    private void DrawGameOver(float u, float now)
    {
        float appear = Mathf.Clamp01(game.StateTime / 0.25f);
        Fill(new Color(0.02f, 0.02f, 0.06f, 0.6f * appear));

        float h = 540f * u;
        Rect p = CenterPanel(u, h);
        p.y += (1f - appear) * 40f * u;
        Panel(p, new Color(0.08f, 0.09f, 0.18f, 0.95f * appear));

        float y = p.y + 22f * u;
        Text(new Rect(p.x, y, p.width, 56f * u), "¡CAÍSTE!", title, new Color(1f, 0.55f, 0.45f, appear));
        y += 70f * u;
        Text(new Rect(p.x, y, p.width, 28f * u), "ALTURA", small, new Color(1f, 1f, 1f, 0.6f * appear));
        y += 26f * u;
        Text(new Rect(p.x, y, p.width, 104f * u), Mathf.FloorToInt(game.Height) + " m", huge, new Color(1f, 1f, 1f, appear));
        y += 112f * u;

        if (game.IsNewRecord)
        {
            float pulse = 0.8f + 0.2f * Mathf.Sin(now * 7f);
            Text(new Rect(p.x, y, p.width, 40f * u), "¡NUEVO RÉCORD!", big, new Color(Gold.r, Gold.g, Gold.b, appear * pulse));
        }
        else
        {
            Text(new Rect(p.x, y, p.width, 40f * u), "RÉCORD  " + Mathf.FloorToInt(game.BestHeight) + " m", mid, new Color(Gold.r, Gold.g, Gold.b, 0.9f * appear));
        }
        y += 60f * u;

        bool ready = game.CanRetry;
        Rect b = new Rect(p.x + 36f * u, y, p.width - 72f * u, 76f * u);
        if (Button(b, "REINTENTAR", ready ? AccentColor : new Color(0.3f, 0.32f, 0.42f, 0.8f)) && ready)
        {
            game.Restart();
            return;
        }
        Rect bm = new Rect(b.x, b.yMax + 14f * u, b.width, 56f * u);
        if (Button(bm, "MENÚ", new Color(0.22f, 0.24f, 0.32f, 0.95f * appear), buttonSmall) && ready)
        {
            game.GoToMenu();
            return;
        }
        Text(new Rect(p.x, bm.yMax + 10f * u, p.width, 30f * u), "Toca la pantalla o pulsa Espacio", small, new Color(1f, 1f, 1f, 0.5f * appear));

        // Tocar fuera de los botones también reintenta
        Event e = Event.current;
        if (ready && e.type == EventType.MouseDown && !b.Contains(e.mousePosition) && !bm.Contains(e.mousePosition))
        {
            e.Use();
            game.Restart();
        }
    }
}
