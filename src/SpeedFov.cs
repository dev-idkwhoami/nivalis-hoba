namespace NivalisMods.Hoba;

// Smooth a bonus, never the player's underlying FOV setting.
internal sealed class SpeedFov
{
    internal float Bonus { get; private set; }
    internal float Step(float speedRatio, float maximum, float dt)
    {
        var ratio = float.IsFinite(speedRatio) ? Math.Clamp(speedRatio, 0, 1) : 0;
        var target = (float.IsFinite(maximum) ? Math.Clamp(maximum, 0, 30) : 0) * ratio * ratio * (3 - 2 * ratio);
        if (float.IsFinite(dt) && dt > 0)
            Bonus += (target - Bonus) * (1 - MathF.Exp(-Math.Min(dt, .1f) / .3f));
        return Bonus;
    }
    internal void Reset() => Bonus = 0;
}
