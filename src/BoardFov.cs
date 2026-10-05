using Cinemachine;
using HarmonyLib;
using Nivalis;
using NivalisMods.ModCompanion.Api;
using UnityEngine;

namespace NivalisMods.Hoba;

internal static class BoardFov
{
    private static Setting<float> _maximum = null!;
    private static readonly SpeedFov Blend = new();
    private static Camera? _camera;
    private static float _base, _applied;
    private static int _frame = -1;
    internal static void Configure(SettingsCategory camera) => _maximum = camera.Slider(
        "FovIncrease", "FOV increase on board", 10f, 0, 30, 1,
        "Extra FOV in degrees at full speed, added to your current view. Blends with speed; 0 disables. Applies immediately.");

    internal static void Before(CinemachineBrain brain)
    {
        if (_camera != null && brain.OutputCamera == _camera) RemoveBonus();
    }

    internal static void RemoveBonus()
    {
        if (_camera != null && Mathf.Abs(_camera.fieldOfView - _applied) < .001f)
            _camera.fieldOfView = _base;
        _camera = null;
    }
    internal static void Reset() { RemoveBonus(); Blend.Reset(); _frame = -1; }

    internal static void Apply(CinemachineBrain brain)
    {
        var ride = RideController.Instance;
        var controller = PlayerManager._instance?.LocalPlayer?.Character?.Controller;
        if (controller == null || ride?.Owns(controller) != true) { Reset(); return; }
        var camera = brain.OutputCamera;
        if (camera == null || camera != controller.Camera || camera.orthographic) return;
        if (_frame != Time.frameCount)
        {
            Blend.Step(ride.FovSpeedRatio, _maximum.Value, RideController.GameplayAvailable() ? Time.deltaTime : 0);
            _frame = Time.frameCount;
        }
        _camera = camera;
        _base = camera.fieldOfView;
        _applied = Mathf.Clamp(_base + Blend.Bonus, 1, 179);
        camera.fieldOfView = _applied;
    }
}

// Ordinary no-argument method: never detour native ref CameraState/LensSettings wrappers.
[HarmonyPatch(typeof(CinemachineBrain), nameof(CinemachineBrain.ManualUpdate))]
internal static class BoardFovPatch
{
    private static void Prefix(CinemachineBrain __instance) => BoardFov.Before(__instance);
    private static void Postfix(CinemachineBrain __instance)
    {
        try { BoardFov.Apply(__instance); }
        catch (Exception e) { BoardFov.Reset(); Plugin.Logger.LogWarning("HOBA FOV: " + e.Message); }
    }
}
