using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Nivalis;
using UnityEngine;

namespace NivalisMods.Hoba;

public sealed class VendorController : MonoBehaviour
{
    internal static VendorController? Instance;
    private readonly VendorLocationFile _locations = WorldPlacements.Vendors;
    private readonly Dictionary<string, VendorNpc> _spawned = new();
    private readonly Dictionary<string, int> _attempts = new();
    private string _area = "";
    private int _scene = -1;
    private float _nextRefresh;
    public VendorController(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;

    public void Update()
    {
        try
        {
            var scenes = GameSceneManager._instance;
            var player = PlayerManager._instance?.LocalPlayer?.Character;
            if (scenes == null || !scenes.IsGame || scenes.IsLoading || GameSceneManager.IsUnloadingGameplay || player == null)
            { if (_scene != -1) Clear(); return; }
            var area = scenes.CurrentGameplaySceneName;
            if (string.IsNullOrEmpty(area)) return;
            if (_area != area || _scene != player.gameObject.scene.handle)
            {
                Clear(); _area = area; _scene = player.gameObject.scene.handle;
                _nextRefresh = Time.unscaledTime + 1;
            }
            if (!RideController.GameplayAvailable()) return;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 2;
            SpawnPending(player);
        }
        catch (Exception e) { Plugin.Logger.LogError("HOBA vendor: " + e); _nextRefresh = Time.unscaledTime + 5; }
    }

    public void LateUpdate()
    {
        try { foreach (var npc in _spawned.Values) npc.FinishPlacement(); }
        catch (Exception e) { Plugin.Logger.LogWarning("HOBA dealer fitting: " + e.Message); }
    }

    [HideFromIl2Cpp]
    private void SpawnPending(PlayerCharacter player)
    {
        var pending = _locations.Locations.Where(p => p.Enabled && p.Area == _area && !_spawned.ContainsKey(p.Id) &&
            _attempts.GetValueOrDefault(p.Id) < 8 && p.DistanceSquared(player.transform.position.x,
                player.transform.position.y, player.transform.position.z) > 1).ToArray();
        if (pending.Length == 0) return;
        var templates = VendorNpc.Templates();
        foreach (var p in pending)
        {
            var attempt = _attempts.GetValueOrDefault(p.Id) + 1;
            _attempts[p.Id] = attempt;
            try
            {
                var template = string.IsNullOrEmpty(p.NpcPrefab) ? templates.FirstOrDefault() : templates.FirstOrDefault(t => t.name == p.NpcPrefab);
                if (template == null) throw new InvalidOperationException("Configured NPC template is not loaded: " + p.NpcPrefab);
                _spawned.Add(p.Id, VendorNpc.Spawn(p, template, player));
                Plugin.Logger.LogInfo("HOBA shop ready: " + p.Id);
            }
            catch (Exception e)
            {
                if (attempt is 1 or 8) Plugin.Logger.LogWarning($"HOBA vendor {p.Id}, attempt {attempt}/8: {e.Message}");
                if (attempt == 8) Notify("Could not spawn " + p.Id + ". Check the log for details.");
            }
        }
    }

    [HideFromIl2Cpp]
    private void Notify(string message)
    {
        Plugin.Logger.LogInfo("HOBA shops: " + message);
        var notifications = NotificationManager._instance;
        if (notifications != null)
        {
            try { notifications.CreateMessage("HOBA shops", message, this); }
            catch (Exception e) { Plugin.Logger.LogWarning("Vendor notification unavailable: " + e.Message); }
        }
    }

    [HideFromIl2Cpp]
    internal void Clear()
    {
        foreach (var npc in _spawned.Values) npc.Dispose();
        _spawned.Clear(); _attempts.Clear(); _area = ""; _scene = -1;
    }
    public void OnDisable() => Clear();
    public void OnDestroy() { Clear(); if (Instance == this) Instance = null; }
}

[HarmonyPatch]
internal static class VendorTravelPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<System.Reflection.MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(GameSceneManager), nameof(GameSceneManager.LoadArea));
        yield return AccessTools.Method(typeof(GameSceneManager), nameof(GameSceneManager.EndGame));
        yield return AccessTools.Method(typeof(GameSceneManager), nameof(GameSceneManager.StartGame));
        yield return AccessTools.Method(typeof(SerializationManager), nameof(SerializationManager.Load));
    }
    [HarmonyPrefix]
    private static void Prefix() => VendorController.Instance?.Clear();
}
