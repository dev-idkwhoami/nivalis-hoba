namespace NivalisMods.Hoba;

// Continuous relative yaw avoids jumping across the forbidden left/back sector
// when either the board or camera crosses the world's +/-180-degree seam.
internal sealed class RideLook
{
    internal const float LeftLimit = -45;
    internal const float RightLimit = 180;
    internal float Relative { get; private set; }
    private float _lastView, _lastBoard;
    private bool _ready;
    private float _quietTime, _yawVelocity;
    internal float AutoFollow { get; private set; }

    internal void ObserveInput(bool hasLookInput, float dt)
    {
        if (hasLookInput) { _quietTime = 0; AutoFollow = 0; _yawVelocity = 0; return; }
        if (!float.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, .05f);
        _quietTime += dt;
        if (_quietTime >= 1) AutoFollow = RideMotion.Approach(AutoFollow, 1, dt * 2);
    }

    internal void Reset() { _ready = false; _quietTime = 0; AutoFollow = 0; _yawVelocity = 0; }
    internal float Step(float view, float board, float turnStrength, float dt)
    {
        if (!float.IsFinite(view + board)) return _lastView;
        var strength = Math.Max(AutoFollow, float.IsFinite(turnStrength) ? Math.Clamp(turnStrength, 0, 1) : 0);
        if (!_ready)
        {
            const float middle = (LeftLimit + RightLimit) / 2;
            Relative = middle + Delta(middle, view - board);
            _ready = true;
        }
        else Relative += Delta(_lastView, view) - Delta(_lastBoard, board);
        Relative = Math.Clamp(Relative, LeftLimit, RightLimit);
        // Critically damped pursuit of the current heading, in world space.
        // No direct board-yaw transfer and no queue of past turn destinations.
        // Retaining velocity softens rapid keyboard reversals as well as starts.
        if (strength <= 0) _yawVelocity = 0;
        else if (float.IsFinite(dt) && dt > 0)
        {
            var step = Math.Min(dt, .05f);
            var omega = 18 * strength;
            var decay = MathF.Exp(-omega * step);
            var impulse = _yawVelocity + omega * Relative;
            Relative = (Relative + impulse * step) * decay;
            _yawVelocity = (_yawVelocity - omega * impulse * step) * decay;
        }
        var constrained = Math.Clamp(Relative, LeftLimit, RightLimit);
        if (constrained != Relative) _yawVelocity = 0;
        Relative = constrained;
        _lastBoard = board;
        _lastView = board + Relative;
        return _lastView;
    }

    internal static float Delta(float from, float to)
    {
        var d = (to - from) % 360;
        if (d > 180) d -= 360;
        if (d < -180) d += 360;
        return d;
    }
}
