namespace NivalisMods.Hoba;

internal sealed record VendorLocation
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "HOBA Dealer";
    public string Area { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
    public bool Enabled { get; init; } = true;
    public string NpcPrefab { get; init; } = "";
    internal float DistanceSquared(float x, float y, float z) => (X-x)*(X-x) + (Y-y)*(Y-y) + (Z-z)*(Z-z);
}

internal sealed record VendorLocationFile
{
    public int Version { get; init; } = 1;
    public VendorLocation[] Locations { get; init; } = Array.Empty<VendorLocation>();
}
