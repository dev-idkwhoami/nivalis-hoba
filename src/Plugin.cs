using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace NivalisMods.Hoba;

[BepInPlugin(Id, "HOBA", Version)]
[BepInDependency(NivalisMods.ModCompanion.Api.SettingsRegistry.PluginId, ">=1.0.3")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "local.nivalis.hoba";
    public const string Version = "1.0.0";
    internal static ManualLogSource Logger = null!;
    private Harmony? _patches;

    public override void Load()
    {
        Logger = Log;
        var config = new ConfigFile(Path.Combine(Paths.ConfigPath, "hoba.cfg"), true);
        BoardConfiguration.Load(config);
        CompanionSettings.Register(config);
        HobaSave.Initialize();
        _patches = new Harmony(Id);
        _patches.PatchAll(typeof(Plugin).Assembly);
        AddComponent<RideController>();
        AddComponent<VendorController>();
        AddComponent<HiddenChests>();
        Log.LogInfo($"HOBA {Version} loaded. Settings: hoba.cfg and Mod Companion.");
    }

    public override bool Unload()
    {
        if (HiddenChests.Instance != null)
        {
            HiddenChests.Instance.Clear();
            UnityEngine.Object.Destroy(HiddenChests.Instance);
        }
        var controller = RideController.Instance;
        if (controller != null)
        {
            controller.ResetBoard("plugin unloaded");
            UnityEngine.Object.Destroy(controller);
        }
        if (VendorController.Instance != null)
        {
            VendorController.Instance.Clear();
            UnityEngine.Object.Destroy(VendorController.Instance);
        }
        BoardFov.Reset();
        LegendaryCompass.Dispose();
        _patches?.UnpatchSelf();
        return true;
    }
}
