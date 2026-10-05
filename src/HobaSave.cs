using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using Nivalis;
using UnityEngine;

namespace NivalisMods.Hoba;

// Runtime progress belongs alongside game saves, never in the authoring/config folder.
// Content-addressed checkpoints isolate save slots, older saves, and manual copies.
internal static class HobaSave
{
    internal static HobaCheckpoint State = new();
    internal static bool Ready = true;
    internal static bool RestorePending;
    private static string? _pending;
    private static readonly Il2CppSystem.Action Loaded = (Il2CppSystem.Action)Load;
    internal static void Initialize() => SerializationManager.OnPostLoad.Add(Loaded);
    internal static void EndSession() { _pending = null; Reset(); }
    internal static void Reset()
    {
        State = new(); Ready = _pending == null; RestorePending = false;
        HiddenChests.Instance?.Clear();
    }
    internal static void Begin(string name) { _pending = name; Reset(); Ready = false; }
    private static string PathFor(string name)
    {
        using var file = File.OpenRead(Path.Combine(Application.persistentDataPath, name + ".sav"));
        using var sha = SHA256.Create();
        return Path.Combine(Application.persistentDataPath, "HOBA", Convert.ToHexString(sha.ComputeHash(file)) + ".bin");
    }
    private static void Load()
    {
        try
        {
            var name = _pending; _pending = null;
            State = new();
            if (name != null)
            {
                var path = PathFor(name);
                if (File.Exists(path)) State = HobaCheckpointBinary.Decode(File.ReadAllBytes(path));
                else
                {
                    // Read 0.1.26 checkpoints without discarding existing progress.
                    var legacy = Path.ChangeExtension(path, ".json");
                    if (File.Exists(legacy))
                        State = JsonSerializer.Deserialize<HobaCheckpoint>(File.ReadAllText(legacy)) ?? throw new InvalidDataException("Empty HOBA checkpoint.");
                }
            }
            State.Validate();
            RestorePending = State.Board != null;
            Ready = true;
        }
        catch (Exception e) { Ready = false; Plugin.Logger.LogError("HOBA progress load failed; progress kept untouched: " + e); }
    }
    internal static void Save(string name)
    {
        if (!Ready) return;
        try
        {
            if (!RestorePending) State.Board = RideController.Instance?.CaptureParking();
            State.Validate();
            var path = PathFor(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path + ".tmp", HobaCheckpointBinary.Encode(State));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("HOBA progress checkpoint failed: " + e);
            BoardItem.Notify("Game saved, but HOBA world progress could not be saved. See log.");
        }
    }
}

[HarmonyPatch(typeof(SerializationManager), nameof(SerializationManager.Load))]
internal static class HobaLoadHook { private static void Prefix(string saveName) => HobaSave.Begin(saveName); }
[HarmonyPatch(typeof(SerializationManager), nameof(SerializationManager.Clear))]
internal static class HobaClearHook { private static void Prefix() => HobaSave.Reset(); }
[HarmonyPatch(typeof(SerializationManager), nameof(SerializationManager.Save))]
internal static class HobaSaveHook { private static void Postfix(string saveName, bool __result) { if (__result) HobaSave.Save(saveName); } }

[HarmonyPatch(typeof(GameSceneManager), nameof(GameSceneManager.EndGame))]
internal static class HobaEndSessionHook { private static void Prefix() => HobaSave.EndSession(); }
