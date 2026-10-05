namespace NivalisMods.Hoba;

// Presentation only; never changes the character collider, heading or movement.
internal static class BoardArticulation
{
    // Normal turns twist ~25 degrees; crouched sharp turns reach 40 degrees.
    internal static float SpineTwist(float turnRate) => -Math.Clamp(turnRate / 260f, -1, 1) * 40f;

    // Platforms steer around their centres. The spine follows the circular arc between
    // their inward-facing sockets, including the socket displacement from platform yaw.
    internal static (float X, float Z, float Yaw) SteeringArc(float z, float turnRate, bool platform)
    {
        var angle = Math.Clamp(turnRate / 260f, -1, 1) * 24f * MathF.PI / 180;
        if (Math.Abs(angle) < .0001f) return (0, z, 0);
        if (platform) return (0, z, Math.Sign(z) * angle * 180 / MathF.PI);
        var socketZ = .405f - .245f * MathF.Cos(angle);
        var radius = socketZ / MathF.Sin(angle);
        var theta = MathF.Asin(Math.Clamp(z / radius, -1, 1));
        var x = -.245f * MathF.Sin(angle) + radius * (MathF.Cos(angle) - MathF.Cos(theta));
        return (x, z, theta * 180 / MathF.PI);
    }

    internal static (float Height, float Roll) Sample(float time, float z, float speed, float bank)
    {
        speed = Math.Clamp(speed, 0, 1);
        bank = Math.Clamp(bank, -25, 25);
        var wave = MathF.Sin(time * 2.4f - z * 5);
        return (wave * (.006f + speed * .008f), wave * .8f + bank * z * .12f);
    }
}
