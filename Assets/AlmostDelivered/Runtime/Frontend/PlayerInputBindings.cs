using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public enum PlayerAction { Sprint, Roll }

    /// <summary>Single entry point for keys; a future settings menu can call SetKey.</summary>
    public static class PlayerInputBindings
    {
        private const string PrefPrefix = "AlmostDelivered.Key.";

        public static KeyCode GetKey(PlayerAction action)
        {
            var fallback = action == PlayerAction.Sprint ? KeyCode.LeftShift : KeyCode.LeftControl;
            return (KeyCode)PlayerPrefs.GetInt(PrefPrefix + action, (int)fallback);
        }

        public static void SetKey(PlayerAction action, KeyCode key)
        {
            if (key == KeyCode.None) return;
            PlayerPrefs.SetInt(PrefPrefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static string DisplayName(PlayerAction action)
        {
            var key = GetKey(action);
            if (key == KeyCode.LeftShift || key == KeyCode.RightShift) return "Shift";
            if (key == KeyCode.LeftControl || key == KeyCode.RightControl) return "Ctrl";
            return key.ToString();
        }

        public static bool IsHeld(PlayerAction action)
        {
            var key = GetKey(action);
            return Input.GetKey(key) || (key == KeyCode.LeftShift && Input.GetKey(KeyCode.RightShift));
        }

        public static bool WasPressed(PlayerAction action)
        {
            var key = GetKey(action);
            return Input.GetKeyDown(key) || (key == KeyCode.LeftControl && Input.GetKeyDown(KeyCode.RightControl));
        }
    }
}
