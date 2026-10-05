namespace NivalisMods.Hoba;

internal sealed record ParkedBoardSave(string Product, string Area, float[] Ground, float[] Rotation);
internal sealed class HobaCheckpoint
{
    public int Version { get; set; } = 1;
    public ParkedBoardSave? Board { get; set; }
    public Dictionary<string, string> Discoveries { get; set; } = new();

    internal bool TryDiscover(string chestId, Func<BoardProduct, bool> grant, out BoardProduct? reward)
    {
        reward = null;
        if (Discoveries.ContainsKey(chestId) || !WorldPlacements.Chests.Any(c => c.Id == chestId)) return false;
        var available = BoardProducts.All.Where(p => p.Legendary && !Discoveries.Values.Contains(p.Id)).ToArray();
        if (available.Length == 0) return false;
        var choice = available[Random.Shared.Next(available.Length)];
        if (!grant(choice)) return false;
        Discoveries.Add(chestId, choice.Id);
        reward = choice;
        return true;
    }

    internal void Validate()
    {
        if (Version != 1 || Discoveries == null || Discoveries.Values.Distinct().Count() != Discoveries.Count ||
            Discoveries.Any(d => !WorldPlacements.Chests.Any(c => c.Id == d.Key) || !BoardProducts.All.Any(p => p.Id == d.Value && p.Legendary)))
            throw new InvalidDataException("Invalid legendary unlock checkpoint.");
        if (Board is { } b && (b.Ground == null || b.Rotation == null || b.Ground.Length != 3 || b.Rotation.Length != 4 ||
            !b.Ground.Concat(b.Rotation).All(float.IsFinite) || string.IsNullOrWhiteSpace(b.Area) ||
            Math.Abs(b.Rotation.Sum(v => v*v) - 1) > .01f || !BoardProducts.All.Any(p => p.Id == b.Product && !p.IsPaint)))
            throw new InvalidDataException("Invalid parked-board checkpoint.");
    }
}
