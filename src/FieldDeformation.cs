using System.Numerics;

namespace NivalisMods.Hoba;

// Entirely managed: Unity's IL2CPP Vector/Quaternion operators cross the runtime boundary.
// Each longitudinal section shares its curve position and orientation across all faces.
internal sealed class FieldDeformation
{
    private readonly Vector3[] _source;
    private readonly int[] _sections;
    private readonly float[] _times;
    private readonly Vector3[] _centers, _right, _up;
    internal Vector3[] Vertices { get; }

    internal FieldDeformation(IEnumerable<Vector3> source)
    {
        _source = source.ToArray();
        Vertices = new Vector3[_source.Length];
        _times = _source.Select(v => Math.Clamp(v.Z + .5f, 0, 1)).Distinct().ToArray();
        var indices = _times.Select((t, i) => (t, i)).ToDictionary(pair => pair.t, pair => pair.i);
        _sections = _source.Select(v => indices[Math.Clamp(v.Z + .5f, 0, 1)]).ToArray();
        _centers = new Vector3[_times.Length];
        _right = new Vector3[_times.Length];
        _up = new Vector3[_times.Length];
    }

    internal void Bend(Vector3 a, Vector3 c1, Vector3 c2, Vector3 b, float width, float radians)
    {
        for (var i = 0; i < _times.Length; i++)
        {
            var t = _times[i]; var u = 1 - t;
            _centers[i] = u*u*u*a + 3*u*u*t*c1 + 3*u*t*t*c2 + t*t*t*b;
            var tangent = 3*u*u*(c1-a) + 6*u*t*(c2-c1) + 3*t*t*(b-c2);
            if (tangent.LengthSquared() <= .00000001f)
            { _right[i] = Vector3.UnitX; _up[i] = Vector3.UnitY; continue; }
            var forward = Vector3.Normalize(tangent);
            var right = Vector3.Cross(Vector3.UnitY, forward);
            // Connectors normally run along Z. Keep an orthonormal frame for vertical tangents too.
            if (right.LengthSquared() < .00000001f) right = Vector3.Cross(Vector3.UnitZ, forward);
            _right[i] = Vector3.Normalize(right);
            _up[i] = Vector3.Cross(forward, _right[i]);
        }
        var sin = MathF.Sin(radians); var cos = MathF.Cos(radians);
        for (var i = 0; i < _source.Length; i++)
        {
            var v = _source[i]; var section = _sections[i];
            var x = (cos * v.X - sin * v.Y) * width;
            var y = (sin * v.X + cos * v.Y) * width;
            Vertices[i] = _centers[section] + _right[section] * x + _up[section] * y;
        }
    }
}
