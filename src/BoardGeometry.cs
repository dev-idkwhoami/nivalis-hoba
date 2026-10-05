using System.Numerics;

namespace NivalisMods.Hoba;

internal sealed class BoardPart
{
    internal string Name { get; }
    internal string Finish { get; }
    internal HashSet<(int X, int Y, int Z)> Cells { get; } = new();
    internal List<Vector3> Vertices { get; } = new();
    internal List<int> Triangles { get; } = new();
    internal BoardPart(string name, string finish) { Name = name; Finish = finish; }
    internal void Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var i = Vertices.Count;
        Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
        Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
    }
    internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Triangle(a, b, c); Triangle(a, c, d);
    }
}

// Authoring data also exports to OBJ for inspection. +Z is the board's nose.
// Flat-shaded geometry, stepped edges, no textures or external asset bundles.
internal static class BoardGeometry
{
    internal static readonly Dictionary<string, Vector3> Colors = new()
    {
        ["shell"] = new(.24f, .29f, .085f), ["edge"] = new(.48f, .49f, .43f),
        ["grip"] = new(.025f, .029f, .026f), ["metal"] = new(.105f, .12f, .10f),
        ["cyan"] = new(.025f, .55f, 1f), ["core"] = new(.78f, .95f, 1f),
        ["mark"] = new(.79f, .80f, .69f), ["yellow"] = new(.95f, .57f, .045f),
        ["red"] = new(.55f, .045f, .025f), ["copper"] = new(.40f, .22f, .10f),
        ["signal"] = new(.65f, .025f, .015f)
    };

    internal static BoardPart[] Create()
    {
        var parts = new List<BoardPart>();
        void Add(string name, string finish, Vector3 p, Vector3 size, float cut = .002f) =>
            parts.Add(Box(name, finish, p, size, Math.Min(cut, Math.Min(size.X, size.Z) * .24f)));
        void Bolt(string id, float x, float y, float z)
        {
            Add("Bolt " + id, "edge", new(x, y, z), new(.013f, .006f, .013f), .003f);
        }
        void Cable(string id, string finish, float diameter, params Vector3[] points)
        {
            for (var i = 1; i < points.Length; i++)
                parts.Add(Beam("Cable " + id + " segment " + i, finish, points[i - 1], points[i], diameter));
            foreach (var p in new[] { points[0], points[^1] })
                Add("Cable connector " + id, "metal", p, new(diameter * 2, diameter * 1.6f, diameter * 2), diameter * .3f);
        }

        parts.Add(Deck("Deck rim", "edge", .255f, .72f, -.046f, -.012f));
        parts.Add(Deck("Deck shell", "shell", .247f, .704f, -.012f, .045f));
        parts.Add(Deck("Undertray", "metal", .226f, .678f, -.07f, -.046f));

        // Two clear standing areas, surrounded by replaceable armor and service hardware.
        foreach (var z in new[] { -.325f, .325f })
        {
            Add("Grip surround " + z, "yellow", new(0, .051f, z), new(.333f, .015f, .313f), .024f);
            Add("Grip pad " + z, "grip", new(0, .061f, z), new(.303f, .011f, .28f), .022f);
            for (var i = -5; i <= 5; i++)
            {
                Add("Grip tread " + z + " " + i, "metal", new(0, .075f, z + i * .023f), new(.27f, .010f, .010f));
                // Broken, worn metallic edges rather than a uniform smooth rubber slab.
                if (i % 3 == 0) Add("Tread wear " + z + " " + i, "edge", new(.075f, .085f, z + i * .023f), new(.034f, .010f, .009f));
            }
            foreach (var x in new[] { -.155f, .155f })
                foreach (var dz in new[] { -.137f, .137f }) Bolt("footplate", x, .061f, z + dz);
        }

        // Segmented side armor: inset vents, narrow ribs, exposed screws and caution tabs.
        foreach (var side in new[] { -1, 1 })
        {
            for (var i = -4; i <= 4; i++)
            {
                var z = i * .119f;
                Add("Side armor " + side + " " + i, "shell", new(side * .239f, .015f, z), new(.030f, .075f, .108f), .008f);
                Add("Side seam " + side + " " + i, "metal", new(side * .252f, .003f, z), new(.008f, .030f, .078f));
                for (var j = -2; j <= 2; j++)
                    Add("Side vent " + side + " " + i + " " + j, "edge", new(side * .265f, .004f, z + j * .012f), new(.010f, .021f, .004f), .0008f);
                Add("Top armor segment " + side + " " + i, i % 3 == 0 ? "edge" : "shell",
                    new(side * .204f, .057f, z), new(.056f, .024f, .104f), .006f);
                Bolt("side " + side + " " + i, side * .211f, .073f, z - .030f);
                Add("Caution tab " + side + " " + i, "yellow", new(side * .241f, .054f, z + .025f), new(.019f, .007f, .024f));
            }
            Add("Bumper rail " + side, "metal", new(side * .238f, -.045f, 0), new(.022f, .017f, 1.13f), .006f);
            // Deliberately asymmetric cable routes with visible elbows and retaining clips.
            var zz = side < 0 ? -.08f : .07f;
            Cable("deck feed " + side, "red", .013f,
                new(side * .083f, .086f, zz), new(side * .179f, .086f, zz),
                new(side * .181f, .086f, side * .51f), new(side * .116f, .10f, side * .56f));
            Cable("auxiliary " + side, "copper", .009f,
                new(side * .063f, .074f, -zz), new(side * .160f, .08f, -zz),
                new(side * .168f, .080f, -side * .48f), new(side * .10f, .093f, -side * .55f));
            for (var i = -3; i <= 3; i++)
                Add("Harness clip " + side + " " + i, "edge", new(side * .18f, .094f, i * .13f), new(.030f, .006f, .012f));
        }

        // Dense center service bay: layered frame, heat sink, contact bank and exposed harnesses.
        Add("Service bay gasket", "grip", new(0, .052f, 0), new(.31f, .018f, .28f), .026f);
        Add("Service bay frame", "yellow", new(0, .065f, 0), new(.284f, .014f, .255f), .024f);
        Add("Service bay plate", "edge", new(0, .074f, 0), new(.258f, .012f, .231f), .020f);
        Add("Controller module", "grip", new(-.021f, .097f, .012f), new(.14f, .042f, .139f), .008f);
        for (var i = -4; i <= 4; i++)
            Add("Controller cooling fin " + i, "metal", new(-.021f, .123f, .012f + i * .014f), new(.15f, .014f, .006f));
        for (var i = 0; i < 5; i++)
        {
            Add("Contact socket " + i, "grip", new(.10f, .086f, -.07f + i * .032f), new(.027f, .02f, .023f));
            Add("Brass contact " + i, "yellow", new(.10f, .099f, -.07f + i * .032f), new(.011f, .005f, .014f));
        }
        Add("Controller readout", "cyan", new(-.022f, .111f, -.089f), new(.060f, .006f, .018f));
        foreach (var x in new[] { -.115f, .115f })
            foreach (var z in new[] { -.097f, .097f }) Bolt("service", x, .086f, z);

        // Nose and tail carry different maintenance modules, serial markings and bumper ribs.
        foreach (var end in new[] { -1, 1 })
        {
            Add("End service plate " + end, "edge", new(0, .084f, end * .57f), new(.292f, .016f, .16f), .020f);
            foreach (var x in new[] { -.112f, .112f })
                foreach (var z in new[] { .52f, .62f })
                    Add("End plate standoff " + end, "metal", new(x, .061f, end * z), new(.019f, .04f, .018f));
            Add("End service box " + end, "grip", new(end * .047f, .108f, end * .565f), new(.135f, .036f, .080f), .007f);
            for (var j = -3; j <= 3; j++)
                Add("End box fins " + end + " " + j, "metal", new(end * .047f, .129f, end * .565f + j * .010f), new(.14f, .006f, .003f), .0006f);
            Add("Module badge " + end, "edge", new(end * .047f, .134f, end * .565f), new(.038f, .006f, .039f));
            foreach (var dx in new[] { -.011f, .011f })
                Add("H badge stroke " + end, "mark", new(end * .047f + dx, .138f, end * .565f), new(.004f, .002f, .026f), .0007f);
            Add("H badge bridge " + end, "mark", new(end * .047f, .138f, end * .565f), new(.023f, .002f, .005f), .0007f);
            for (var j = 0; j < 5; j++)
            {
                Add("End vent " + end + " " + j, "metal", new(-end * .095f, .095f, end * (.533f + j * .018f)), new(.058f, .009f, .008f));
                Add("Bumper rib " + end + " " + j, "edge", new(0, .035f + j * .013f, end * (.694f - j * .009f)), new(.244f + j * .013f, .008f, .014f));
            }
            foreach (var x in new[] { -.112f, .112f }) Bolt("end plate", x, .096f, end * .621f);
            Add("End lamp surround " + end, "metal", new(0, .034f, end * .711f), new(.091f, .022f, .012f));
            Add("End lamp " + end, end > 0 ? "core" : "signal", new(0, .034f, end * .718f), new(.065f, .011f, .003f), .0007f);
        }

        Add("Battery spine", "metal", new(0, -.107f, 0), new(.245f, .075f, .68f), .025f);
        for (var i = -3; i <= 3; i++)
        {
            Add("Battery armor " + i, "shell", new(0, -.151f, i * .083f), new(.213f, .015f, .070f), .008f);
            Add("Battery strap " + i, "edge", new(0, -.162f, i * .083f), new(.24f, .009f, .012f));
            Add("Battery caution " + i, "yellow", new(.066f, -.162f, i * .083f + .021f), new(.043f, .008f, .009f));
        }

        // Recessed jets: the entire collar fits inside the 51 cm deck width.
        foreach (var x in new[] { -.145f, .145f })
            foreach (var z in new[] { -.47f, .47f })
            {
                var id = (x < 0 ? "L" : "R") + (z < 0 ? " rear" : " front");
                Add("Thruster " + id, "shell", new(x, -.10f, z), new(.189f, .108f, .234f), .025f);
                for (var ring = 0; ring < 3; ring++)
                    Add("Thruster rib " + id + ring, ring == 1 ? "yellow" : "metal", new(x, -.095f - ring * .025f, z), new(.200f, .009f, .246f), .026f);
                Add("Thruster collar " + id, "edge", new(x, -.162f, z), new(.205f, .026f, .251f), .030f);
                Add("Nozzle well " + id, "grip", new(x, -.185f, z), new(.173f, .010f, .216f), .025f);
                Add("Blue nozzle " + id, "cyan", new(x, -.195f, z), new(.146f, .010f, .185f), .024f);
                Add("White core " + id, "core", new(x, -.205f, z), new(.089f, .010f, .12f), .018f);
                for (var j = -2; j <= 2; j++)
                    Add("Thruster side rib " + id + j, "metal", new(x + Math.Sign(x) * .094f, -.10f, z + j * .035f), new(.008f, .060f, .008f));
                Cable("jet power " + id, "red", .013f, new(x, -.153f, z - Math.Sign(z) * .108f),
                    new(x, -.153f, z - Math.Sign(z) * .17f), new(x * .56f, -.157f, z - Math.Sign(z) * .22f));
            }
        return Surface(parts);
    }

    // Every surface is now made from axis-aligned blocks on a 1 cm grid.
    // Contours are stair steps, not chamfers. Adjacent equal rows share one prism.
    internal const float Grid = .01f;
    private static int Cell(float value) => (int)MathF.Round(value / Grid, MidpointRounding.AwayFromZero);

    private static BoardPart Beam(string name, string finish, Vector3 a, Vector3 b, float diameter)
    {
        var part = new BoardPart(name, finish);
        var cells = new HashSet<(int X, int Y, int Z)>();
        var thickness = Math.Max(1, Cell(diameter));
        void Stamp(int x, int y, int z)
        {
            for (var dx = 0; dx < thickness; dx++)
                for (var dy = 0; dy < thickness; dy++)
                    for (var dz = 0; dz < thickness; dz++) cells.Add((x + dx, y + dy, z + dz));
        }
        var x = Cell(a.X); var y = Cell(a.Y); var z = Cell(a.Z);
        Stamp(x, y, z);
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(a, b) / Grid * 2));
        for (var i = 1; i <= steps; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)steps);
            // Manhattan connections keep diagonal runs solid and visibly stepped.
            while (x != Cell(p.X)) { x += Math.Sign(Cell(p.X) - x); Stamp(x, y, z); }
            while (y != Cell(p.Y)) { y += Math.Sign(Cell(p.Y) - y); Stamp(x, y, z); }
            while (z != Cell(p.Z)) { z += Math.Sign(Cell(p.Z) - z); Stamp(x, y, z); }
        }
        foreach (var c in cells.OrderBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Z))
            Block(part, c.X, c.Y, c.Z, c.X + 1, c.Y + 1, c.Z + 1);
        return part;
    }

    private static BoardPart Deck(string name, string finish, float width, float length, float bottom, float top)
    {
        var part = new BoardPart(name, finish);
        Rows(part, -Cell(length), Cell(length), z =>
        {
            var distance = Math.Abs((z + .5f) * Grid);
            var w = width - Math.Max(0, distance - (length - .105f));
            return (-Cell(w), Cell(w), Cell(bottom), Cell(top));
        });
        return part;
    }

    internal static BoardPart Box(string name, string finish, Vector3 center, Vector3 size, float cut)
    {
        var part = new BoardPart(name, finish);
        (int Low, int High) Bounds(float c, float s)
        {
            var lo = Cell(c - s / 2); var hi = Cell(c + s / 2);
            if (lo == hi) { lo = (int)MathF.Floor(c / Grid); hi = lo + 1; }
            return (lo, hi);
        }
        var (x0, x1) = Bounds(center.X, size.X);
        var (y0, y1) = Bounds(center.Y, size.Y);
        var (z0, z1) = Bounds(center.Z, size.Z);
        var corner = Math.Min(Cell(cut), Math.Max(0, (x1 - x0 - 1) / 2));
        Rows(part, z0, z1, z =>
        {
            var inset = Math.Max(0, corner - Math.Min(z - z0, z1 - 1 - z));
            return (x0 + inset, x1 - inset, y0, y1);
        });
        return part;
    }

    private static void Rows(BoardPart part, int start, int end, Func<int, (int X0, int X1, int Y0, int Y1)> shape)
    {
        for (var z = start; z < end;)
        {
            var row = shape(z);
            var stop = z + 1;
            while (stop < end && shape(stop) == row) stop++;
            Block(part, row.X0, row.Y0, z, row.X1, Math.Max(row.Y0 + 1, row.Y1), stop);
            z = stop;
        }
    }

    // Both ends rise 12 cm along a quadratic profile, still assembled from
    // horizontal blocks. Slice hardware too so armor/cables follow the kicked deck.
    internal static float KickHeight(float z)
    {
        var t = Math.Clamp((Math.Abs(z) - .48f) / .24f, 0, 1);
        return .12f * t * t;
    }
    internal const float ThrusterBottom = -.13f;
    private static void Block(BoardPart part, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        var jet = part.Name.StartsWith("Thruster") || part.Name.StartsWith("Nozzle well") ||
            part.Name.StartsWith("Blue nozzle") || part.Name.StartsWith("White core") ||
            part.Name.Contains("jet power");
        if (jet)
        {
            // Halve the complete assembly's height about its deck attachment plane.
            var low = Cell(-.05f + (y0 * Grid + .05f) * .5f);
            var high = Cell(-.05f + (y1 * Grid + .05f) * .5f);
            RawBlock(part, x0, low, z0, x1, Math.Max(low + 1, high), z1);
            return;
        }
        for (var z = z0; z < z1;)
        {
            var kick = Cell(KickHeight((z + .5f) * Grid));
            var stop = z + 1;
            while (stop < z1 && Cell(KickHeight((stop + .5f) * Grid)) == kick) stop++;
            RawBlock(part, x0, y0 + kick, z, x1, y1 + kick, stop);
            z = stop;
        }
    }

    private static void RawBlock(BoardPart part, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        for (var x = x0; x < x1; x++)
        for (var y = y0; y < y1; y++)
        for (var z = z0; z < z1; z++) part.Cells.Add((x, y, z));
    }

    // Resolve occupied cells first. Later-authored details own shared cells; faces
    // inside the solid never reach the renderer, even at material boundaries.
    internal static BoardPart[] Surface(List<BoardPart> parts)
    {
        var solid = new Dictionary<(int X, int Y, int Z), BoardPart>();
        foreach (var part in parts)
            foreach (var cell in part.Cells) solid[cell] = part;
        var faces = new Dictionary<(BoardPart Part, int Axis, int Sign, int Plane), HashSet<(int U, int V)>>();
        foreach (var (c, part) in solid)
        {
            void Face(int axis, int sign, int plane, int u, int v, (int, int, int) neighbor)
            {
                if (solid.ContainsKey(neighbor)) return;
                var key = (part, axis, sign, plane);
                if (!faces.TryGetValue(key, out var mask)) faces[key] = mask = new();
                mask.Add((u, v));
            }
            Face(0, -1, c.X, c.Y, c.Z, (c.X - 1, c.Y, c.Z));
            Face(0, 1, c.X + 1, c.Y, c.Z, (c.X + 1, c.Y, c.Z));
            Face(1, -1, c.Y, c.X, c.Z, (c.X, c.Y - 1, c.Z));
            Face(1, 1, c.Y + 1, c.X, c.Z, (c.X, c.Y + 1, c.Z));
            Face(2, -1, c.Z, c.X, c.Y, (c.X, c.Y, c.Z - 1));
            Face(2, 1, c.Z + 1, c.X, c.Y, (c.X, c.Y, c.Z + 1));
        }
        // Merge adjacent exposed squares without crossing a material/detail boundary.
        foreach (var (key, mask) in faces)
        {
            foreach (var start in mask.OrderBy(c => c.V).ThenBy(c => c.U).ToArray())
            {
                if (!mask.Contains(start)) continue;
                var u1 = start.U + 1; var v1 = start.V + 1;
                while (mask.Contains((u1, start.V))) u1++;
                while (Enumerable.Range(start.U, u1 - start.U).All(u => mask.Contains((u, v1)))) v1++;
                for (var u = start.U; u < u1; u++)
                    for (var v = start.V; v < v1; v++) mask.Remove((u, v));
                Vector3 P(int u, int v) => key.Axis switch
                {
                    0 => new(key.Plane * Grid, u * Grid, v * Grid),
                    1 => new(u * Grid, key.Plane * Grid, v * Grid),
                    _ => new(u * Grid, v * Grid, key.Plane * Grid)
                };
                var a = P(start.U, start.V); var b = P(u1, start.V);
                var c = P(u1, v1); var d = P(start.U, v1);
                var positive = key.Axis != 1;
                if ((key.Sign > 0) == positive) key.Part.Quad(a, b, c, d);
                else key.Part.Quad(a, d, c, b);
            }
        }
        foreach (var part in parts) part.Cells.Clear();
        return parts.Where(p => p.Vertices.Count != 0).ToArray();
    }
}
