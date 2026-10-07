using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NivalisMods.Hoba;

internal sealed class ChestVisual : IDisposable
{
    internal GameObject Root { get; }
    internal Bounds Bounds { get; }
    private Mesh? _mesh;
    private Material? _material;
    private Texture2D? _texture;

    internal ChestVisual(ContainerPlacement placement)
    {
        Root = new GameObject("HOBA hidden chest");
        Root.SetActive(false);
        try
        {
            if (placement.Model != "Carboard_Box_Light_C_Open") throw new InvalidDataException("Unsupported chest model.");
            var data = NativeChestAsset.Read();
            var shift = new Vector3(placement.MeshOffset[0], placement.MeshOffset[1], placement.MeshOffset[2]);
            _mesh = new Mesh { name = placement.Model };
            _mesh.vertices = new Il2CppStructArray<Vector3>(data.Vertices.Select(p => new Vector3(p.X, p.Y, p.Z) + shift).ToArray());
            _mesh.uv = new Il2CppStructArray<Vector2>(data.UV.Select(p => new Vector2(p.X, p.Y)).ToArray());
            _mesh.triangles = new Il2CppStructArray<int>(data.Triangles);
            _mesh.RecalculateNormals(); _mesh.RecalculateBounds(); Bounds = _mesh.bounds;
            _texture = new Texture2D(512, 512, TextureFormat.DXT1, true) { filterMode = FilterMode.Point };
            _texture.LoadRawTextureData(new Il2CppStructArray<byte>(data.Texture));
            _texture.Apply(false, true);
            _material = new Material(Shader.Find("Standard") ?? throw new InvalidOperationException("Standard shader missing."));
            _material.mainTexture = _texture; _material.SetFloat("_Glossiness", .15f);
            Root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            Root.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (Root != null) { Root.SetActive(false); Object.Destroy(Root); }
        if (_mesh != null) Object.Destroy(_mesh);
        if (_material != null) Object.Destroy(_material);
        if (_texture != null) Object.Destroy(_texture);
    }
}
