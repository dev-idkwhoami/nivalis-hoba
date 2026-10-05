using HarmonyLib;
using Cinemachine;
using Nivalis;
using UnityEngine;

namespace NivalisMods.Hoba;

[HarmonyPatch(typeof(PlayerCharacterController), nameof(PlayerCharacterController.UpdateMovement))]
internal static class RideMovementPatch
{
    internal struct Snapshot
    {
        internal bool Applied;
        internal float Walk, Sprint, Scale, Delta;
        internal Vector3 Position;
    }

    [HarmonyPrefix]
    private static bool Prefix(PlayerCharacterController __instance, ref float __0, ref Vector3 __1, out Snapshot __state)
    {
        __state = default;
        var ride = RideController.Instance;
        if (ride == null || !ride.Owns(__instance)) return true;
        if (ride.Suspended) return false;
        try
        {
            var dt = float.IsFinite(__0) ? Mathf.Clamp(__0, 0, .05f) : 0;
            var velocity = ride.Move(dt);
            if (!ride.Owns(__instance)) return true;
            __state = new Snapshot
            {
                Applied = true, Walk = __instance.defaultMoveSpeed, Sprint = __instance.sprintSpeed,
                Scale = __instance._moveSpeedScale, Position = __instance.transform.position, Delta = dt
            };
            // Native UpdateMovement normalizes its direction. Supply the magnitude separately,
            // then let its gravity, collision, velocity and ground handling run unchanged.
            var speed = velocity.magnitude;
            __instance.defaultMoveSpeed = __instance.sprintSpeed = speed;
            __instance._moveSpeedScale = 1;
            __1 = speed > .0001f ? velocity / speed : Vector3.zero;
            __0 = dt;
        }
        catch (Exception e) { ride.Fail(e); }
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(PlayerCharacterController __instance, Snapshot __state, Exception? __exception)
    {
        if (!__state.Applied) return;
        try
        {
            __instance.defaultMoveSpeed = __state.Walk;
            __instance.sprintSpeed = __state.Sprint;
            __instance._moveSpeedScale = __state.Scale;
            if (__exception != null) RideController.Instance?.Fail(__exception);
            else RideController.Instance?.AfterMovement(__state.Position, __state.Delta);
        }
        catch (Exception e) { RideController.Instance?.Fail(e); }
    }
}

[HarmonyPatch(typeof(PlayerCharacterController), nameof(PlayerCharacterController.Move))]
internal static class RideCollisionPatch
{
    private static bool _substepping;

    [HarmonyPrefix]
    private static bool Prefix(PlayerCharacterController __instance, Vector3 __0)
    {
        if (_substepping || RideController.Instance?.Owns(__instance) != true || __0.magnitude <= .18f) return true;
        // Use the native collision path for every short segment, including native navmesh bounds.
        // This is not a teleport and does not enable noclip or add a second physics controller.
        var steps = Mathf.Clamp(Mathf.CeilToInt(__0.magnitude / .18f), 1, 16);
        _substepping = true;
        try
        {
            for (var i = 0; i < steps; i++) __instance.Move(__0 / steps);
        }
        catch (Exception e) { RideController.Instance?.Fail(e); }
        finally { _substepping = false; }
        return false;
    }
}

[HarmonyPatch(typeof(PlayerCharacterController), nameof(PlayerCharacterController.HeadBob))]
internal static class RideHeadBobPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerCharacterController __instance) => RideController.Instance?.Owns(__instance) != true;
}

[HarmonyPatch(typeof(PlayerCharacterController), nameof(PlayerCharacterController.ManualUpdate))]
internal static class RideCameraPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerCharacterController __instance) =>
        RideController.Instance?.Owns(__instance) != true || !RideController.Instance.Suspended;

    [HarmonyPostfix]
    private static void Postfix(PlayerCharacterController __instance)
    {
        var ride = RideController.Instance;
        if (ride == null || !ride.Owns(__instance)) return;
        try { ride.Present(Mathf.Clamp(Time.unscaledDeltaTime, 0, .05f)); }
        catch (Exception e) { ride.Fail(e); }
    }
}

[HarmonyPatch(typeof(PlayerCharacterController), nameof(PlayerCharacterController.OnDisable))]
internal static class RideDisablePatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerCharacterController __instance)
    {
        if (RideController.Instance?.Owns(__instance) == true && !RideController.Instance.Suspended)
            RideController.Instance.Dismount("player controller disabled");
    }
}

[HarmonyPatch(typeof(GameSceneManager), nameof(GameSceneManager.LoadArea))]
internal static class ParkBeforeTravelPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        try { RideController.Instance?.LeaveArea(); }
        catch (Exception e) { RideController.Instance?.Fail(e); }
    }
}

[HarmonyPatch]
internal static class ClearBoardSessionPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<System.Reflection.MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(GameSceneManager), nameof(GameSceneManager.EndGame));
        yield return AccessTools.Method(typeof(GameSceneManager), nameof(GameSceneManager.StartGame));
        yield return AccessTools.Method(typeof(SerializationManager), nameof(SerializationManager.Load));
    }

    [HarmonyPrefix]
    private static void Prefix() => RideController.Instance?.ResetBoard("game session changed");
}

// Native UpdateFootsteps produces both audio and the snow-render-texture stamp.
[HarmonyPatch(typeof(PlayerHandsAnimator), nameof(PlayerHandsAnimator.UpdateFootsteps))]
internal static class RideFootstepsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerHandsAnimator __instance)
    {
        var controller = __instance.character?.Controller;
        return controller == null || RideController.Instance?.Owns(controller) != true;
    }
}

// Keep the native FOV/audio/look sensitivity, releasing only zoom's own movement lock.
[HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.UpdateCameraZooming))]
internal static class RideZoomPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerCameraController __instance)
    {
        var controller = PlayerManager._instance?.LocalPlayer?.Character?.Controller;
        if (controller == null || RideController.Instance?.Owns(controller) != true ||
            controller.CameraController?.Pointer != __instance.Pointer) return;
        __instance.playerMovementLock?.Release();
        __instance.playerMovementLock = null;
    }
}

// Do not detour MutateCameraState(ref CameraState, float): CameraState is a
// non-blittable IL2CPP value type represented by a managed wrapper. The installed
// bridge interprets its native by-ref data as an object pointer and crashes.
// Native POV calls this after axis input, before composing camera orientation.
[HarmonyPatch(typeof(CinemachinePOV), nameof(CinemachinePOV.GetRecenterTarget))]
internal static class RideLookPatch
{
    [HarmonyPostfix]
    private static void Postfix(CinemachinePOV __instance)
    {
        try { RideController.Instance?.ConstrainLook(__instance, Time.unscaledDeltaTime); }
        catch (Exception e) { RideController.Instance?.Fail(e); }
    }
}
