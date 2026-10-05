using System.Numerics;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace NivalisMods.Hoba;

// Runs only after the verified fast path fails. Reads files, never loads a Unity scene.
internal sealed class NativeChestResolver : IDisposable
{
    private const string Model = "Carboard_Box_Light_C_Open";
    private readonly AssetsManager _manager = new();
    private readonly string _directory;
    private readonly Dictionary<(string, long), Matrix4x4> _transforms = new();
    private readonly Dictionary<(string, long), AssetTypeValueField> _meshes = new();
    private readonly Dictionary<(string, long), byte[]> _vertices = new();
    private readonly List<Vector3> _positions = new();
    private readonly List<Vector2> _uv = new();
    private readonly List<int> _triangles = new();
    private byte[]? _texture;
    private (string, long)? _textureId;
    private Matrix4x4 _worldToChest;

    private NativeChestResolver(string directory)
    {
        _directory = directory;
        using var database = typeof(NativeChestResolver).Assembly.GetManifestResourceStream("Hoba.UnityClassDatabase")
            ?? throw new InvalidDataException("HOBA Unity class database missing.");
        _manager.LoadClassDatabase(database);
    }

    internal static NativeChestAsset.Data Read(string directory)
    {
        using var resolver = new NativeChestResolver(directory);
        return resolver.Resolve();
    }

    private NativeChestAsset.Data Resolve()
    {
        var scene = _manager.LoadAssetsFile(Path.Combine(_directory, "level2"), false);
        Require(scene.file.Metadata.UnityVersion == "2020.3.44f1", "Unsupported Unity asset version.");
        AssetExternal? source = null;
        foreach (var info in scene.file.GetAssetsOfType(AssetClassID.GameObject))
        {
            var go = _manager.GetBaseField(scene, info);
            if (go["m_Name"].AsString != Model) continue;
            var candidate = new AssetExternal { file = scene, info = info, baseField = go };
            var transform = Transform(candidate);
            var parent = Follow(transform, transform.baseField["m_Father"]);
            var parentGo = Follow(parent, parent.baseField["m_GameObject"]);
            if (parentGo.baseField["m_Name"].AsString != "Detail_Objects_Foreground") continue;
            Require(source == null, "Ambiguous native chest source.");
            source = candidate;
        }
        Require(source != null, "Named native chest source was not found in level2.");
        var root = source!.Value;
        var world = World(Transform(root));
        // Static batches contain world-space vertices. Undo root rotation/translation,
        // retaining authored scale exactly as the known-address reader does.
        var x = Vector3.Normalize(new(world.M11, world.M12, world.M13));
        var y = Vector3.Normalize(new(world.M21, world.M22, world.M23));
        var z = Vector3.Normalize(new(world.M31, world.M32, world.M33));
        var basis = new Matrix4x4(x.X,x.Y,x.Z,0, y.X,y.Y,y.Z,0, z.X,z.Y,z.Z,0,
            world.M41,world.M42,world.M43,1);
        Require(Matrix4x4.Invert(basis, out _worldToChest), "Invalid chest transform.");
        Visit(root, 0);
        var data = new NativeChestAsset.Data(_positions.ToArray(), _uv.ToArray(), _triangles.ToArray(),
            _texture ?? throw new InvalidDataException("Native chest texture missing."));
        Validate(data);
        return data;
    }

    private AssetExternal Follow(AssetExternal owner, AssetTypeValueField pointer)
    {
        Require(pointer["m_PathID"].AsLong != 0, "Missing chest asset reference.");
        var asset = _manager.GetExtAsset(owner.file, pointer);
        Require(asset.info != null && asset.baseField != null, "Unresolved chest asset reference.");
        Require(asset.file.file.Metadata.UnityVersion == "2020.3.44f1", "Unsupported referenced asset version.");
        return asset;
    }

    private List<AssetExternal> Components(AssetExternal go) => go.baseField["m_Component.Array"].Children
        .Select(c => Follow(go, c["component"])).ToList();

    private AssetExternal Transform(AssetExternal go) => Components(go).Single(c => c.info.TypeId == 4);

    private Matrix4x4 World(AssetExternal transform, int depth = 0)
    {
        Require(depth < 64, "Cyclic or excessive transform hierarchy.");
        var key = (transform.file.path, transform.info.PathId);
        if (_transforms.TryGetValue(key, out var cached)) return cached;
        var t = transform.baseField;
        var q = t["m_LocalRotation"];
        var matrix = Matrix4x4.CreateScale(Vector(t["m_LocalScale"])) *
            Matrix4x4.CreateFromQuaternion(new(q["x"].AsFloat,q["y"].AsFloat,q["z"].AsFloat,q["w"].AsFloat)) *
            Matrix4x4.CreateTranslation(Vector(t["m_LocalPosition"]));
        if (t["m_Father.m_PathID"].AsLong != 0) matrix *= World(Follow(transform, t["m_Father"]), depth + 1);
        _transforms.Add(key, matrix);
        return matrix;
    }

    private void Visit(AssetExternal go, int depth)
    {
        Require(depth < 16, "Excessive chest object hierarchy.");
        var name = go.baseField["m_Name"].AsString;
        if (name.Contains("Shadow", StringComparison.OrdinalIgnoreCase) || name.Contains("LOD_", StringComparison.Ordinal)) return;
        var components = Components(go);
        var transform = components.Single(c => c.info.TypeId == 4);
        var filters = components.Where(c => c.info.TypeId == 33).ToArray();
        var renderers = components.Where(c => c.info.TypeId == 23).ToArray();
        if (filters.Length == 1 && renderers.Length == 1)
            Append(Follow(filters[0], filters[0].baseField["m_Mesh"]), renderers[0], World(transform));
        foreach (var child in transform.baseField["m_Children.Array"].Children)
        {
            var childTransform = Follow(transform, child);
            Visit(Follow(childTransform, childTransform.baseField["m_GameObject"]), depth + 1);
        }
    }

    private void Append(AssetExternal meshAsset, AssetExternal renderer, Matrix4x4 world)
    {
        var key = (meshAsset.file.path, meshAsset.info.PathId);
        if (!_meshes.TryGetValue(key, out var mesh)) _meshes.Add(key, mesh = meshAsset.baseField);
        Require(mesh["m_MeshCompression"].AsByte == 0, "Compressed chest meshes are unsupported.");
        var vertex = mesh["m_VertexData"];
        var count = checked((int)vertex["m_VertexCount"].AsUInt);
        Require(count is > 0 and <= 1000000, "Invalid chest vertex count.");
        if (!_vertices.TryGetValue(key, out var bytes))
        {
            bytes = vertex["m_DataSize"].AsByteArray;
            if (bytes.Length == 0) bytes = ReadStream(mesh["m_StreamData"]);
            _vertices.Add(key, bytes);
        }
        var channels = vertex["m_Channels.Array"].Children;
        Require(channels.Count >= 5, "Chest position/UV channels missing.");
        var position = Channel(channels, 0, count);
        var uv = Channel(channels, 4, count);
        Require(position.Dimension == 3 && uv.Dimension == 2, "Unsupported chest position/UV dimensions.");
        var indexFormat = mesh["m_IndexFormat"].AsInt;
        Require(indexFormat is 0 or 1, "Unsupported chest index format.");
        var indexSize = indexFormat == 0 ? 2 : 4;
        var indices = mesh["m_IndexBuffer.Array"].AsByteArray;
        var batch = renderer.baseField["m_StaticBatchInfo"];
        var batchCount = batch["subMeshCount"].AsUShort;
        var first = batchCount > 0 ? batch["firstSubMesh"].AsUShort : 0;
        var materials = renderer.baseField["m_Materials.Array"].Children;
        var submeshes = mesh["m_SubMeshes.Array"].Children;
        var number = batchCount > 0 ? batchCount : materials.Count;
        Require(number > 0 && first + number <= submeshes.Count && number == materials.Count, "Invalid chest submeshes.");
        var mapping = new Dictionary<int,int>();
        for (var j = 0; j < number; j++)
        {
            ReadTexture(Follow(renderer, materials[j]));
            var submesh = submeshes[first + j];
            Require(submesh["topology"].AsInt == 0, "Chest mesh is not triangles.");
            var length = checked((int)submesh["indexCount"].AsUInt);
            var start = checked((int)submesh["firstByte"].AsUInt);
            var baseVertex = checked((int)submesh["baseVertex"].AsUInt);
            Require(length > 0 && length <= 30000 && length % 3 == 0 && start >= 0 &&
                (long)start + (long)length * indexSize <= indices.Length, "Invalid chest index range.");
            for (var i = 0; i < length; i++)
            {
                var offset = start + i * indexSize;
                var index = checked(baseVertex + (indexSize == 2 ? BitConverter.ToUInt16(indices, offset) : (int)BitConverter.ToUInt32(indices, offset)));
                Require(index >= 0 && index < count, "Chest index exceeds vertex buffer.");
                if (!mapping.TryGetValue(index, out var mapped))
                {
                    var p = new Vector3(Component(bytes, position, index, 0), Component(bytes, position, index, 1), Component(bytes, position, index, 2));
                    if (batchCount == 0) p = Vector3.Transform(p, world);
                    p = Vector3.Transform(p, _worldToChest);
                    mapped = _positions.Count;
                    mapping.Add(index, mapped);
                    _positions.Add(p);
                    _uv.Add(new(Component(bytes, uv, index, 0), Component(bytes, uv, index, 1)));
                }
                _triangles.Add(mapped);
            }
        }
    }

    private void ReadTexture(AssetExternal material)
    {
        var entries = material.baseField["m_SavedProperties.m_TexEnvs.Array"].Children;
        var main = entries.SingleOrDefault(e => e["first"].AsString == "_MainTex")
            ?? throw new InvalidDataException("Chest material has no main texture.");
        var texture = Follow(material, main["second.m_Texture"]);
        var id = (texture.file.path, texture.info.PathId);
        if (_textureId != null)
        {
            Require(_textureId == id, "Chest uses multiple textures.");
            return;
        }
        var t = texture.baseField;
        Require(t["m_Width"].AsInt == 512 && t["m_Height"].AsInt == 512 &&
            t["m_TextureFormat"].AsInt == 10 && t["m_MipCount"].AsInt == 10, "Unsupported chest texture layout.");
        _texture = t["image data"].AsByteArray;
        if (_texture.Length == 0) _texture = ReadStream(t["m_StreamData"]);
        Require(_texture.Length == 174776, "Invalid chest texture length.");
        _textureId = id;
    }

    private byte[] ReadStream(AssetTypeValueField stream)
    {
        var name = stream["path"].AsString;
        Require(name.Length > 0 && name == Path.GetFileName(name) && !name.Contains('\\'), "Unsupported asset stream path.");
        var offset = checked((long)stream["offset"].AsULong);
        var size = checked((int)stream["size"].AsUInt);
        Require(size is > 0 and <= 64000000, "Unsupported asset stream size.");
        using var file = File.OpenRead(Path.Combine(_directory, name));
        Require(offset >= 0 && offset <= file.Length - size, "Asset stream range exceeds file.");
        file.Position = offset;
        var bytes = new byte[size];
        var read = 0;
        while (read < size)
        {
            var n = file.Read(bytes, read, size - read);
            if (n == 0) throw new EndOfStreamException(name);
            read += n;
        }
        return bytes;
    }

    private sealed record VertexChannel(int Offset, int Stride, int Format, int Dimension);

    private static VertexChannel Channel(List<AssetTypeValueField> channels, int index, int vertexCount)
    {
        var selected = channels[index];
        var stream = selected["stream"].AsByte;
        var offset = 0;
        var stride = 0;
        for (var s = 0; s <= stream; s++)
        {
            stride = 0;
            foreach (var c in channels.Where(c => c["stream"].AsByte == s))
            {
                var dimension = c["dimension"].AsByte & 15;
                if (dimension > 0) stride = Math.Max(stride, c["offset"].AsByte + dimension * FormatSize(c["format"].AsByte));
            }
            if (s < stream) offset = checked((offset + stride * vertexCount + 15) & ~15);
        }
        return new(checked(offset + selected["offset"].AsByte), stride, selected["format"].AsByte, selected["dimension"].AsByte & 15);
    }

    private static int FormatSize(int format) => format switch
    {
        0 or 10 or 11 => 4, 1 or 4 or 5 or 8 or 9 => 2, 2 or 3 or 6 or 7 => 1,
        _ => throw new InvalidDataException("Unsupported vertex channel format.")
    };

    private static float Component(byte[] bytes, VertexChannel channel, int vertex, int component)
    {
        Require(channel.Format is 0 or 1, "Unsupported chest position/UV format.");
        var size = FormatSize(channel.Format);
        var offset = checked(channel.Offset + vertex * channel.Stride + component * size);
        Require(offset >= 0 && offset <= bytes.Length - size, "Chest vertex range exceeds buffer.");
        return channel.Format == 0 ? BitConverter.ToSingle(bytes, offset) : (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(bytes, offset));
    }

    internal static void Validate(NativeChestAsset.Data data)
    {
        Require(data.Vertices.Length is > 0 and <= 10000 && data.UV.Length == data.Vertices.Length &&
            data.Triangles.Length is > 0 and <= 30000 && data.Triangles.Length % 3 == 0, "Invalid resolved chest geometry.");
        Require(data.Vertices.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)) &&
            data.UV.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y)) &&
            data.Triangles.All(i => i >= 0 && i < data.Vertices.Length), "Invalid resolved chest vertex/index values.");
        var min = data.Vertices.Aggregate(Vector3.Min);
        var max = data.Vertices.Aggregate(Vector3.Max);
        Require(Vector3.Distance((min + max) / 2, new(-.04579527f,.01374945f,.010190487f)) < .02f &&
            (max - min).Length() is > .5f and < 2f, "Resolved chest no longer fits existing placements.");
        Require(data.Texture.Length == 174776, "Invalid resolved chest texture.");
    }

    private static Vector3 Vector(AssetTypeValueField value) => new(value["x"].AsFloat,value["y"].AsFloat,value["z"].AsFloat);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    public void Dispose() => _manager.UnloadAll();
}
