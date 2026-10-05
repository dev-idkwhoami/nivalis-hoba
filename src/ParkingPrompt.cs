using HarmonyLib;
using Nivalis;
using UnityEngine;

namespace NivalisMods.Hoba;

// Use the game's crosshair label; do not introduce a separate HUD canvas.
[HarmonyPatch(typeof(CrosshairUI), nameof(CrosshairUI.LateUpdate))]
internal static class ParkingPrompt
{
    private static CrosshairUI? _owned;
    private static bool _active, _enabled;
    private static Color _color;
    private static float _alpha;

    [HarmonyPrefix, HarmonyBefore("local.nivalis.cigarette")]
    private static bool Prefix(CrosshairUI __instance)
    {
        try
        {
            // Keep native door, NPC and item prompts when they have focus.
            var search = HiddenChests.HasSearchTarget;
            if (search ||
                PlayerManager._instance?.LocalPlayer?.Character?.Interaction?.CurrentFocus == null &&
                RideController.Instance?.CanReachParked() == true)
            {
                if (_owned != __instance)
                {
                    Release();
                    var label = __instance.interactionTMP;
                    _active = label.gameObject.activeSelf; _enabled = label.enabled;
                    _color = label.color; _alpha = label.canvasRenderer.GetAlpha();
                    _owned = __instance;
                }
                if (!__instance._currentlyFocused) __instance.GoToCrosshairState(true);
                var text = __instance.interactionTMP;
                text.gameObject.SetActive(true);
                text.enabled = true;
                var color = text.color; color.a = 1;
                text.color = color;
                text.canvasRenderer.SetAlpha(1);
                text.text = search ? HiddenChests.Prompt : SprayTool.Equipped ? SprayTool.Prompt : "[LMB] Ride " + (BoardItem.Deployed?.Name ?? "HOBA") + "  •  [Shift + LMB] Pick up";
                return false;
            }
        }
        catch (Exception e) { Plugin.Logger.LogWarning("HOBA prompt: " + e.Message); }
        Release();
        return true;
    }

    internal static void Release()
    {
        var owned = _owned;
        _owned = null;
        if (owned == null) return;
        owned.GoToCrosshairState(false);
        var label = owned.interactionTMP;
        if (label == null) return;
        label.text = ""; label.color = _color; label.canvasRenderer.SetAlpha(_alpha);
        label.enabled = _enabled; label.gameObject.SetActive(_active);
    }
}
