using System.Numerics;

namespace NivalisMods.Hoba;

// All parts use the existing 1 cm solid-cell mesher, including exposed-face removal.
// Names preserve pod, spine and emitter boundaries for future articulation/export.
internal static class BoardVariants
{
    internal static BoardPart[] Create(BoardDesign design)
    {
        if (design.Id == BoardDesigns.DefaultId) return BoardGeometry.Create();
        var p = new List<BoardPart>();
        void Add(string name, string finish, float x, float y, float z, float w, float h, float l, float cut = .01f) =>
            p.Add(BoardGeometry.Box(name, finish, new(x,y,z), new(w,h,l), cut));
        var split = design.Family != "hoba";
        var mark = design.Mark;
        var legendary = design.Legendary;
        var center = split ? .405f : .35f;
        var length = split ? .46f : .44f;
        var width = mark == 1 ? .49f : mark == 2 ? .47f : .45f;
        var corner = mark == 1 ? .02f : mark == 2 ? .045f : .075f;

        foreach (var end in new[] { -1, 1 })
        {
            var z = end * center;
            var id = end > 0 ? "Front pod " : "Rear pod ";
            Add(id + "undertray", "metal", 0,-.045f,z,width-.02f,.05f,length,.03f);
            Add(id + "rim", "edge", 0,-.01f,z,width,.03f,length,corner);
            Add(id + "armor", "shell", 0,.025f,z,width-.015f,.06f,length-.02f,corner);
            Add(id + "pad surround", "yellow", 0,.065f,z,.345f,.025f,.33f,.025f);
            Add(id + "pad", "grip", 0,.08f,z,.31f,.02f,.30f,.02f);
            for (var i = -6; i <= 6; i++)
                Add(id + "traction " + i, "metal",0,.092f,z+i*.021f,.28f,.01f,.008f,0);
            foreach (var side in new[] { -1, 1 })
            {
                for (var i = -2; i <= 2; i++)
                {
                    Add(id + "side panel " + side + " " + i,"shell",side*(width/2-.015f),.025f,z+i*.068f,.035f,.07f,.058f,.005f);
                    Add(id + "vent " + side + " " + i,"metal",side*(width/2+.005f),.025f,z+i*.068f,.01f,.025f,.036f,0);
                    Add(id + "fastener " + side + " " + i,"edge",side*.20f,.068f,z+i*.065f,.014f,.01f,.014f,0);
                }
                Add(id + "accent rail " + side,"yellow",side*.19f,.065f,z,.022f,.018f,.28f,0);
                // Low side conduits do not intersect the top standing surface.
                Add(id + "conduit " + side,"red",side*.208f,-.012f,z,.012f,.015f,.29f,0);
                Add(id + "conduit socket " + side,"edge",side*.208f,-.012f,z-end*.15f,.024f,.028f,.028f,0);
            }
            Add(id + "outer bumper","edge",0,.035f,end*(center+length/2-.02f),width-.13f,.06f,.05f,.01f);
            Add(id + "lamp housing","metal",0,.065f,end*(center+length/2),.10f,.025f,.018f,0);
            Add(id + "lamp",end>0?"core":"signal",0,.067f,end*(center+length/2+.012f),.065f,.012f,.008f,0);

            if (split)
            {
                // Upturned outer shoulders; no physical bridge occupies the center gap.
                for (var step = 0; step < 3; step++)
                    Add(id + "kick shoulder " + step,"shell",0,.04f+step*.012f,end*(center+length/2+step*.02f),width-.12f-step*.05f,.025f,.03f,.01f);
                var inner = end*(center-length/2);
                Add(id + "emitter frame","edge",0,.015f,inner,.15f,.10f,.045f,.015f);
                Add(id + "emitter well","grip",0,.015f,inner-end*.026f,.115f,.065f,.015f,0);
                Add(id + "emitter","cyan",0,.015f,inner-end*.038f,.07f,.025f,.012f,0);
                if (mark == 1)
                    Add(id + "external service block","metal",.18f,.087f,z+end*.13f,.06f,.025f,.07f,.005f);
                if (mark >= 2)
                    for (var j = -2; j <= 2; j++)
                        Add(id + "flush cooling " + j,"metal",j*.033f,.066f,z+end*.18f,.019f,.01f,.026f,0);
            }
        }

        if (!split)
        {
            Add("Structural waist","metal",0,-.025f,0,mark==2?.18f:.25f,.07f,.42f,.02f);
            Add("Bridge armor","shell",0,.025f,0,mark==2?.17f:.26f,.06f,.33f,.02f);
            Add("Power cartridge frame","edge",0,.063f,0,.15f,.025f,.17f,.01f);
            Add("Power cartridge","grip",0,.079f,0,.12f,.015f,.14f,.01f);
            Add("Power readout","cyan",0,.09f,0,.08f,.01f,.023f,0);
            foreach (var side in new[] { -1, 1 })
            {
                Add("Waist feed " + side,"red",side*.10f,.028f,0,.015f,.02f,.25f,0);
                foreach (var end in new[] { -1, 1 })
                {
                    if (mark == 2 || end == 1)
                        Add("Fork " + side + " " + end,"shell",side*.17f,.02f,end*.625f,.10f,.065f,.19f,.02f);
                    else Add("Solid tail " + side,"shell",side*.095f,.025f,-.61f,.19f,.065f,.16f,.025f);
                    Add("Tip stripe " + side + " " + end,"yellow",side*.17f,.06f,end*.63f,.033f,.012f,.13f,0);
                }
            }
        }
        else if (design.Family == "sc")
        {
            if (legendary)
            {
                // Keep the connector open so its twin energy rails can bend freely.
                foreach (var x in new[] { -.038f, .038f })
                    Add("Twin field beam " + x,"core",x,.012f,0,.012f,.014f,.34f,0);
            }
            else Add("Energy tether","core",0,.015f,0,.014f,.014f,.34f,0);
        }
        else
        {
            var count = legendary ? 7 : mark + 2;
            var pitch = .30f / count;
            Add("Spine energy axis","cyan",0,.015f,0,.015f,.015f,.34f,0);
            for (var i = 0; i < count; i++)
            {
                var z = (i-(count-1)/2f)*pitch;
                var w = legendary ? .085f + .035f*(1-MathF.Abs(z)/.15f) : .115f - (mark-1)*.012f;
                Add("Spine " + i + " body","metal",0,.015f,z,w,.065f,pitch*.65f,.01f);
                Add("Spine " + i + " armor","shell",0,.053f,z,w+.02f,.02f,pitch*.65f,.01f);
                foreach (var side in new[] { -1, 1 })
                    Add("Spine " + i + " contact " + side,"yellow",side*w/2,.015f,z,.015f,.038f,pitch*.45f,0);
                if (legendary)
                    Add("Spine " + i + " aurora crown","core",0,.074f,z,.025f,.02f,pitch*.35f,0);
            }
        }

        if (legendary)
        {
            foreach (var end in new[] { -1, 1 })
            foreach (var side in new[] { -1, 1 })
            {
                for (var j = -2; j <= 2; j++)
                    Add("Legendary inlay " + end + " " + side + " " + j,"yellow",side*.205f,.083f,end*center+j*.045f,.02f,.018f,.025f,0);
                Add("Legendary shoulder " + end + " " + side,"edge",side*.205f,.095f,end*(center+.14f),.055f,.038f,.12f,.018f);
                if (design.Family == "hoba")
                {
                    // Foundry has external heat exchangers and guarded gold service rails.
                    for (var j = -3; j <= 3; j++)
                        Add("Foundry heat sink " + end + " " + side + " " + j,"copper",side*.18f,.09f,end*center+j*.033f,.045f,.035f,.01f,0);
                    Add("Foundry guard " + side,"yellow",side*.14f,.085f,0,.023f,.02f,.35f,0);
                }
                else if (design.Family == "sc")
                    Add("Eclipse light blade " + end + " " + side,"cyan",side*.228f,.055f,end*center,.01f,.013f,.18f,0);
                else
                    Add("Aurora light channel " + end + " " + side,"cyan",side*.193f,.077f,end*center,.01f,.012f,.23f,0);
            }
        }

        // Reuse the proven shallow nozzle stack and exact anchors used by runtime rings.
        foreach (var jet in BoardDesigns.Jets(design))
        {
            var id = jet.X + ":" + jet.Z;
            Add("Thruster housing " + id,"shell",jet.X,-.10f,jet.Z,.19f,.10f,.20f,.02f);
            Add("Thruster collar " + id,"edge",jet.X,-.16f,jet.Z,.20f,.025f,.21f,.025f);
            Add("Nozzle well " + id,"grip",jet.X,-.185f,jet.Z,.17f,.01f,.18f,.02f);
            Add("Blue nozzle " + id,"cyan",jet.X,-.195f,jet.Z,.14f,.01f,.15f,.02f);
            Add("White core " + id,"core",jet.X,-.205f,jet.Z,.09f,.01f,.10f,.01f);
        }
        if (legendary) LegendaryGraffiti.Apply(p, design);
        return BoardGeometry.Surface(p);
    }
}
