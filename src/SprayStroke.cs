namespace NivalisMods.Hoba;

internal sealed class SprayStroke
{
    internal const float Duration = .65f;
    private float _elapsed;
    internal bool Active { get; private set; }
    internal float Progress => Math.Clamp(_elapsed / Duration, 0, 1);
    internal void Begin() { if (Active) return; _elapsed = 0; Active = true; }
    internal void Cancel() { Active = false; _elapsed = 0; }
    internal bool Step(float dt, bool valid)
    {
        if (!Active) return false;
        if (!valid) { Cancel(); return false; }
        _elapsed += Math.Max(0, dt);
        if (_elapsed < Duration) return false;
        Active = false;
        return true;
    }
}
