namespace NivalisMods.Hoba;

// An envelope, not stacked acceleration events: AD/input jitter cannot build volume.
internal sealed class HoverVolume
{
    internal float Value { get; private set; }
    internal void Step(bool accelerating, float baseVolume, float boost, float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0) return;
        var normal = float.IsFinite(baseVolume) ? Math.Clamp(baseVolume, 0, 1) : .05f;
        var extra = float.IsFinite(boost) ? Math.Clamp(boost, 0, 1) : .02f;
        var target = Math.Clamp(normal + (accelerating ? extra : 0), 0, 1);
        var tau = target > Value ? .12f : .7f;
        Value += (target - Value) * (1 - MathF.Exp(-dt / tau));
    }
}
