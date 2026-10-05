using System.Text;

namespace NivalisMods.Hoba;

internal static class HobaCheckpointBinary
{
    private const uint Magic = 0x41424F48; // HOBA, little endian
    internal static byte[] Encode(HobaCheckpoint state)
    {
        state.Validate();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Magic);
        writer.Write(state.Version);
        writer.Write(state.Board != null);
        if (state.Board is { } board)
        {
            writer.Write(board.Product);
            writer.Write(board.Area);
            foreach (var value in board.Ground) writer.Write(value);
            foreach (var value in board.Rotation) writer.Write(value);
        }
        writer.Write(state.Discoveries.Count);
        foreach (var entry in state.Discoveries.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value);
        }
        writer.Flush();
        return stream.ToArray();
    }

    internal static HobaCheckpoint Decode(byte[] bytes)
    {
        if (bytes.Length > 65536) throw new InvalidDataException("HOBA checkpoint too large.");
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != 1)
            throw new InvalidDataException("Unknown HOBA checkpoint format.");
        var state = new HobaCheckpoint();
        var hasBoard = reader.ReadByte();
        if (hasBoard > 1) throw new InvalidDataException("Invalid board flag.");
        if (hasBoard == 1)
            state.Board = new ParkedBoardSave(reader.ReadString(), reader.ReadString(),
                new[] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() },
                new[] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() });
        var count = reader.ReadInt32();
        if (count < 0 || count > WorldPlacements.Chests.Length)
            throw new InvalidDataException("Invalid discovery count.");
        for (var i = 0; i < count; i++)
            if (!state.Discoveries.TryAdd(reader.ReadString(), reader.ReadString()))
                throw new InvalidDataException("Duplicate chest in checkpoint.");
        if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected checkpoint data.");
        state.Validate();
        return state;
    }
}
