using System.Numerics;

namespace NivalisMods.Hoba;

// Recolor occupied surface cells instead of laying coplanar decals over the mesh.
// This creates true voxel-sized paint boundaries with no overlapping faces.
internal static class LegendaryGraffiti
{
    internal static void Apply(List<BoardPart> parts, BoardDesign design)
    {
        var solid = new Dictionary<(int X, int Y, int Z), BoardPart>();
        foreach (var part in parts)
            foreach (var cell in part.Cells) solid[cell] = part;
        var paint = new Dictionary<(BoardPart Part, string Finish), BoardPart>();
        foreach (var (c, owner) in solid)
        {
            if (owner.Finish is "cyan" or "core" or "signal" || owner.Name.StartsWith("Thruster") || c.Y < 0) continue;
            var top = !solid.ContainsKey((c.X, c.Y + 1, c.Z));
            var side = Math.Abs(c.X) > 19 && !solid.ContainsKey((c.X + Math.Sign(c.X), c.Y, c.Z));
            if (!top && !side) continue;
            var z = c.Z - Math.Sign(c.Z) * (design.Family == "hoba" ? 35 : 40);
            var x = c.X;
            string? color = null;
            if (top && Math.Abs(x) < 15 && Math.Abs(z) < 15)
            {
                if (design.Family == "hoba")
                {
                    if ((x + z * 2 + 80) % 19 < 4) color = "paintA";
                    if (c.Z < 0 && Math.Abs(x + z) < 2) color = "paintB";
                    if (c.Z > 0 && Skull(x + 6, z + 6)) color = "paintPaper";
                }
                else if (design.Family == "sc")
                {
                    var r = x*x + z*z;
                    if (c.Z < 0 && ((r > 65 && r < 105 && x+z>-12) || r<5)) color = "paintA";
                    if (c.Z > 0 && (Math.Abs(z - x/2) < 2 || Math.Abs(z - x/2 - 7) < 2)) color = "paintB";
                }
                else
                {
                    if (c.Z < 0 && ((x+4)*(x+4)+(z+3)*(z+3) is > 18 and < 42 || Math.Abs(z-x/2-5)<2 && x>0)) color = "paintA";
                    if (c.Z > 0 && Math.Abs(z - (int)(5*MathF.Sin(x*.22f))) < 2) color = "paintB";
                }
            }
            // Tags, square stickers and short drips on exposed side panels/shoulders.
            var hash = unchecked(c.X*73856093 ^ c.Y*19349663 ^ c.Z*83492791) & 0x7fffffff;
            if (side)
            {
                if (Math.Abs(z)<7 && c.Y is >= 1 and <= 5) color = "paintPaper";
                if (Math.Abs(z)<5 && c.Y is >= 2 and <= 4 && (z+c.Y)%3!=0) color = "paintInk";
                if ((c.Z+90)%23<3 && c.Y<8) color = c.X>0?"paintA":"paintB";
            }
            if (top && Math.Abs(z)>15 && Math.Abs(z)<24 && (x+z+100)%11<2) color = "paintB";
            if (owner.Name.StartsWith("Spine") && top && (c.Z+100)%5<2) color = "paintA";
            if (color != null && hash%17==0) color = null; // worn gaps in painted motifs
            if (color == null && hash%151==0) color = "paintPaper";
            if (color == null) continue;
            var key = (owner, color);
            if (!paint.TryGetValue(key, out var part)) paint[key] = part = new BoardPart(owner.Name + " graffiti " + color, color);
            part.Cells.Add(c);
        }
        parts.AddRange(paint.Values);
    }

    private static bool Skull(int x, int z)
    {
        string[] mask = { "00111111100", "01111111110", "11111111111", "11001110011", "11001110011", "11111011111", "01111111110", "00101010100", "00101010100" };
        return z>=0 && z<mask.Length && x>=0 && x<mask[0].Length && mask[z][x]=='1';
    }
}
