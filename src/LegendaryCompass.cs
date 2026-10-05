using NivalisMods.ModCompanion.Api;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nivalis.Navigation;
using Nivalis.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

internal static class LegendaryCompass
{
    // Private runtime-only marker type; never written to native saves.
    internal const NavigationMarkerType MarkerType = (NavigationMarkerType)0x484F42;
    private static Setting<bool> _show = null!;
    private static NavigationPipDisplayData? _display;
    private static Texture2D? _texture;
    private static Sprite? _sprite;
    internal static void Configure(SettingsCategory discovery) => _show = discovery.Toggle("ShowLegendariesOnCompass", "Show legendaries on compass", false,
        "Show unsearched legendary chests in the current area as question marks on the compass.");
    internal static bool Visible(string id) => _show.Value && !HobaSave.State.Discoveries.ContainsKey(id);

    internal static GameObject Create(Transform parent)
    {
        EnsureDisplay();
        var root = new GameObject("HOBA legendary compass marker");
        root.SetActive(false);
        root.transform.SetParent(parent, false);
        root.AddComponent<CompassMarker>().type = MarkerType;
        return root;
    }

    private static void EnsureDisplay()
    {
        if (_display != null && _sprite != null && _texture != null) return;
        Dispose();
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Hoba.LegendaryCompass.png")
            ?? throw new InvalidOperationException("Missing legendary compass icon.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            { name = "HOBA question mark", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
              hideFlags = HideFlags.DontUnloadUnusedAsset };
        if (!ImageConversion.LoadImage(_texture, new Il2CppStructArray<byte>(bytes.ToArray()), false))
        {
            Object.Destroy(_texture); _texture = null;
            throw new InvalidOperationException("Could not load legendary compass icon.");
        }
        _sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), new Vector2(.5f, .5f), 100);
        _sprite.name = "HOBA legendary question mark";
        _sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        _display = new NavigationPipDisplayData { icon = _sprite, color = new Color(1f, .78f, .35f),
            compassDisplayRange = new Vector2(0, 10000), scannerDisplayRange = Vector2.zero };
    }

    internal static Sprite? Icon => _sprite;
    internal static NavigationPipDisplayData Display
    {
        get { EnsureDisplay(); return _display!; }
    }
    internal static void Dispose()
    {
        _display = null;
        if (_sprite != null) Object.Destroy(_sprite);
        if (_texture != null) Object.Destroy(_texture);
        _sprite = null; _texture = null;
    }
}

[HarmonyPatch(typeof(NavigationManager), nameof(NavigationManager.GetDisplayDataForMarkerType))]
internal static class LegendaryCompassDisplayPatch
{
    private static bool Prefix(NavigationMarkerType __0, ref NavigationPipDisplayData __result)
    {
        if (__0 != LegendaryCompass.MarkerType) return true;
        __result = LegendaryCompass.Display;
        return false;
    }
}
