using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nivalis;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

// A mod-owned stationary dealer; its shop definition and stock are independent.
internal sealed class VendorNpc : IDisposable
{
    private Character? _character;
    private GameObject? _spotRoot;
    private AISpot? _spot;
    private CharacterTask? _task;
    private IdleAction? _idle;
    private BoardShop? _shop;
    internal void FinishPlacement() => _shop?.FinishPlacement();
    internal bool Alive => _character != null && _character.gameObject.activeInHierarchy;

    internal static GameObject[] Templates()
    {
        var originals = new Dictionary<int, GameObject>();
        foreach (var character in Resources.FindObjectsOfTypeAll<Character>())
        {
            if (character == null) continue;
            var original = character.PooledElement?.Original;
            if (original == null && !character.gameObject.scene.IsValid()) original = character.gameObject;
            if (original == null) continue;
            var c = original.GetComponent<Character>();
            if (c == null || c.Movement == null || c.Agent?.initialPerson != null) continue;
            originals.TryAdd(original.GetInstanceID(), original);
        }
        return originals.Values.OrderBy(o => o.name, StringComparer.Ordinal).ToArray();
    }

    internal static VendorNpc Spawn(VendorLocation location, GameObject template, PlayerCharacter player)
    {
        var result = new VendorNpc();
        try { result.Create(location, template, player); return result; }
        catch { result.Dispose(); throw; }
    }

    private void Create(VendorLocation location, GameObject template, PlayerCharacter player)
    {
        if (CharacterManager.CharactersPools == null || CharacterManager.CharactersPools.Length < 3)
            throw new InvalidOperationException("Native character pools are not ready.");
        var requested = new Vector3(location.X, location.Y, location.Z);
        if (!NavMesh.SamplePosition(requested, out var nav, .65f, NavMesh.AllAreas) ||
            Math.Abs(nav.position.y - requested.y) > .3f)
            throw new InvalidOperationException("Location has no nearby walkable floor on the same level.");
        var rotation = Quaternion.Euler(0, location.Yaw, 0);
        var idleSource = Resources.FindObjectsOfTypeAll<IdleAction>()
            .FirstOrDefault(a => a != null && a.animation == Character.AnimatorStateType.Idle);
        if (idleSource == null) throw new InvalidOperationException("Native idle animation is not ready.");
        _idle = Object.Instantiate(idleSource);
        _idle.name = "HOBA.Vendor.Idle";
        _idle.matchSnap = false; _idle.snapItem = null; _idle.dialogue = null;
        _idle.dance = false; _idle.randomizePose = false; _idle.pose = 0;
        _task = ScriptableObject.CreateInstance<CharacterTask>();
        _task.name = "HOBA.Vendor.Task";
        _task.type = CharacterTask.TaskType.Loop; _task.checkItem = false;
        _task.actions = new Il2CppReferenceArray<CharacterAction>(new CharacterAction[] { _idle });
        _spotRoot = new GameObject("HOBA.VendorSpot." + location.Id);
        _spotRoot.SetActive(false);
        _spotRoot.transform.SetPositionAndRotation(nav.position, rotation);
        SceneManager.MoveGameObjectToScene(_spotRoot, player.gameObject.scene);
        _spot = _spotRoot.AddComponent<AISpot>();
        _spot.capacity = 1; _spot.minimumReserve = 0; _spot.radius = Vector2.zero;
        _spot.rotationType = Slot.RotationType.Local;
        _spot.thisTransform = _spotRoot.transform; _spot.hasThisTransform = true;
        _spot.points = new Il2CppReferenceArray<AISlot>(new[]
        {
            new AISlot(Vector3.zero, Vector3.zero, 0, Slot.RotationType.Local, false) { main = true }
        });
        _spot.task = _task;
        _spotRoot.SetActive(true);
        var pooled = GameObjectPool.Global.Get(template, nav.position, rotation);
        _character = pooled.GetComponent<Character>();
        if (_character == null) { Object.Destroy(pooled.gameObject); throw new InvalidOperationException("Template has no Character."); }
        _character.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(_character.gameObject, player.gameObject.scene);
        _character.SetSpot(_spot, _spot.points[0]);
        _character.SetFake(_task, true, false);
        _character.ResetActions();
        _character.avalaibleCommands.Clear(); _character.availableTasks.Clear();
        _character.fakeTask = _task; _character.SetTask();
        // Suppress the template's interactions before attaching our own shop.
        if (_character.Interaction != null) _character.Interaction.enabled = false;
        if (_character.DialogueInteraction != null) _character.DialogueInteraction.enabled = false;
        if (_character.Vendor != null) _character.Vendor.enabled = false;
        if (_character.NameDisplay != null) _character.NameDisplay.enabled = false;
        _shop = BoardShop.Attach(_character, location);
        Plugin.Logger.LogInfo($"HOBA vendor {location.Id}: {template.name}, {location.Area}, {nav.position}, yaw {location.Yaw}");
    }

    public void Dispose()
    {
        // Independent cleanup also covers a partial native spawn failure.
        void Clean(Action action) { try { action(); } catch (Exception e) { Plugin.Logger.LogWarning("Vendor cleanup: " + e.Message); } }
        Clean(() => { if (_character != null) { _character.ResetActions(); if (_spot != null) _character.RemoveSpot(_spot); } });
        Clean(() => _shop?.Dispose());
        Clean(() => { if (_character != null) Object.Destroy(_character.gameObject); });
        Clean(() => { if (_spotRoot != null) Object.Destroy(_spotRoot); });
        Clean(() => { if (_task != null) Object.Destroy(_task); });
        Clean(() => { if (_idle != null) Object.Destroy(_idle); });
        _character = null; _spot = null; _spotRoot = null; _task = null; _idle = null; _shop = null;
    }
}
