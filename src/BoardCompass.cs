using BepInEx.Configuration;
using HarmonyLib;
using Nivalis.Navigation;
using Nivalis.UI;
using NivalisMods.ModCompanion.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

// The marker is a child of the deployed visual: native area visibility and movement
// carry it automatically. No world searches or separate position polling are needed.
internal sealed class BoardCompass : IDisposable
{
    internal const NavigationMarkerType MarkerType = (NavigationMarkerType)0x484F43;
    private static Setting<bool> _show = null!;
    internal static NavigationPipDisplayData? Display;
    private readonly GameObject _marker;
    private bool _parked;

    internal static void Configure(SettingsCategory general, ConfigFile config)
    {
        const string key = "ShowParkedHobaOnCompass";
        const string description = "Mark your parked hoverboard on the compass while you are in the same area. Hidden while riding.";
        // Preserve the existing config key while displaying this setting in General.
        _show = general.BindToggle(key, "Show parked HOBA on compass",
            config.Bind("Discovery", key, true, description), description);
    }

    internal BoardCompass(Transform parent, Sprite icon)
    {
        Display = new NavigationPipDisplayData {
            icon = icon, color = new Color(.15f,.85f,1),
            compassDisplayRange = new Vector2(0,10000), scannerDisplayRange = Vector2.zero
        };
        _marker = new GameObject("HOBA parked compass marker");
        _marker.SetActive(false);
        _marker.transform.SetParent(parent, false);
        _marker.transform.localPosition = Vector3.up * .12f;
        _marker.AddComponent<CompassMarker>().type = MarkerType;
        _show.Changed += OnSettingChanged;
    }

    internal void SetParked(bool parked) { _parked = parked; Refresh(); }
    private void OnSettingChanged(bool _) => Refresh();
    private void Refresh()
    {
        if (_marker == null) return;
        var visible = _parked && _show.Value;
        if (_marker.activeSelf != visible) _marker.SetActive(visible);
    }
    public void Dispose()
    {
        _show.Changed -= OnSettingChanged;
        if (_marker != null) { _marker.SetActive(false); Object.Destroy(_marker); }
    }
}

[HarmonyPatch(typeof(NavigationManager), nameof(NavigationManager.GetDisplayDataForMarkerType))]
internal static class BoardCompassDisplayPatch
{
    private static bool Prefix(NavigationMarkerType __0, ref NavigationPipDisplayData __result)
    {
        if (__0 != BoardCompass.MarkerType || BoardCompass.Display == null) return true;
        __result = BoardCompass.Display;
        return false;
    }
}
