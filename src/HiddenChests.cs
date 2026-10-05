using Il2CppInterop.Runtime.Attributes;
using Nivalis;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NivalisMods.Hoba;

public sealed class HiddenChests : MonoBehaviour
{
    private sealed record Chest(ContainerPlacement Placement, ChestVisual Visual, BoxCollider Collider, GameObject Marker);
    internal static HiddenChests? Instance;
    private readonly List<Chest> _chests = new();
    private string _area = "";
    private bool _failed;
    private const float SearchCheckRadiusSquared = 15f * 15f;
    private int _targetFrame = -1;
    private Chest? _target;
    private static int _searchFrame = -1;
    internal static bool HasSearchTarget => _searchFrame == Time.frameCount || Instance?.Target() != null;
    internal static string Prompt => "Search ?";

    public HiddenChests(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;

    public void Update()
    {
        try
        {
            var scenes = GameSceneManager._instance;
            if (!HobaSave.Ready || scenes == null || !scenes.IsGame || scenes.IsLoading ||
                GameSceneManager.IsUnloadingGameplay)
            { if (_chests.Count > 0 || _area != "") Clear(); return; }
            var area = scenes.CurrentGameplaySceneName;
            if (_area != area)
            {
                Clear(); _area = area;
                foreach (var p in WorldPlacements.Chests.Where(p => p.Area == area))
                {
                    var visual = new ChestVisual(p);
                    try
                    {
                        var t = visual.Root.transform;
                        t.localScale = Vector3.one * p.Scale;
                        t.SetPositionAndRotation(new(p.Position[0],p.Position[1],p.Position[2]),
                            new(p.RotationQuaternion[0],p.RotationQuaternion[1],p.RotationQuaternion[2],p.RotationQuaternion[3]));
                        var collider = visual.Root.AddComponent<BoxCollider>();
                        collider.center = visual.Bounds.center; collider.size = visual.Bounds.size;
                        collider.isTrigger = false;
                        var marker = LegendaryCompass.Create(t);
                        _chests.Add(new Chest(p, visual, collider, marker));
                        visual.Root.SetActive(true);
                    }
                    catch { visual.Dispose(); throw; }
                }
            }
            foreach (var c in _chests)
            {
                var visible = LegendaryCompass.Visible(c.Placement.Id);
                if (c.Marker.activeSelf != visible) c.Marker.SetActive(visible);
            }
            if (_failed || !RideController.GameplayAvailable() || Mouse.current?.leftButton.wasPressedThisFrame != true) return;
            var chest = Target();
            if (chest != null) Search(chest);
        }
        catch (Exception e)
        {
            // Keep the area marked to avoid retrying/spamming every frame.
            foreach (var c in _chests) c.Visual.Dispose();
            _chests.Clear(); _target = null; _targetFrame = -1; _failed = true;
            Plugin.Logger.LogError("HOBA hidden chests stopped in this area: " + e);
        }
    }

    [HideFromIl2Cpp]
    private Chest? Target()
    {
        var frame = Time.frameCount;
        if (_targetFrame == frame) return _target;
        _targetFrame = frame;
        _target = null;
        if (_chests.Count == 0 || _failed || !HobaSave.Ready) return null;
        var player = PlayerManager._instance?.LocalPlayer?.Character;
        if (player == null) return null;
        var position = player.transform.position;
        var rayRange = 0f;
        foreach (var chest in _chests)
        {
            if (HobaSave.State.Discoveries.ContainsKey(chest.Placement.Id)) continue;
            var p = chest.Placement.Position;
            var dx = position.x - p[0]; var dy = position.y - p[1]; var dz = position.z - p[2];
            if (dx * dx + dy * dy + dz * dz <= SearchCheckRadiusSquared)
                rayRange = Math.Max(rayRange, chest.Placement.InteractionRange);
        }
        // Only nearby, undiscovered chests warrant gameplay/aim checks. The radius does
        // not increase the authored interaction distance or change compass visibility.
        if (rayRange <= 0 || !RideController.GameplayAvailable() ||
            RideController.Instance?.IsMounted == true || !RideController.CanRide(player)) return null;
        var eye = player.Controller.Camera.transform;
        if (!Physics.Raycast(eye.position, eye.forward, out var hit, rayRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
        foreach (var chest in _chests)
        {
            if (chest.Collider == null || hit.collider != chest.Collider ||
                hit.distance > chest.Placement.InteractionRange || HobaSave.State.Discoveries.ContainsKey(chest.Placement.Id)) continue;
            var p = chest.Placement.Position;
            var dx = position.x - p[0]; var dy = position.y - p[1]; var dz = position.z - p[2];
            if (dx * dx + dy * dy + dz * dz <= SearchCheckRadiusSquared)
                return _target = chest;
        }
        return null;
    }

    [HideFromIl2Cpp]
    private static void Search(Chest chest)
    {
        _searchFrame = Time.frameCount;
        if (!HobaSave.State.TryDiscover(chest.Placement.Id, BoardItem.GrantLegendary, out var reward))
        { BoardItem.Notify("Make room in your inventory to search this chest."); return; }
        if (Instance != null) Instance._target = null;
        chest.Marker.SetActive(false);
        BoardItem.Notify("Legendary unlocked: " + reward!.Name + "!");
    }

    [HideFromIl2Cpp]
    internal void Clear()
    {
        _area = ""; _failed = false; _target = null; _targetFrame = -1; _searchFrame = -1;
        foreach (var c in _chests) c.Visual.Dispose();
        _chests.Clear();
    }
    public void OnDisable() => Clear();
    public void OnDestroy() { Clear(); if (Instance == this) Instance = null; }
}
