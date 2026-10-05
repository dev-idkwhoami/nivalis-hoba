using System.Numerics;
using System.Globalization;

namespace NivalisMods.Hoba;

internal sealed record RideProfile(float Speed = 9, float Acceleration = 4.5f, float Braking = 12,
    float CoastDeceleration = 3, float TurnRate = 160, float CrouchedTurnRate = 260,
    float CrouchSpeedBonus = .15f, float BankAngle = 8, float CrouchedBankAngle = 14,
    float HoverHeight = .38f, float UnloadedHeight = .65f, float HoverMotionScale = 1);

// Defaults keep managed tools usable without BepInEx; runtime fills these from hoba.cfg before registration.
internal static class BoardTuning
{
    internal static readonly Dictionary<string, RideProfile> Profiles = new();
    internal static readonly Dictionary<string, int> Prices = new();
    internal static readonly Dictionary<string, Dictionary<string, Vector3>> Palettes = new();
    internal static string FormatColor(Vector3 color) => string.Join(", ", new[] { color.X, color.Y, color.Z }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
    internal static bool TryColor(string text, out Vector3 color)
    {
        color = default;
        var parts = text.Split(',');
        if (parts.Length != 3) return false;
        var values = new float[3];
        for (var i = 0; i < 3; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                !float.IsFinite(values[i]) || values[i] < 0 || values[i] > 1) return false;
        color = new(values[0], values[1], values[2]);
        return true;
    }
    internal static RideProfile Profile(string? model) => model != null && Profiles.TryGetValue(model, out var p) ? p : new();
    internal static int Price(string key, int fallback) => Prices.GetValueOrDefault(key, fallback);
    internal static void ApplyPalette(string key, Dictionary<string, Vector3> colors)
    {
        if (Palettes.TryGetValue(key, out var palette))
            foreach (var pair in palette) colors[pair.Key] = pair.Value;
    }
}
