using System.Numerics;

namespace NivalisMods.Hoba;

// Session-only world location. Area names survive unload/reload; scene handles do not.
internal sealed record ParkingSpot(string Area, Vector3 Ground, Quaternion Rotation)
{
    internal bool IsVisible(string currentArea, bool loading, bool inGame) =>
        inGame && !loading && !string.IsNullOrEmpty(Area) && Area == currentArea;

    internal bool IsNear(Vector3 player)
    {
        var delta = player - Ground;
        return float.IsFinite(delta.LengthSquared()) && Math.Abs(delta.Y) <= .65f &&
            delta.X * delta.X + delta.Z * delta.Z <= 1.6f * 1.6f;
    }
}
