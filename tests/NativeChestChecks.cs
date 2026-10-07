using System.Numerics;
using System.Security.Cryptography;
using NivalisMods.Hoba;

internal static class NativeChestChecks
{
    internal static void Run()
    {
        using var stream = typeof(NativeChestAsset).Assembly.GetManifestResourceStream("Hoba.HiddenChest")!;
        Require(Convert.ToHexString(SHA256.HashData(stream)) ==
            "4351BA16FB5667EFE15C0A8E4C61F19311D836648B426FA2176E650ECE6CFFEE",
            "Embedded chest matches the verified source export");
        var data = NativeChestAsset.Read();
        Require(ReferenceEquals(data, NativeChestAsset.Read()), "Session reuses chest data");
        Require(data.Vertices.Length == 144 && data.UV.Length == 144 && data.Triangles.Length == 276,
            "Complete chest mesh and lids are present");
        Require(data.Vertices.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)) &&
            data.UV.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y)) &&
            data.Triangles.All(i => i >= 0 && i < data.Vertices.Length), "Valid geometry and UV values");
        var min = data.Vertices.Aggregate(Vector3.Min);
        var max = data.Vertices.Aggregate(Vector3.Max);
        Require(Vector3.Distance((min + max) / 2, new(-.04579527f, .01374945f, .010190487f)) < .001f &&
            (max - min).Length() is > .5f and < 2f, "Chest retains the existing placement pivot and size");
        Require(data.Texture.Length == 174776 && Convert.ToHexString(SHA256.HashData(data.Texture)) ==
            "D457D310FC141F3550E16986A05E80D0384E8E860D7594A47E736111B96C19C6",
            "Complete original DXT1 texture and mipmaps are present");
        Console.WriteLine("Embedded chest: source checksum, geometry, UVs, placement pivot, texture and session cache checks passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
