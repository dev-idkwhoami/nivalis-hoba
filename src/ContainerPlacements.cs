namespace NivalisMods.Hoba;

internal sealed record ContainerPlacement
{
    public string Id { get; init; } = "";
    public string Model { get; init; } = "";
    public string Area { get; init; } = "";
    public float[] Position { get; init; } = Array.Empty<float>();
    public float[] RotationEuler { get; init; } = Array.Empty<float>();
    public float[] RotationQuaternion { get; init; } = Array.Empty<float>();
    public float Scale { get; init; } = 1;
    public float InteractionRange { get; init; } = 1;
    public float[] MeshOffset { get; init; } = Array.Empty<float>();
}
