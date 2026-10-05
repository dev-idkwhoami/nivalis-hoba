using BepInEx.Configuration;
using Vector3 = System.Numerics.Vector3;

namespace NivalisMods.Hoba;

internal static class BoardConfiguration
{
    internal static void Load(ConfigFile config)
    {
        var autoSave = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        try
        {
            BoardTuning.Profiles.Clear(); BoardTuning.Prices.Clear(); BoardTuning.Palettes.Clear();
            foreach (var design in BoardDesigns.All)
            {
                var section = "Board." + design.Id;
                float Number(string key, float value, float min, float max, string units) => Bind(config, section, key, value, min, max, units);
                var d = new RideProfile();
                BoardTuning.Profiles[design.Id] = new RideProfile(
                    Number("MaxSpeed", d.Speed, 1, 40, "Forward/reverse speed in metres per second."),
                    Number("Acceleration", d.Acceleration, .1f, 40, "Acceleration in metres per second squared."),
                    Number("Braking", d.Braking, .1f, 60, "Braking before reversing, in metres per second squared."),
                    Number("CoastDeceleration", d.CoastDeceleration, .1f, 30, "Deceleration with no throttle, including after dismount."),
                    Number("TurnRate", d.TurnRate, 10, 360, "Standing turn rate in degrees per second."),
                    Number("CrouchedTurnRate", d.CrouchedTurnRate, 10, 540, "Crouched turn rate in degrees per second."),
                    Number("CrouchSpeedBonus", d.CrouchSpeedBonus, 0, 1, "Additional speed fraction while crouched; 0.15 means 15 percent."),
                    Number("BankAngle", d.BankAngle, 0, 35, "Standing banking angle in degrees at full speed."),
                    Number("CrouchedBankAngle", d.CrouchedBankAngle, 0, 45, "Crouched banking angle in degrees at full speed."),
                    Number("HoverHeight", d.HoverHeight, .15f, 1.2f, "Loaded hover height above ground in metres."),
                    Number("UnloadedHeight", d.UnloadedHeight, .15f, 1.5f, "Parked hover height above ground in metres."),
                    Number("HoverMotionScale", d.HoverMotionScale, 0, 3, "Multiplier for vertical hover animation."));
                BoardTuning.Prices[design.Id] = config.Bind(section, "Price", 2000 + (design.Mark - 1) * 1000,
                    new ConfigDescription("Base price before native economy adjustments. Does not make legendary boards purchasable. Restart required.", new AcceptableValueRange<int>(0, 1000000))).Value;
            }
            foreach (var finish in BoardDesigns.Finishes)
                BoardTuning.Prices["paint." + finish.Id] = config.Bind("Paint." + finish.Id, "Price", 300,
                    new ConfigDescription("Consumable paint base price before economy adjustments. Restart required.", new AcceptableValueRange<int>(0, 1000000))).Value;

            // Shared neutral/paint palettes across marks; each legendary has its own palette.
            var palettes = new Dictionary<string, Dictionary<string, Vector3>> {
                ["neutral"] = BoardDesigns.Palette(BoardDesigns.Get(BoardDesigns.DefaultId))
            };
            foreach (var finish in BoardDesigns.Finishes)
                palettes[finish.Id] = BoardDesigns.Palette(BoardDesigns.All.First(d => d.Family == finish.Family && !d.Legendary), finish.Id);
            foreach (var design in BoardDesigns.All.Where(d => d.Legendary)) palettes[design.Id] = BoardDesigns.Palette(design);
            foreach (var (name, palette) in palettes)
            {
                var configured = new Dictionary<string, Vector3>();
                foreach (var (part, color) in palette)
                {
                    var entry = config.Bind("Palette." + name, part, BoardTuning.FormatColor(color),
                        "RGB as three comma-separated channels from 0 to 1, e.g. 0.24, 0.29, 0.085. Applies to board/spray-can materials, not baked inventory thumbnails. Restart required.");
                    if (!BoardTuning.TryColor(entry.Value, out var configuredColor))
                    {
                        configuredColor = color; entry.Value = BoardTuning.FormatColor(color);
                        Plugin.Logger.LogWarning("Reset invalid palette color: " + name + "/" + part);
                    }
                    if (BoardDesigns.IsPreviousPaintColor(name, part, configuredColor) && configuredColor != color)
                    {
                        configuredColor = color;
                        entry.Value = BoardTuning.FormatColor(color);
                    }
                    configured[part] = configuredColor;

                }
                BoardTuning.Palettes[name] = configured;
            }
            BoardProducts.ApplyConfiguredPrices();
            config.Save();
        }
        finally { config.SaveOnConfigSet = autoSave; }
    }

    private static float Bind(ConfigFile config, string section, string key, float value, float min, float max, string description)
    {
        var entry = config.Bind(section, key, value, new ConfigDescription(description + " Restart required.", new AcceptableValueRange<float>(min, max)));
        if (!float.IsFinite(entry.Value)) { entry.Value = value; Plugin.Logger.LogWarning("Reset non-finite setting: " + section + "/" + key); }
        return entry.Value;
    }
}
