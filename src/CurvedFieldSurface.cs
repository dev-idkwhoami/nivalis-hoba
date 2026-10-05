using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using ManagedVector = System.Numerics.Vector3;

namespace NivalisMods.Hoba;

// Reuse fixed mesh/buffers. All per-vertex math stays outside the IL2CPP wrappers.
internal sealed class CurvedFieldSurface
{
    internal Mesh Mesh { get; }
    private readonly FieldDeformation _deformation;
    private readonly Il2CppStructArray<Vector3> _vertices;
    private readonly float _rotationRate;
    private ManagedVector _a, _c1, _c2, _b;
    private float _width, _angle;
    private bool _hasPose;

    internal CurvedFieldSurface(BoardPart part, float rotationRate)
    {
        _rotationRate = rotationRate;
        _deformation = new FieldDeformation(part.Vertices);
        _vertices = new Il2CppStructArray<Vector3>(part.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray());
        Mesh = new Mesh { name = part.Name };
        Mesh.MarkDynamic();
        Mesh.vertices = _vertices;
        Mesh.triangles = new Il2CppStructArray<int>(part.Triangles.ToArray());
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
    }

    internal void Bend(Vector3 a, Vector3 c1, Vector3 c2, Vector3 b, float width, float time)
    {
        var ma = new ManagedVector(a.x, a.y, a.z);
        var mc1 = new ManagedVector(c1.x, c1.y, c1.z);
        var mc2 = new ManagedVector(c2.x, c2.y, c2.z);
        var mb = new ManagedVector(b.x, b.y, b.z);
        var angle = time * _rotationRate * (MathF.PI / 180f);
        // Paused frames and unchanged non-rotating links need no uploads or recalculations.
        if (_hasPose && ma == _a && mc1 == _c1 && mc2 == _c2 && mb == _b && width == _width && angle == _angle) return;
        _deformation.Bend(ma, mc1, mc2, mb, width, angle);
        var vertices = _deformation.Vertices;
        for (var i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            _vertices[i] = new Vector3 { x = v.X, y = v.Y, z = v.Z };
        }
        Mesh.vertices = _vertices;
        // Preserve lit surface normals and accurate culling bounds when the shape changes.
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
        _a = ma; _c1 = mc1; _c2 = mc2; _b = mb; _width = width; _angle = angle; _hasPose = true;
    }
}
