namespace NivalisMods.Hoba;

// Visual suspension only. Native player collision/gravity remain authoritative.
internal sealed class GroundResponse
{
    internal float Height { get; private set; }
    internal float ForwardSlope { get; private set; }
    internal float CrossSlope { get; private set; }
    private bool _ready;
    internal static float Median(float a, float b, float c) => Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
    internal void Reset() => _ready = false;
    internal void Step(float height, float forward, float cross, float dt)
    {
        if (!float.IsFinite(height + forward + cross)) return;
        forward = Math.Clamp(forward, -1, 1);
        cross = Math.Clamp(cross, -1, 1);
        // Ignore differences below roughly 3 degrees; retain the slope above that.
        if (Math.Abs(forward) < .055f) forward = 0;
        if (Math.Abs(cross) < .055f) cross = 0;
        if (!_ready) { Height = height; ForwardSlope = forward; CrossSlope = cross; _ready = true; return; }
        if (!float.IsFinite(dt) || dt <= 0) return;
        var blend = 1 - MathF.Exp(-6 * Math.Min(dt, .05f));
        // Continuous soft dead zone: never reset merely because a fast slope
        // outruns the filter. Reset is reserved for explicit mount/session changes.
        var error = height - Height;
        var filteredError = MathF.CopySign(Math.Max(0, Math.Abs(error) - .012f), error);
        Height += filteredError * blend;
        ForwardSlope += (forward - ForwardSlope) * blend;
        CrossSlope += (cross - CrossSlope) * blend;
    }
}
