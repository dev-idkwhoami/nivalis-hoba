using System.Numerics;

namespace NivalisMods.Hoba;

// The complete chest mesh and DXT1 texture are embedded; no installed-game reads.
internal static class NativeChestAsset
{
    internal sealed record Data(Vector3[] Vertices, Vector2[] UV, int[] Triangles, byte[] Texture);
    private static readonly Lazy<Data> Cached = new(Load);

    internal static Data Read() => Cached.Value;

    private static Data Load()
    {
        using var stream = typeof(NativeChestAsset).Assembly.GetManifestResourceStream("Hoba.HiddenChest")
            ?? throw new InvalidDataException("Embedded HOBA chest missing.");
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != 0x31434248 || reader.ReadInt32() != 144 ||
            reader.ReadInt32() != 276 || reader.ReadInt32() != 174776 || stream.Length != 178776)
            throw new InvalidDataException("Invalid embedded HOBA chest format.");
        var vertices = new Vector3[144];
        var uv = new Vector2[144];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            uv[i] = new(reader.ReadSingle(), reader.ReadSingle());
        }
        var triangles = new int[276];
        for (var i = 0; i < triangles.Length; i++) triangles[i] = reader.ReadInt32();
        return new(vertices, uv, triangles, reader.ReadBytes(174776));
    }
}
