using BepInEx.Configuration;
using NivalisMods.ModCompanion;
using NivalisMods.ModCompanion.Api;

namespace NivalisMods.Hoba;

internal static class CompanionSettings
{
    private static Setting<bool>? _hotkeyEnabled;
    private static KeyBindingSetting? _takeOutBoard;

    internal static bool TakeOutBoardPressed => _hotkeyEnabled?.Value == true &&
        _takeOutBoard != null && CompanionInput.WasPressed(_takeOutBoard);

    internal static void Register(ConfigFile config)
    {
        var mod = SettingsRegistry.Register(Plugin.Id,
            new ModMetadata("HOBA", "dev_idkwhoami", Plugin.Version,
                "Hoverboards, custom finishes and hidden legendary boards.",
                Icon: ModIcon.FromResource(typeof(Plugin).Assembly, "Hoba.BoardIcon.png")), config);
        var general = mod.AddCategory("General", "General");
        _hotkeyEnabled = general.Toggle(
            "EnableBoardHotkey", "Enable board hotkey", true,
            "Allow the Take out board binding to deploy and mount an owned board. Inventory use remains available when disabled.");
        _takeOutBoard = mod.Controls.AddInput("TakeOutBoard", "Take out board", "<Keyboard>/q");
        BoardFov.Configure(mod.AddCategory("Camera", "Camera"));
        BoardAudio.Configure(mod.AddCategory("Audio", "Audio"));
        LegendaryCompass.Configure(general, config);
        BoardCompass.Configure(general, config);
    }
}
