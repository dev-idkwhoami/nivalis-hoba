using System.Numerics;
using System.Security.Cryptography;

namespace NivalisMods.Hoba;

// Fast-path addresses for Steam build 25726588. Game bytes are never shipped.
internal static class NativeChestAsset
{
    internal sealed record Data(Vector3[] Vertices, Vector2[] UV, int[] Triangles, byte[] Texture);
    private static readonly Dictionary<string, Lazy<Data>> Cache = new();

    internal static Data Read(string dataDirectory, Action<string>? log = null)
    {
        var directory = Path.GetFullPath(dataDirectory);
        lock (Cache)
        {
            if (!Cache.TryGetValue(directory, out var entry))
                Cache.Add(directory, entry = new Lazy<Data>(() => Load(directory, ReadKnown, log)));
            // Lazy caches failures too: no repeated scan on area changes.
            return entry.Value;
        }
    }

    internal static Data Load(string directory, Func<string, Data> readKnown, Action<string>? log = null)
    {
        try { return readKnown(directory); }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            var data = NativeChestResolver.Read(directory);
            log?.Invoke("HOBA chest asset addresses changed; resolved installed assets by name. Cached for this session.");
            return data;
        }
    }

    internal static Data ReadKnown(string dataDirectory)
    {
        var vertices = ReadRange(dataDirectory, "level2.resS", 213137968, 2460920,
            "23A8509C65F6EBE93AFB4E40945FA4CE69EAED3C3EFFEA9038306522764DECE1");
        var triangles = new List<int>();
        foreach (var (offset, count, hash) in new[] {
            (32516212L, 204, "598B1D012DFFCC7F729D41B25D362AAA6C58EC56C2D1245876831F5DEEB8D051"),
            (32514004L, 36, "E98316B3E502D4E01A5F2D3A6405B25483EE98AE5BCD2F035B56FB524BB9915E"),
            (32514148L, 36, "F39FC4A2D2E17E33BD23CC0DCA9616614030DA808812C166B92D4BE86552E6C9") })
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
                uv.Add(new((float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(vertices, offset + 28)), (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(vertices, offset + 30))));
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
