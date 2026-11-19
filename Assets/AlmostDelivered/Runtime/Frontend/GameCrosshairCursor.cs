using AlmostDelivered.Gameplay.Payload;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Frontend
{
    /// <summary>Account-scoped color entry point for a future settings screen.</summary>
    public static class CrosshairColorSettings
    {
        private const string Prefix = "AlmostDelivered.CrosshairColor.v1.";
        public static int Revision { get; private set; }

        public static Color GetColor()
        {
            var stored = PlayerPrefs.GetString(Prefix + LocalPlayerAccount.CurrentAccountId, string.Empty);
            return ColorUtility.TryParseHtmlString("#" + stored, out var parsed)
                ? parsed : new Color(.27f, .94f, 1f, 1f);
        }

        public static void SetColor(Color color)
        {
            if (float.IsNaN(color.r) || float.IsNaN(color.g) || float.IsNaN(color.b)) return;
            color.a = 1f;
            PlayerPrefs.SetString(Prefix + LocalPlayerAccount.CurrentAccountId,
                ColorUtility.ToHtmlStringRGB(color));
            PlayerPrefs.Save();
            Revision++;
        }
    }

    public sealed class GameCrosshairCursor : MonoBehaviour
    {
        private Texture2D texture;
        private HubWeaponStation weaponStation;
        private HubTraitMenu traitMenu;
        private HubAreaStation[] areaStations;
        private PayloadMissionController mission;
        private string accountId;
        private int colorRevision = -1;
        private bool crosshairActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSceneFlow.DepartureHubScene &&
                scene.name != GameSceneFlow.MissionPrototypeScene)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            if (FindObjectOfType<GameCrosshairCursor>() == null)
                new GameObject("Game Crosshair Cursor").AddComponent<GameCrosshairCursor>();
        }

        private void Start()
        {
            weaponStation = FindObjectOfType<HubWeaponStation>();
            traitMenu = FindObjectOfType<HubTraitMenu>();
            areaStations = FindObjectsOfType<HubAreaStation>();
            mission = FindObjectOfType<PayloadMissionController>();
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            var currentAccount = LocalPlayerAccount.CurrentAccountId;
            if (texture == null || accountId != currentAccount || colorRevision != CrosshairColorSettings.Revision)
            {
                accountId = currentAccount;
                colorRevision = CrosshairColorSettings.Revision;
                if (texture != null) Destroy(texture);
                texture = CreateTexture(CrosshairColorSettings.GetColor());
                crosshairActive = false;
            }

            var menuOpen = weaponStation != null && weaponStation.IsMenuOpen ||
                traitMenu != null && traitMenu.IsOpen ||
                mission != null && mission.IsRelayChoicePending;
            if (areaStations != null)
                foreach (var station in areaStations)
                    menuOpen |= station != null && station.IsMenuOpen;
            if (crosshairActive == !menuOpen) return;

            crosshairActive = !menuOpen;
            Cursor.visible = true;
            Cursor.SetCursor(crosshairActive ? texture : null,
                crosshairActive ? new Vector2(16f, 16f) : Vector2.zero, CursorMode.Auto);
        }

        private void OnDestroy()
        {
            if (crosshairActive) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (texture != null) Destroy(texture);
        }

        public static Texture2D CreateTexture(Color color)
        {
            var result = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[32 * 32];
            var ink = (Color32)new Color(color.r, color.g, color.b, 1f);
            Paint(pixels, 4, 14, 14, 19, Color.black);
            Paint(pixels, 18, 14, 28, 19, Color.black);
            Paint(pixels, 14, 4, 19, 14, Color.black);
            Paint(pixels, 14, 19, 19, 29, Color.black);
            Paint(pixels, 5, 15, 13, 18, ink);
            Paint(pixels, 19, 15, 27, 18, ink);
            Paint(pixels, 15, 5, 18, 13, ink);
            Paint(pixels, 15, 20, 18, 28, ink);
            Paint(pixels, 15, 15, 18, 18, Color.black);
            pixels[16 + 16 * 32] = ink;
            result.SetPixels32(pixels);
            result.Apply(false, false);
            return result;
        }

        private static void Paint(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 color)
        {
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                    pixels[x + y * 32] = color;
        }
    }
}
