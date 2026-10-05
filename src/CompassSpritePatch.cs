using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NivalisMods.Hoba;

// Apply only to our supplied sprites, without detouring CompassIcon.Display's
// native by-reference struct signature or changing other compass markers.
[HarmonyPatch(typeof(Image), nameof(Image.sprite), MethodType.Setter)]
internal static class CompassSpritePatch
{
    private static bool _reportedLegendary, _reportedBoard;
    private static void Postfix(Image __instance, Sprite __0)
    {
        if (__0 == null || (__0 != LegendaryCompass.Icon && __0 != BoardCompass.Display?.icon)) return;
        // Inventory uses the board icon too; only normalize native compass widgets.
        if (__instance.GetComponentInParent<Nivalis.UI.CompassIcon>() == null) return;
        try
        {
            var legendary = __0 == LegendaryCompass.Icon;
            if (legendary ? !_reportedLegendary : !_reportedBoard)
            {
                if (legendary) _reportedLegendary = true;
                else _reportedBoard = true;
                Plugin.Logger.LogInfo($"HOBA compass sprite: {__0.name}, {__0.texture.width}x{__0.texture.height}; " +
                    $"native image type={__instance.type}, material={__instance.material?.name}, override={__instance.overrideSprite?.name}");
            }
            __instance.overrideSprite = null;
            __instance.material = null; // Unity UI/Default samples the PNG alpha channel.
            __instance.type = Image.Type.Simple;
            __instance.preserveAspect = true;
            __instance.SetAllDirty();
        }
        catch (Exception e)
        {
            // Do not throw through Unity's native UI code.
            if (!_failed) { _failed = true; Plugin.Logger.LogWarning("HOBA compass image setup: " + e.Message); }
        }
    }
    private static bool _failed;
}
