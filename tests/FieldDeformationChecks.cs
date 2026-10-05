using System.Numerics;
using NivalisMods.Hoba;

internal static class FieldDeformationChecks
{
    internal static void Run()
    {
        static void Near(Vector3 actual, Vector3 expected, string label)
        {
            if (!float.IsFinite(actual.X + actual.Y + actual.Z) || Vector3.Distance(actual, expected) > .00001f)
                throw new Exception($"Field deformation {label}: {actual} != {expected}");
        }
        var source = new[] { new Vector3(0,0,-.5f), new Vector3(0,0,.5f), new Vector3(1,0,0), new Vector3(0,1,0), new Vector3(1,0,0) };
        var shape = new FieldDeformation(source);
        var a = new Vector3(0,0,-1); var b = new Vector3(0,0,1);
        shape.Bend(a, a + (b-a)/3, a + (b-a)*2/3, b, .1f, 0);
        Near(shape.Vertices[0], a, "rear anchor"); Near(shape.Vertices[1], b, "front anchor");
        Near(shape.Vertices[2], new(.1f,0,0), "width"); Near(shape.Vertices[3], new(0,.1f,0), "up");
        Near(shape.Vertices[4], shape.Vertices[2], "shared section");
        shape.Bend(a, a + (b-a)/3, a + (b-a)*2/3, b, .1f, MathF.PI/2);
        Near(shape.Vertices[2], new(0,.1f,0), "positive shell spin");
        // Bend into +X: midpoint center is .3, forward remains +Z by symmetry.
        var c1 = new Vector3(.4f,0,-.5f); var c2 = new Vector3(.4f,0,.5f);
        shape.Bend(a,c1,c2,b,.1f,0);
        Near(shape.Vertices[2],new(.4f,0,0),"curved midpoint");
        // Rigidly rotate the whole curve: local right follows its tangent.
        shape.Bend(new(-1,0,0),new(-1f/3,0,0),new(1f/3,0,0),new(1,0,0),.1f,0);
        Near(shape.Vertices[2],new(0,0,-.1f),"yaw orientation");
        shape.Bend(Vector3.Zero,Vector3.Zero,Vector3.Zero,Vector3.Zero,.1f,0);
        Near(shape.Vertices[2],new(.1f,0,0),"degenerate tangent");
        shape.Bend(new(0,-1,0),new(0,-.3f,0),new(0,.3f,0),new(0,1,0),.1f,0);
        if (!float.IsFinite(shape.Vertices[2].LengthSquared())) throw new Exception("Vertical tangent not finite");
        shape.Bend(a,c1,c2,b,.1f,0); // Warm before allocation check.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i=0; i<100; i++) shape.Bend(a,c1,c2,b,.1f,i*.1f);
        if (GC.GetAllocatedBytesForCurrentThread() != before) throw new Exception("Per-frame managed curve allocation");
        Console.WriteLine("Field deformation: anchors, curve, orientation, shell spin, degeneracy and allocation checks passed.");
    }
}
