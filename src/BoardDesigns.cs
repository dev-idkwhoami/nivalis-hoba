using System.Numerics;

namespace NivalisMods.Hoba;

// Visual definitions only. Ownership, prices and performance live outside this catalogue.
internal sealed record BoardDesign(string Id, string Family, int Mark, string Name, bool Legendary = false);
internal sealed record BoardFinish(string Id, string Family, string Name, Vector3 Shell, Vector3 Trim,
    Vector3 Edge, Vector3 Grip, Vector3 Cable);

internal static class BoardDesigns
{
    internal const string DefaultId = "hoba-mk1";
    internal static readonly BoardDesign[] All =
    {
        new(DefaultId, "hoba", 1, "HOBA Mark I"),
        new("hoba-mk2", "hoba", 2, "HOBA Mark II"),
        new("hoba-mk3", "hoba", 3, "HOBA Mark III"),
        new("sc-mk1", "sc", 1, "HOBA-SC Mark I"),
        new("sc-mk2", "sc", 2, "HOBA-SC Mark II"),
        new("sc-mk3", "sc", 3, "HOBA-SC Mark III"),
        new("fc-mk1", "fc", 1, "HOBA-FC Mark I"),
        new("fc-mk2", "fc", 2, "HOBA-FC Mark II"),
        new("fc-mk3", "fc", 3, "HOBA-FC Mark III"),
        new("hoba-foundry", "hoba", 3, "HOBA Foundry", true),
        new("sc-eclipse", "sc", 3, "HOBA-SC Eclipse", true),
        new("fc-aurora", "fc", 3, "HOBA-FC Aurora", true)
    };

    private static readonly BoardFinish[] PreviousFinishes =
    {
        new("industrial", "hoba", "Industrial", new(.24f,.29f,.085f), new(.95f,.57f,.045f), new(.48f,.49f,.43f), new(.025f,.029f,.026f), new(.55f,.045f,.025f)),
        new("porcelain", "hoba", "Porcelain", new(.82f,.84f,.78f), new(.025f,.40f,.39f), new(.51f,.62f,.60f), new(.025f,.035f,.04f), new(.04f,.25f,.24f)),
        new("copperwork", "hoba", "Copperwork", new(.47f,.23f,.105f), new(.73f,.47f,.16f), new(.31f,.18f,.08f), new(.15f,.065f,.026f), new(.32f,.16f,.07f)),
        new("obsidian", "sc", "Obsidian", new(.035f,.025f,.055f), new(.41f,.09f,.64f), new(.19f,.16f,.24f), new(.015f,.018f,.022f), new(.29f,.04f,.40f)),
        new("rescue", "sc", "Rescue", new(.88f,.22f,.035f), new(.85f,.78f,.56f), new(.44f,.43f,.38f), new(.035f,.03f,.02f), new(.13f,.14f,.14f)),
        new("glacier", "sc", "Glacier", new(.57f,.67f,.73f), new(.18f,.65f,.85f), new(.80f,.86f,.88f), new(.035f,.065f,.085f), new(.10f,.29f,.38f)),
        new("reactor", "fc", "Reactor", new(.07f,.09f,.08f), new(.50f,.83f,.035f), new(.28f,.34f,.26f), new(.025f,.03f,.02f), new(.32f,.47f,.035f)),
        new("relic", "fc", "Relic", new(.36f,.23f,.095f), new(.07f,.35f,.25f), new(.56f,.39f,.16f), new(.075f,.07f,.035f), new(.12f,.27f,.19f)),
        new("crimson", "fc", "Crimson", new(.39f,.035f,.045f), new(.67f,.12f,.08f), new(.21f,.23f,.25f), new(.025f,.02f,.025f), new(.12f,.14f,.16f))
    };

    internal static readonly BoardFinish[] Finishes = PreviousFinishes.Select(p => p.Id switch
    {
        "industrial" => p with { Shell = new(.39f,.51f,.045f), Trim = new(1f,.72f,.025f), Cable = new(.82f,.045f,.025f) },
        "porcelain" => p with { Shell = new(.94f,.96f,.90f), Trim = new(.015f,.69f,.64f), Cable = new(.025f,.43f,.39f) },
        "copperwork" => p with { Shell = new(.72f,.34f,.12f), Trim = new(.96f,.65f,.18f), Edge = new(.45f,.24f,.08f), Cable = new(.52f,.24f,.075f) },
        "obsidian" => p with { Shell = new(.07f,.035f,.115f), Trim = new(.67f,.10f,.96f), Edge = new(.29f,.20f,.39f), Cable = new(.52f,.04f,.74f) },
        "rescue" => p with { Shell = new(1f,.34f,.02f), Trim = new(1f,.91f,.66f), Cable = new(.16f,.18f,.18f) },
        "glacier" => p with { Shell = new(.66f,.83f,.94f), Trim = new(.035f,.78f,1f), Edge = new(.91f,.97f,1f), Cable = new(.04f,.46f,.62f) },
        "reactor" => p with { Shell = new(.085f,.13f,.095f), Trim = new(.64f,1f,.02f), Edge = new(.32f,.43f,.27f), Cable = new(.43f,.70f,.02f) },
        "relic" => p with { Shell = new(.56f,.35f,.10f), Trim = new(.035f,.62f,.39f), Edge = new(.78f,.54f,.18f), Cable = new(.065f,.43f,.26f) },
        "crimson" => p with { Shell = new(.75f,.025f,.055f), Trim = new(1f,.15f,.055f), Edge = new(.30f,.33f,.37f) },
        _ => p
    }).ToArray();

    // Upgrade untouched pre-vibrancy defaults, preserving user-customized RGB entries.
    internal static bool IsPreviousPaintColor(string finish, string part, Vector3 color)
    {
        var old = PreviousFinishes.FirstOrDefault(p => p.Id == finish);
        if (old == null) return false;
        Vector3? previous = part switch
        {
            "shell" => old.Shell, "yellow" => old.Trim, "edge" => old.Edge,
            "grip" => old.Grip, "red" => old.Cable, _ => null
        };
        return previous.HasValue && color == previous.Value;
    }

    internal static BoardDesign Get(string id) => All.SingleOrDefault(d => d.Id == id)
        ?? throw new ArgumentException("Unknown HOBA model: " + id, nameof(id));

    internal static Dictionary<string, Vector3> Palette(BoardDesign design, string finish = "neutral")
    {
        // Neutral bodywork still has rubber, bare metal, copper contacts and safety lamps.
        var colors = new Dictionary<string, Vector3>(BoardGeometry.Colors)
        {
            ["shell"] = new(.31f,.31f,.31f), ["edge"] = new(.53f,.53f,.53f),
            ["yellow"] = new(.41f,.41f,.41f), ["mark"] = new(.76f,.76f,.76f),
            ["grip"] = new(.025f,.025f,.025f), ["metal"] = new(.11f,.11f,.11f),
            ["red"] = new(.065f,.065f,.065f), ["signal"] = new(.65f,.025f,.015f)
        };
        BoardFinish? paint = null;
        if (design.Legendary)
        {
            if (finish != "neutral") throw new ArgumentException("Legendary models have their own exclusive finish.", nameof(finish));
            paint = design.Family switch
            {
                "hoba" => new("foundry", "hoba", "Foundry", new(.06f,.065f,.065f), new(.83f,.91f,.02f), new(.26f,.27f,.27f), new(.02f,.02f,.02f), new(.8f,.02f,.30f)),
                "sc" => new("eclipse", "sc", "Eclipse", new(.045f,.018f,.085f), new(.37f,.09f,.55f), new(.67f,.66f,.75f), new(.015f,.01f,.022f), new(.65f,.025f,.5f)),
                _ => new("aurora", "fc", "Aurora", new(.78f,.80f,.76f), new(.08f,.70f,.52f), new(.035f,.10f,.40f), new(.025f,.03f,.04f), new(.90f,.20f,.13f))
            };
            colors["paintA"] = design.Family == "hoba" ? new(.85f,.95f,.015f) : design.Family == "sc" ? new(.95f,.015f,.50f) : new(.95f,.23f,.13f);
            colors["paintB"] = design.Family == "hoba" ? new(.95f,.015f,.35f) : new(.02f,.85f,.65f);
            colors["paintPaper"] = new(.86f,.83f,.69f);
            colors["paintInk"] = new(.018f,.012f,.03f);
        }
        else if (finish != "neutral")
            paint = Finishes.SingleOrDefault(f => f.Id == finish && f.Family == design.Family)
                ?? throw new ArgumentException("Finish does not belong to this HOBA family: " + finish, nameof(finish));
        if (paint != null)
        {
            colors["shell"] = paint.Shell; colors["yellow"] = paint.Trim;
            colors["edge"] = paint.Edge; colors["grip"] = paint.Grip; colors["red"] = paint.Cable;
        }
        colors["field"] = new(.45f, .85f, 1);
        colors["exhaustBody"] = new(.45f, .8f, 1);
        colors["spillLight"] = new(.15f, .65f, 1);
        colors["exhaust"] = new(.4f, .78f, 1);
        colors["fieldSleeve"] = new(.035f, .22f, .32f);
        colors["fieldSleeveEmission"] = new(.04f, .5f, .8f);
        BoardTuning.ApplyPalette(design.Legendary ? design.Id : finish, colors);
        return colors;
    }

    internal static Vector3[] Jets(BoardDesign design) =>
        (from x in new[] { -.145f, .145f } from z in new[] { -.47f, .47f }
         select new Vector3(x, BoardGeometry.ThrusterBottom, z)).ToArray();
}
