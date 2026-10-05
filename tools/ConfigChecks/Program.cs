using BepInEx.Configuration;
using NivalisMods.Hoba;

var directory = Path.Combine(Path.GetTempPath(), "hoba-config-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "hoba.cfg");
    var file = new ConfigFile(path, false);
    file.Bind("Audio", "BaseVolume", .2f).Value = .317f;
    BoardConfiguration.Load(file);
    if (BoardTuning.Profiles.Count != 12 || BoardTuning.Palettes.Count != 13) throw new Exception("Missing model/paint config sections");
    var text = File.ReadAllText(path);
    if (!text.Contains("[Board.sc-mk2]") || !text.Contains("[Paint.industrial]") || !text.Contains("[Palette.sc-eclipse]")) throw new Exception("Generated config incomplete");
    file.Bind("Board.sc-mk2", "MaxSpeed", 9f).Value = 13;
    file.Bind("Board.sc-mk2", "Price", 3000).Value = 8765;
    file.Bind("Paint.industrial", "Price", 300).Value = 765;
    file.Bind("Palette.industrial", "shell", "").Value = "0.1, 0.2, 0.3";
    file.Bind("Board.fc-mk1", "Braking", 12f).Value = float.NaN;
    file.Bind("Palette.sc-eclipse", "core", "").Value = "invalid";
    file.Bind("Palette.crimson", "shell", "").Value = "0.39, 0.035, 0.045";
    file.Save();
    BoardConfiguration.Load(new ConfigFile(path, true));
    if (BoardTuning.Profile("sc-mk2").Speed != 13 || BoardProducts.Appearance("sc-mk2", "rescue").Price != 8765 ||
        BoardProducts.All.Single(p => p.IsPaint && p.Finish == "industrial").Price != 765) throw new Exception("Persisted customization ignored");
    if (BoardDesigns.Palette(BoardDesigns.Get("hoba-mk1"), "industrial")["shell"] != new System.Numerics.Vector3(.1f, .2f, .3f)) throw new Exception("Palette ignored");
    if (!float.IsFinite(BoardTuning.Profile("fc-mk1").Braking) || BoardTuning.Profile("fc-mk1").Braking < .1f) throw new Exception("Non-finite value accepted");
    if (BoardTuning.Palettes["crimson"]["shell"] != new System.Numerics.Vector3(.75f,.025f,.055f)) throw new Exception("Old paint default was not upgraded");
    var read = new ConfigFile(path, true);
    if (read.Bind("Palette.crimson", "shell", "").Value != "0.75, 0.025, 0.055") throw new Exception("Paint upgrade not saved");
    if (read.Bind("Audio", "BaseVolume", .2f).Value != .317f) throw new Exception("Existing audio overwritten");
    if (BoardProducts.All.Any(p => p.Legendary && p.ForSale)) throw new Exception("Legendary sale restriction lost");
    Console.WriteLine("PASS: actual BepInEx config generation, disk reload, customized prices/stats/colors, invalid-value recovery and existing audio preservation.");
}
finally { Directory.Delete(directory, true); }

namespace NivalisMods.Hoba
{
    internal static class Plugin { internal static readonly TestLogger Logger = new(); }
    internal sealed class TestLogger { internal void LogWarning(string message) => Console.WriteLine(message); }
}
