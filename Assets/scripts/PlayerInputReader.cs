using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Lee teclado/gamepad (acciones Player/Move, Jump, Dash) y controles táctiles
/// (mitad izquierda/derecha = mover, tap = saltar, swipe = dash) y los expone de forma unificada.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerInputReader : MonoBehaviour
{
    private class TouchTrack
    {
        public Vector2 startPos;
        public float startTime;
        public bool swiped;
        public bool ignored;
    }

    [SerializeField] private GameConfig config;
    [SerializeField] private InputActionAsset actions;

    private InputActionMap map;
    private InputAction moveAction, jumpAction, dashAction;
    /// <summary>Zonas de pantalla (coordenadas del Input System, origen abajo-izquierda) donde los toques no controlan al jugador (botones del HUD).</summary>
    public static readonly List<Rect> BlockedScreenRects = new List<Rect>();

    private readonly Dictionary<int, TouchTrack> tracks = new Dictionary<int, TouchTrack>();
    private float touchJumpHoldUntil;
    private float touchMoveX;

    /// <summary>Entrada de movimiento (x: lateral, y: vertical para dash en 8 direcciones).</summary>
    public Vector2 Move { get; private set; }
    public bool JumpHeld { get; private set; }
    /// <summary>True solo durante el frame en que se pulsó saltar.</summary>
    public bool JumpPressed { get; private set; }
    /// <summary>True solo durante el frame en que se pidió dash.</summary>
    public bool DashPressed { get; private set; }
    /// <summary>Dirección del dash pedida por swipe (cero = usar Move / dirección horizontal).</summary>
    public Vector2 DashDirection { get; private set; }

    private void OnEnable()
    {
        map = actions.FindActionMap("Player", true);
        moveAction = map.FindAction("Move", true);
        jumpAction = map.FindAction("Jump", true);
        dashAction = map.FindAction("Dash", true);
        map.Enable();

        EnhancedTouchSupport.Enable();
        if (config.simulateTouchWithMouse) TouchSimulation.Enable();
    }

    private void OnDisable()
    {
        if (map != null) map.Disable();
        if (config.simulateTouchWithMouse) TouchSimulation.Disable();
        EnhancedTouchSupport.Disable();
        tracks.Clear();
    }

    private void Update()
    {
        JumpPressed = jumpAction.WasPressedThisFrame();
        DashPressed = dashAction.WasPressedThisFrame();
        DashDirection = Vector2.zero;

        touchMoveX = 0f;
        ProcessTouches();

        Vector2 move = moveAction.ReadValue<Vector2>();
        if (Mathf.Abs(move.x) < 0.01f) move.x = touchMoveX;
        Move = move;

        JumpHeld = jumpAction.IsPressed() || Time.unscaledTime < touchJumpHoldUntil;
    }

    private void ProcessTouches()
    {
        float now = Time.unscaledTime;
        float minSwipePixels = Screen.width * config.swipeMinDistance;
        float halfWidth = Screen.width * 0.5f;

        foreach (Touch t in Touch.activeTouches)
        {
            int id = t.touchId;
            if (!tracks.TryGetValue(id, out TouchTrack tr))
            {
                tr = new TouchTrack { startPos = t.screenPosition, startTime = now, ignored = IsBlocked(t.screenPosition) };
                tracks[id] = tr;
            }

            bool ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            if (tr.ignored)
            {
                if (ended) tracks.Remove(id);
                continue;
            }
            Vector2 delta = t.screenPosition - tr.startPos;
            float elapsed = now - tr.startTime;

            // Swipe -> dash
            if (!tr.swiped && t.phase != TouchPhase.Canceled && delta.magnitude >= minSwipePixels && elapsed <= config.swipeMaxTime)
            {
                tr.swiped = true;
                DashPressed = true;
                DashDirection = delta.normalized;
            }

            if (ended)
            {
                // Tap -> salto
                if (!tr.swiped && t.phase == TouchPhase.Ended && elapsed <= config.touchTapMaxTime && delta.magnitude < minSwipePixels)
                {
                    JumpPressed = true;
                    touchJumpHoldUntil = now + config.touchJumpHoldTime;
                }
                tracks.Remove(id);
                continue;
            }

            // Mitad izquierda/derecha -> mover
            if (!tr.swiped && elapsed >= config.touchMoveDelay)
                touchMoveX += t.screenPosition.x < halfWidth ? -1f : 1f;
        }

        touchMoveX = Mathf.Clamp(touchMoveX, -1f, 1f);
    }


private static bool IsBlocked(Vector2 screenPos)
    {
        for (int i = 0; i < BlockedScreenRects.Count; i++)
            if (BlockedScreenRects[i].Contains(screenPos)) return true;
        return false;
    }
}
