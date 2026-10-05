using BepInEx.Configuration;
using NivalisMods.ModCompanion.Api;

namespace NivalisMods.Hoba;

internal static class CompanionSettings
{
    internal static void Register(ConfigFile config)
    {
        var mod = SettingsRegistry.Register(Plugin.Id,
            new ModMetadata("HOBA", "dev_idkwhoami", Plugin.Version,
                "Hoverboards, custom finishes and hidden legendary boards.",
                Icon: ModIcon.FromResource(typeof(Plugin).Assembly, "Hoba.BoardIcon.png")), config);
        BoardFov.Configure(mod.AddCategory("Camera", "Camera"));
        BoardAudio.Configure(mod.AddCategory("Audio", "Audio"));
        var discovery = mod.AddCategory("Discovery", "Discovery");
        LegendaryCompass.Configure(discovery);
        BoardCompass.Configure(discovery);
    }
}
