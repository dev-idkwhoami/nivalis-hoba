namespace NivalisMods.Hoba;

// Add terrain pitch to the player's chosen angle, never back into the baseline.
internal sealed class RidePitch
{
    internal const float LookLimit = 80f;
    private bool _ready;
    private float _baseline, _lastOutput, _offset, _velocity;
    internal void Reset() { _ready = false; _offset = _velocity = 0; }

    internal float WithoutOffset(float current) => _ready ? _baseline + current - _lastOutput : current;

    internal float Step(float current, float forwardSlope, float follow, float dt, float minimum, float maximum)
    {
        if (!float.IsFinite(current + forwardSlope + follow + minimum + maximum) || minimum > maximum) return current;
        if (!_ready) { _baseline = current; _lastOutput = current; _ready = true; }
        // Only changes made outside this helper (mouse/stick input) alter baseline.
        var input = current - _lastOutput;
        // When reversing away from a clipped view, discard the hidden overshoot.
        // With no input, retain the baseline so leaving a slope restores the chosen angle.
        if ((_lastOutput >= maximum && input < 0) || (_lastOutput <= minimum && input > 0))
            _baseline = _lastOutput - _offset;
        _baseline = Math.Clamp(_baseline + input, minimum, maximum);
        var target = -MathF.Atan(forwardSlope) * (180 / MathF.PI) * Math.Clamp(follow, 0, 1);
        if (float.IsFinite(dt) && dt > 0)
        {
            const float omega = 12;
            var step = Math.Min(dt, .05f);
            var error = _offset - target;
            var impulse = _velocity + omega * error;
            var decay = MathF.Exp(-omega * step);
            _offset = target + (error + impulse * step) * decay;
            _velocity = (_velocity - omega * impulse * step) * decay;
        }
        _lastOutput = Math.Clamp(_baseline + _offset, minimum, maximum);
        return _lastOutput;
    }
}
