using System.Numerics;
using NivalisMods.Hoba;

internal static class NativeChestChecks
{
    internal static void Run(string directory)
    {
        var fast = NativeChestAsset.ReadKnown(directory);
        var notices = new List<string>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var recovered = NativeChestAsset.Load(directory,
            _ => throw new InvalidDataException("Simulated stale game-build addresses"), notices.Add);
        Console.WriteLine($"Native chest fallback resolved in {watch.ElapsedMilliseconds} ms.");
        Require(notices.Count == 1, "Fallback reports recovery once");
        Require(fast.Triangles.SequenceEqual(recovered.Triangles), "Fallback triangle order matches known mesh");
        Require(fast.Texture.SequenceEqual(recovered.Texture), "Fallback texture matches verified texture");
        Require(fast.Vertices.Length == recovered.Vertices.Length, "Fallback vertex count matches");
        for (var i = 0; i < fast.Vertices.Length; i++)
        {
            Require(Vector3.Distance(fast.Vertices[i], recovered.Vertices[i]) < .0001f, "Fallback preserves vertex/pivot " + i);
            Require(Vector2.Distance(fast.UV[i], recovered.UV[i]) < .00001f, "Fallback UV matches " + i);
        }
        NativeChestResolver.Validate(recovered);
        Require(ReferenceEquals(NativeChestAsset.Read(directory), NativeChestAsset.Read(directory)), "Session reuses chest data");
        var unexpectedFallback = false;
        Require(ReferenceEquals(fast, NativeChestAsset.Load(directory, _ => fast, _ => unexpectedFallback = true)) &&
            !unexpectedFallback, "Successful fast path does not resolve or log");
        Reject(() => NativeChestResolver.Validate(recovered with { Texture = Array.Empty<byte>() }));
        Reject(() => NativeChestResolver.Validate(recovered with { Triangles = new[] { 0, 1, recovered.Vertices.Length } }));
        Reject(() => NativeChestResolver.Validate(recovered with { Vertices = recovered.Vertices.Select(v => v + Vector3.One).ToArray() }));
        Reject(() => NativeChestResolver.Validate(recovered with { UV = recovered.UV.Select(_ => new Vector2(float.NaN,0)).ToArray() }));
        var missing = Path.Combine(Path.GetTempPath(), "hoba-missing-assets-" + Guid.NewGuid());
        Exception? first = null, second = null;
        try { NativeChestAsset.Read(missing); } catch (Exception e) { first = e; }
        try { NativeChestAsset.Read(missing); } catch (Exception e) { second = e; }
        Require(first != null && ReferenceEquals(first, second), "Session caches failed resolution without retrying");
        Console.WriteLine("Native chest fallback: stale-address recovery, geometry/UV/texture parity, validation and session cache checks passed.");
    }

    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Invalid native chest data was accepted.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
