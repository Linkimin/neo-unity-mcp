// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using Neo.UnityMcp.Registry;
using UnityEditor;
#if NEO_INPUTSYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Neo.UnityMcp.Tools
{
    // Input simulation. Decision E: detect the active backend and simulate via the New Input
    // System (the only editor-injectable backend; legacy Input Manager has no clean editor
    // injection — same limitation as Funplay). Legacy-only projects get a graceful UNSUPPORTED.
    [NeoToolProvider("Input")]
    internal static class InputSimTools
    {
        internal enum InputBackend
        {
            None,
            Legacy,
            NewInputSystem
        }

#if ENABLE_INPUT_SYSTEM
        private const bool NewInputActive = true;
#else
        private const bool NewInputActive = false;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        private const bool LegacyInputActive = true;
#else
        private const bool LegacyInputActive = false;
#endif

        // Pure, testable router. "Both" active -> prefer the New Input System.
        public static InputBackend SelectBackend(bool newActive, bool legacyActive)
        {
            if (newActive) return InputBackend.NewInputSystem;
            if (legacyActive) return InputBackend.Legacy;
            return InputBackend.None;
        }

        public static InputBackend ActiveBackend => SelectBackend(NewInputActive, LegacyInputActive);

        [NeoTool("get_input_backend", "Active input backend (New Input System / Legacy / None) and what is installed/active.")]
        [ReadOnlyTool]
        public static object GetInputBackend()
        {
            return Response.Success("Input backend.", new
            {
                backend = ActiveBackend.ToString(),
                newInputActive = NewInputActive,
                legacyInputActive = LegacyInputActive
            });
        }

        [NeoTool("simulate_mouse_click", "Simulate a mouse click at a screen position (New Input System, play mode).")]
        [ReadOnlyTool]
        public static object SimulateMouseClick(
            [ToolParam("X coordinate in pixels.")] float x,
            [ToolParam("Y coordinate in pixels.")] float y,
            [ToolParam("Button: left, right, middle (default left).", Required = false)] string button = "left")
        {
            if (ActiveBackend != InputBackend.NewInputSystem)
                return Response.Error("INPUT_BACKEND_UNSUPPORTED",
                    new { backend = ActiveBackend.ToString(), hint = "Set Active Input Handling to Input System (or Both)." });
            if (!EditorApplication.isPlaying)
                return Response.Error("PLAY_MODE_REQUIRED", new { hint = "Enter play mode to simulate input." });

#if NEO_INPUTSYSTEM
            return NewInputMouseClick(x, y, button);
#else
            return Response.Error("INPUT_SYSTEM_NOT_INSTALLED");
#endif
        }

        [NeoTool("simulate_key_press", "Simulate a key tap (press+release) (New Input System, play mode).")]
        [ReadOnlyTool]
        public static object SimulateKeyPress(
            [ToolParam("Key name, e.g. W, Space, LeftShift, E, 1.")] string key)
        {
            if (ActiveBackend != InputBackend.NewInputSystem)
                return Response.Error("INPUT_BACKEND_UNSUPPORTED",
                    new { backend = ActiveBackend.ToString(), hint = "Set Active Input Handling to Input System (or Both)." });
            if (!EditorApplication.isPlaying)
                return Response.Error("PLAY_MODE_REQUIRED", new { hint = "Enter play mode to simulate input." });

#if NEO_INPUTSYSTEM
            return NewInputKeyPress(key);
#else
            return Response.Error("INPUT_SYSTEM_NOT_INSTALLED");
#endif
        }

#if NEO_INPUTSYSTEM
        private static object NewInputMouseClick(float x, float y, string button)
        {
            var mouse = EnsureMouse();
            if (mouse == null)
                return Response.Error("NO_MOUSE_DEVICE");

            var pos = new Vector2(x, y);
            var btn = GetMouseButton(mouse, button);

            QueueStateEvent(mouse, e => { mouse.position.WriteValueIntoEvent(pos, e); btn.WriteValueIntoEvent(1f, e); });
            QueueStateEvent(mouse, e => { mouse.position.WriteValueIntoEvent(pos, e); btn.WriteValueIntoEvent(0f, e); });

            return Response.Success("Simulated mouse click.", new { backend = "NewInputSystem", x, y, button });
        }

        private static object NewInputKeyPress(string key)
        {
            var keyboard = EnsureKeyboard();
            if (keyboard == null)
                return Response.Error("NO_KEYBOARD_DEVICE");

            var control = FindKey(keyboard, key);
            if (control == null)
                return Response.Error("INVALID_KEY", new { key });

            QueueStateEvent(keyboard, e => control.WriteValueIntoEvent(1f, e));
            QueueStateEvent(keyboard, e => control.WriteValueIntoEvent(0f, e));

            return Response.Success("Simulated key press.", new { backend = "NewInputSystem", key });
        }

        private static void QueueStateEvent(InputDevice device, Action<InputEventPtr> writeState)
        {
            if (device == null || writeState == null)
                return;

            using (StateEvent.From(device, out var eventPtr))
            {
                writeState(eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            // Editor-driven simulation needs an explicit update so the state is applied immediately.
            InputSystem.Update();
        }

        private static Mouse EnsureMouse()
        {
            var mouse = Mouse.current ?? InputSystem.GetDevice<Mouse>();
            if (mouse != null)
                return mouse;
            try { return InputSystem.AddDevice<Mouse>(); }
            catch { return null; }
        }

        private static Keyboard EnsureKeyboard()
        {
            var keyboard = Keyboard.current ?? InputSystem.GetDevice<Keyboard>();
            if (keyboard != null)
                return keyboard;
            try { return InputSystem.AddDevice<Keyboard>(); }
            catch { return null; }
        }

        private static ButtonControl GetMouseButton(Mouse mouse, string button)
        {
            switch ((button ?? "left").Trim().ToLowerInvariant())
            {
                case "right": return mouse.rightButton;
                case "middle": return mouse.middleButton;
                default: return mouse.leftButton;
            }
        }

        private static KeyControl FindKey(Keyboard keyboard, string keyName)
        {
            if (keyboard == null || string.IsNullOrWhiteSpace(keyName))
                return null;

            try
            {
                var control = keyboard[keyName.ToLowerInvariant()] as KeyControl;
                if (control != null)
                    return control;
            }
            catch
            {
            }

            return System.Enum.TryParse<Key>(keyName.Trim(), true, out var parsed)
                ? keyboard[parsed]
                : null;
        }
#endif
    }
}
