using System.Numerics;

namespace NivalisMods.Hoba;

internal static class PaintGeometry
{
    internal static BoardPart[] Create()
    {
        var parts = new List<BoardPart>();
        void Box(string name,string finish,Vector3 p,Vector3 size) => parts.Add(BoardGeometry.Box(name,finish,p,size,.015f));
        // Circular voxel cross-sections retain the game's stepped silhouette.
        void Round(string name, string finish, float radius, float y, float height)
        {
            for (var z = -radius; z < radius; z += .01f)
            {
                var halfWidth = MathF.Floor(MathF.Sqrt(radius * radius - (z + .005f) * (z + .005f)) / .01f) * .01f;
                if (halfWidth > 0) Box(name, finish, new(0, y, z + .005f), new(halfWidth * 2, height, .01f));
            }
        }
        Round("Can base", "metal", .08f, -.115f, .02f);
        Round("Paint cartridge", "shell", .075f, .025f, .26f);
        Round("Paint label", "yellow", .08f, .025f, .12f);
        Round("Lid rim", "metal", .08f, .165f, .02f);
        Round("Shoulder", "edge", .06f, .185f, .02f);
        Box("Nozzle", "edge", new(0, .21f, 0), new(.04f, .03f, .04f));
        Box("Nozzle port", "grip", new(.025f, .21f, 0), new(.01f, .01f, .02f));
        return BoardGeometry.Surface(parts);
    }
}
