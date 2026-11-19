using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PousoLua
{
    /// <summary>
    /// Centraliza a leitura de entrada do jogador.
    /// Suporta: qualquer Gamepad Bluetooth (Xbox, PlayStation, controles Android/VR genéricos),
    /// Joystick HID genérico e Teclado/Mouse para testes no Editor.
    ///
    /// Mapeamento:
    ///   Analógico esquerdo / WASD ............................ andar na direção do olhar
    ///   Analógico direito (X) / setas ←→ ..................... girar câmera (snap turn)
    ///   Qualquer botão principal (A/B/X/Y/RB/LB/RT/E/F) ...... pegar pedra na mira
    ///   Botão A/B/Y/RB/LB / E / F ............................ soltar pedra segurada
    ///   Gatilho RT/LT / X / Clique esquerdo .................. arremessar pedra (estilo Gravity Gun)
    ///   Analógico direito (Y) / D-pad ↑↓ / Roda do mouse ..... aproximar / afastar pedra (estilo Garry's Mod)
    /// </summary>
    public static class VRInput
    {
        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;

                var gamepads = Gamepad.all;
                for (int i = 0; i < gamepads.Count; i++)
                {
                    Vector2 stick = gamepads[i].leftStick.ReadValue();
                    if (stick.sqrMagnitude > v.sqrMagnitude)
                        v = stick;
                }

                var joysticks = Joystick.all;
                for (int i = 0; i < joysticks.Count; i++)
                {
                    Vector2 stick = joysticks[i].stick.ReadValue();
                    if (stick.sqrMagnitude > v.sqrMagnitude)
                        v = stick;
                }

                var kb = Keyboard.current;
                if (kb != null)
                {
                    Vector2 kv = Vector2.zero;
                    if (kb.wKey.isPressed) kv.y += 1f;
                    if (kb.sKey.isPressed) kv.y -= 1f;
                    if (kb.dKey.isPressed) kv.x += 1f;
                    if (kb.aKey.isPressed) kv.x -= 1f;
                    if (kv.sqrMagnitude > v.sqrMagnitude)
                        v = kv;
                }

                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static float Turn
        {
            get
            {
                float x = 0f;
                var gamepads = Gamepad.all;
                for (int i = 0; i < gamepads.Count; i++)
                {
                    float gx = gamepads[i].rightStick.ReadValue().x;
                    if (Mathf.Abs(gx) > Mathf.Abs(x))
                        x = gx;
                }

                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.rightArrowKey.isPressed) x += 1f;
                    if (kb.leftArrowKey.isPressed) x -= 1f;
                }
                return Mathf.Clamp(x, -1f, 1f);
            }
        }

        /// <summary>
        /// Botão para PEGAR um objeto quando as mãos estão livres.
        /// Aceita qualquer botão de ação do controle para funcionar com qualquer modelo de gamepad Bluetooth/VR.
        /// </summary>
        public static bool GrabPressed
        {
            get
            {
                var gamepads = Gamepad.all;
                for (int i = 0; i < gamepads.Count; i++)
                {
                    var gp = gamepads[i];
                    if (gp.buttonSouth.wasPressedThisFrame ||
                        gp.buttonWest.wasPressedThisFrame ||
                        gp.buttonEast.wasPressedThisFrame ||
                        gp.buttonNorth.wasPressedThisFrame ||
                        gp.rightShoulder.wasPressedThisFrame ||
                        gp.leftShoulder.wasPressedThisFrame ||
                        gp.rightTrigger.wasPressedThisFrame ||
                        gp.leftTrigger.wasPressedThisFrame)
                    {
                        return true;
                    }
                }

                var joysticks = Joystick.all;
                for (int i = 0; i < joysticks.Count; i++)
                {
                    var js = joysticks[i];
                    if (js.trigger.wasPressedThisFrame) return true;
                    var controls = js.allControls;
                    for (int c = 0; c < controls.Count; c++)
                    {
                        if (controls[c] is ButtonControl btn && btn.wasPressedThisFrame)
                            return true;
                    }
                }

                var kb = Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame))
                    return true;

#if UNITY_EDITOR || UNITY_STANDALONE
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    return true;
#endif
                return false;
            }
        }

        /// <summary>Botão para SOLTAR suavemente o objeto que já está sendo segurado.</summary>
        public static bool ReleasePressed
        {
            get
            {
                var gamepads = Gamepad.all;
                for (int i = 0; i < gamepads.Count; i++)
                {
                    var gp = gamepads[i];
                    if (gp.buttonSouth.wasPressedThisFrame ||
                        gp.buttonEast.wasPressedThisFrame ||
                        gp.buttonNorth.wasPressedThisFrame ||
                        gp.rightShoulder.wasPressedThisFrame ||
                        gp.leftShoulder.wasPressedThisFrame)
                    {
                        return true;
                    }
                }

                var joysticks = Joystick.all;
                for (int i = 0; i < joysticks.Count; i++)
                {
                    if (joysticks[i].trigger.wasPressedThisFrame)
                        return true;
                }

                var kb = Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame))
                    return true;

                return false;
            }
        }

        /// <summary>Botão para ARREMESSAR o objeto segurado (estilo Gravity Gun do Half-Life 2).</summary>
        public static bool ThrowPressed
        {
            get
            {
                var gamepads = Gamepad.all;
                for (int i = 0; i < gamepads.Count; i++)
                {
                    var gp = gamepads[i];
                    if (gp.rightTrigger.wasPressedThisFrame ||
                        gp.leftTrigger.wasPressedThisFrame ||
                        gp.buttonWest.wasPressedThisFrame)
                    {
                        return true;
                    }
                }

#if UNITY_EDITOR || UNITY_STANDALONE
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    return true;
#endif
                return false;
            }
        }

        /// <summary>Eixo contínuo (-1..1) para aproximar/afastar o objeto segurado na mira.</summary>
        public static float HoldDistanceAxis
        {
            get
            {
                float a = 0f;
                var gamepads = Gamepad.all;
                for (int i = 0; i < gamepads.Count; i++)
                {
                    var gp = gamepads[i];
                    if (gp.dpad.up.isPressed) a += 1f;
                    if (gp.dpad.down.isPressed) a -= 1f;

                    float ry = gp.rightStick.ReadValue().y;
                    if (Mathf.Abs(ry) > 0.25f && Mathf.Abs(ry) > Mathf.Abs(a))
                        a = ry;
                }

                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.upArrowKey.isPressed) a += 1f;
                    if (kb.downArrowKey.isPressed) a -= 1f;
                }
                return Mathf.Clamp(a, -1f, 1f);
            }
        }

        /// <summary>Passos discretos da roda do mouse (-1, 0 ou 1).</summary>
        public static float HoldDistanceSteps
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    float y = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(y) > 0.01f) return Mathf.Sign(y);
                }
#endif
                return 0f;
            }
        }

        /// <summary>Delta do mouse enquanto o botão direito está pressionado (para girar a visão no Editor).</summary>
        public static Vector2 EditorLookDelta
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                var mouse = Mouse.current;
                if (mouse != null && mouse.rightButton.isPressed)
                    return mouse.delta.ReadValue();
#endif
                return Vector2.zero;
            }
        }
    }
}
