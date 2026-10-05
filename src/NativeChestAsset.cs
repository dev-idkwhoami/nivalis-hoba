using System.Numerics;
using System.Security.Cryptography;

namespace NivalisMods.Hoba;

// Asset addresses for the supported game build. Only offsets/hashes are shipped,
// never game mesh or texture bytes. Fail closed if an update changes these ranges.
internal static class NativeChestAsset
{
    internal sealed record Data(Vector3[] Vertices, Vector2[] UV, int[] Triangles, byte[] Texture);
    internal static Data Read(string dataDirectory)
    {
        var vertices = ReadRange(dataDirectory, "level2", 31766008, 301920,
            "91B8901BF685B86C0DC00C4560BCDB68A870D3767A0BADED5FA072CF4A5C269B");
        var triangles = new List<int>();
        foreach (var (offset, count, hash) in new[] {
            (31743908L, 204, "598B1D012DFFCC7F729D41B25D362AAA6C58EC56C2D1245876831F5DEEB8D051"),
            (31741700L, 36, "E98316B3E502D4E01A5F2D3A6405B25483EE98AE5BCD2F035B56FB524BB9915E"),
            (31741844L, 36, "F39FC4A2D2E17E33BD23CC0DCA9616614030DA808812C166B92D4BE86552E6C9") })
        {
            var indices = ReadRange(dataDirectory, "level2", offset, count * 2, hash);
            for (var i = 0; i < count; i++) triangles.Add(BitConverter.ToUInt16(indices, i * 2));
        }
        var positions = new List<Vector3>(); var uv = new List<Vector2>();
        var mapping = new Dictionary<int, int>();
        for (var i = 0; i < triangles.Count; i++)
        {
            var original = triangles[i];
            if (!mapping.TryGetValue(original, out var index))
            {
                var offset = original * 40;
                var p = new Vector3(BitConverter.ToSingle(vertices, offset), BitConverter.ToSingle(vertices, offset + 4), BitConverter.ToSingle(vertices, offset + 8))
                    - new Vector3(-9.503997802734375f, -.5024967789649963f, 31.475000381469727f);
                // Undo the source object's orientation, retaining its authored scale.
                var local = new Vector3(
                    Vector3.Dot(p, new(-.000005499087734f, -1f, .000001446979247f)),
                    Vector3.Dot(p, new(-.96592595f, .000005532489145f, .258818584f)),
                    Vector3.Dot(p, new(-.258818565f, .000000005352723f, -.965925955f)));
                index = positions.Count; mapping.Add(original, index); positions.Add(local);
                uv.Add(new(BitConverter.ToSingle(vertices, offset + 24), BitConverter.ToSingle(vertices, offset + 28)));
            }
            triangles[i] = index;
        }
        var texture = ReadRange(dataDirectory, "sharedassets0.assets.resS", 273600944, 174776,
            "D457D310FC141F3550E16986A05E80D0384E8E860D7594A47E736111B96C19C6");
        return new(positions.ToArray(), uv.ToArray(), triangles.ToArray(), texture);
    }

    private static byte[] ReadRange(string directory, string file, long offset, int length, string hash)
    {
        using var stream = File.OpenRead(Path.Combine(directory, file));
        if (stream.Length < offset + length) throw new InvalidDataException("Unsupported game assets: " + file);
        stream.Position = offset;
        var bytes = new byte[length];
        var read = 0;
        while (read < length)
        {
            var count = stream.Read(bytes, read, length - read);
            if (count == 0) throw new EndOfStreamException(file);
            read += count;
        }
        using var sha = SHA256.Create();
        if (Convert.ToHexString(sha.ComputeHash(bytes)) != hash)
            throw new InvalidDataException("Game assets changed; HOBA chest asset addresses need updating: " + file);
        return bytes;
    }
}
