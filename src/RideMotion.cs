namespace NivalisMods.Hoba;

// Pure simulation shared by the game and checks. Metres, seconds, degrees.
internal sealed class RideMotion
{
    internal const float MaxSpeed = 9f;
    internal const float HoverHeight = .38f;
    internal const float UnloadedHeight = .65f;
    private readonly RideProfile _profile;
    internal RideMotion(RideProfile? profile = null) => _profile = profile ?? new();
    internal float Forward { get; private set; }
    internal bool Accelerating { get; private set; }
    internal float Bank { get; private set; }
    internal float Crouch { get; private set; }
    internal float YawDelta { get; private set; }
    internal float TurnRate => _profile.TurnRate + (_profile.CrouchedTurnRate - _profile.TurnRate) * Crouch;
    internal float SpeedLimit => _profile.Speed * (1 + _profile.CrouchSpeedBonus * Crouch);
    internal float Phase { get; private set; }
    internal float SpeedRatio => Math.Clamp(Math.Abs(Forward) / _profile.Speed, 0, 1);
    internal float Hover => MathF.Sin(Phase) * (.012f + .033f * SpeedRatio) * _profile.HoverMotionScale;
    internal float Sway => MathF.Sin(Phase * .5f) * .012f * SpeedRatio;
    internal float Pulse => .8f + .2f * MathF.Sin(Phase * 1.7f);

    internal void Step(float forwardInput, float turnInput, float deltaTime, bool crouching = false)
    {
        YawDelta = 0;
        Accelerating = false;
        var previousSpeed = Math.Abs(Forward);
        if (!float.IsFinite(deltaTime) || deltaTime <= 0) return;
        var dt = Math.Min(deltaTime, .05f);
        var forward = Input(forwardInput);
        var turn = Input(turnInput);
        Crouch = Approach(Crouch, crouching ? 1 : 0, dt * 4);
        var target = forward * SpeedLimit;
        if (target * Forward < 0)
        {
            var stopTime = Math.Abs(Forward) / _profile.Braking;
            Forward = stopTime >= dt ? Approach(Forward, 0, _profile.Braking * dt)
                : Approach(0, target, _profile.Acceleration * (dt - stopTime));
        }
        else
        {
            var rate = Math.Abs(target) > Math.Abs(Forward) ? _profile.Acceleration : _profile.CoastDeceleration;
            Forward = Approach(Forward, target, rate * dt);
        }
        Forward = Math.Clamp(Forward, -SpeedLimit, SpeedLimit);
        Accelerating = Math.Abs(Forward) > previousSpeed + .00001f;
        YawDelta = turn * TurnRate * dt;
        Bank = Approach(Bank, -turn * (_profile.BankAngle + (_profile.CrouchedBankAngle - _profile.BankAngle) * Crouch) * SpeedRatio, 45f * dt);
        Animate(dt);
    }

    // Preserve signed momentum when the rider steps off, including the crouch bonus.
    // Return exact integrated travel while friction brings the board to rest.
    internal float Coast(float deltaTime)
    {
        Accelerating = false;
        if (!float.IsFinite(deltaTime) || deltaTime <= 0) return 0;
        var dt = Math.Min(deltaTime, .05f);
        var movingTime = Math.Min(dt, Math.Abs(Forward) / _profile.CoastDeceleration);
        var next = Approach(Forward, 0, _profile.CoastDeceleration * dt);
        var distance = (Forward + next) * .5f * movingTime;
        Forward = next;
        Crouch = Approach(Crouch, 0, dt * 4);
        Bank = Approach(Bank, 0, 45f * dt);
        YawDelta = 0;
        Animate(dt);
        return distance;
    }

    private void Animate(float dt) => Phase = (Phase + dt * MathF.Tau * (.85f + .75f * SpeedRatio)) % (MathF.Tau * 10);
    private static float Input(float value) => float.IsFinite(value) ? Math.Clamp(value, -1, 1) : 0;
    internal void Blocked() { Forward = 0; Accelerating = false; }
    internal static float Approach(float value, float target, float step) =>
        value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);
}
