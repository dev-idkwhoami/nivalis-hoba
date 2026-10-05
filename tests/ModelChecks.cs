using System.Numerics;
using System.Text.Json;
using NivalisMods.Hoba;

internal static class ModelChecks
{
    internal static void Run(string? output)
    {
        void Require(bool valid, string message) { if (!valid) throw new Exception(message); }
        Require(BoardDesigns.All.Length == 12 && BoardDesigns.All.Select(d=>d.Id).Distinct().Count()==12, "Twelve stable distinct designs");
        var manifest = new List<object>();
        var shapes = new HashSet<string>();
        foreach (var family in new[] { "hoba", "sc", "fc" })
        {
            Require(BoardDesigns.All.Count(d=>d.Family==family && !d.Legendary)==3, "Three marks per family");
            Require(BoardDesigns.All.Count(d=>d.Family==family && d.Legendary)==1, "One legendary per family");
            Require(BoardDesigns.Finishes.Count(f=>f.Family==family)==3, "Three exclusive finishes per family");
        }
        foreach (var d in BoardDesigns.All)
        {
            var parts = BoardVariants.Create(d);
            var vertices = parts.SelectMany(p=>p.Vertices).ToArray();
            Require(vertices.All(v=>float.IsFinite(v.X+v.Y+v.Z)), d.Id+" finite geometry");
            Require(vertices.All(v=>Math.Abs(v.X)<=.28f && Math.Abs(v.Z)<=.75f && v.Y>=-.18f && v.Y<=.27f),d.Id+" compact rider envelope");
            Require(parts.Count(p=>p.Name.StartsWith("White core"))==4,d.Id+" four lift cores");
            var signature = string.Join(";",vertices.Select(v=>$"{v.X:R},{v.Y:R},{v.Z:R}").Distinct().OrderBy(v=>v));
            Require(shapes.Add(signature),d.Id+" genuinely distinct geometry");
            ValidateSurface(parts, d.Id);
            // Split families must not acquire a hidden solid deck under their link.
            if(d.Family!="hoba")
            {
                Require(!parts.Where(p=>p.Name.Contains("pod")).SelectMany(p=>p.Vertices).Any(v=>Math.Abs(v.Z)<.12f),d.Id+" open central gap");
                Require(parts.Any(p=>p.Name.Contains(d.Family=="sc"?"beam":"Spine") || p.Name=="Energy tether"),d.Id+" energy link exists");
            }
            var finishes = d.Legendary ? new[] { "neutral" } : new[] { "neutral" }.Concat(BoardDesigns.Finishes.Where(f=>f.Family==d.Family).Select(f=>f.Id)).ToArray();
            foreach (var finish in finishes)
            {
                var colors=BoardDesigns.Palette(d,finish);
                Require(parts.All(p=>colors.ContainsKey(p.Finish)),"Every material has a palette color");
                if(finish=="neutral" && !d.Legendary)
                    foreach(var key in new[]{"shell","edge","yellow","mark","grip","metal","red"})
                        Require(colors[key].X==colors[key].Y && colors[key].Y==colors[key].Z,"Unpainted body is achromatic");
                if(output==null) continue;
                Directory.CreateDirectory(output);
                var id=d.Id+"-"+finish;
                Export(Path.Combine(output,id),parts,colors);
                manifest.Add(new { id, model=d.Id, family=d.Family, mark=d.Mark, name=d.Name, legendary=d.Legendary, finish, triangles=parts.Sum(p=>p.Triangles.Count/3) });
            }
            if(!d.Legendary)
            {
                var alien=BoardDesigns.Finishes.First(f=>f.Family!=d.Family).Id;
                var rejected=false;
                try { BoardDesigns.Palette(d,alien); } catch(ArgumentException) { rejected=true; }
                Require(rejected,"Reject cross-family paint");
            }
        }
        if (output != null)
            foreach (var finish in BoardDesigns.Finishes)
                Export(Path.Combine(output, "paint-" + finish.Id), PaintGeometry.Create(),
                    BoardDesigns.Palette(BoardDesigns.All.First(d => d.Family == finish.Family && !d.Legendary), finish.Id));
        if(output!=null) File.WriteAllText(Path.Combine(output,"catalog.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine("HOBA models: 12 distinct meshes, 9 family finishes, 39 appearances; compact bounds, closed voxel surfaces, material coverage and family restrictions passed.");
    }

    private static void ValidateSurface(BoardPart[] parts, string id)
    {
        var faces=new Dictionary<(int Axis,int Plane,int U,int V),int>();
        int Cell(float v)=>(int)MathF.Round(v/BoardGeometry.Grid);
        float Coord(Vector3 v,int a)=>a==0?v.X:a==1?v.Y:v.Z;
        foreach(var p in parts)
        for(var i=0;i<p.Vertices.Count;i+=6)
        {
            var q=p.Vertices.Skip(i).Take(6).ToArray();
            var n=Vector3.Cross(q[1]-q[0],q[2]-q[0]);
            if(new[]{n.X,n.Y,n.Z}.Count(v=>Math.Abs(v)>1e-9f)!=1) throw new Exception(id+" non-block face");
            var axis=Math.Abs(n.X)>1e-9f?0:Math.Abs(n.Y)>1e-9f?1:2;
            var ua=axis==0?1:0; var va=axis==2?1:2;
            foreach(var v in q)
                if(new[]{v.X,v.Y,v.Z}.Any(c=>Math.Abs(c/BoardGeometry.Grid-MathF.Round(c/BoardGeometry.Grid))>.0001f)) throw new Exception(id+" off-grid vertex");
            for(var u=Cell(q.Min(v=>Coord(v,ua)));u<Cell(q.Max(v=>Coord(v,ua)));u++)
            for(var v=Cell(q.Min(v=>Coord(v,va)));v<Cell(q.Max(v=>Coord(v,va)));v++)
                if(!faces.TryAdd((axis,Cell(Coord(q[0],axis)),u,v),Math.Sign(Coord(n,axis)))) throw new Exception(id+" overlapping surface");
        }
        foreach(var ray in faces.GroupBy(f=>(f.Key.Axis,f.Key.U,f.Key.V)))
        {
            var signs=ray.OrderBy(f=>f.Key.Plane).Select(f=>f.Value).ToArray();
            if(signs.Length%2!=0 || signs.Where((s,i)=>s!=(i%2==0?-1:1)).Any()) throw new Exception(id+" nonclosed or reversed surface");
        }
    }

    private static void Export(string path,BoardPart[] parts,Dictionary<string,Vector3> colors)
    {
        using var obj=new StreamWriter(path+".obj");
        obj.WriteLine("# Metres; +Y up; +Z forward. Generated HOBA model, not concept art.");
        obj.WriteLine("mtllib "+Path.GetFileName(path)+".mtl");
        var start=1;
        foreach(var part in parts)
        {
            obj.WriteLine("o "+part.Name.Replace(' ','_'));
            obj.WriteLine("usemtl "+part.Finish);
            foreach(var v in part.Vertices) obj.WriteLine(FormattableString.Invariant($"v {v.X:R} {v.Y:R} {v.Z:R}"));
            for(var i=0;i<part.Triangles.Count;i+=3) obj.WriteLine($"f {start+part.Triangles[i]} {start+part.Triangles[i+1]} {start+part.Triangles[i+2]}");
            start+=part.Vertices.Count;
        }
        using var mtl=new StreamWriter(path+".mtl");
        foreach(var (name,c) in colors)
        {
            mtl.WriteLine(FormattableString.Invariant($"newmtl {name}\nKd {c.X:R} {c.Y:R} {c.Z:R}\nKs 0.2 0.2 0.2\nNs 40"));
            if(name is "cyan" or "core") mtl.WriteLine(FormattableString.Invariant($"Ke {c.X*4:R} {c.Y*4:R} {c.Z*4:R}"));
        }
    }
}
