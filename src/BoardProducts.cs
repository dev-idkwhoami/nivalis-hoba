using System.Security.Cryptography;
using System.Text;

namespace NivalisMods.Hoba;

internal sealed record BoardProduct(string Id, string Name, string? Model, string Finish, string Family,
    int Price, bool ForSale, bool Legendary = false)
{
    internal bool IsPaint => Model == null;
}

internal static class BoardProducts
{
    internal const string LegacyId = "673e527a-75a5-4582-8b21-cc5286d461bc";
    internal static readonly BoardProduct[] All = Create();
    internal static BoardProduct Get(string id) => All.Single(p => p.Id == id);
    internal static string StableId(string key) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("HOBA.product." + key)).Take(16).ToArray()).ToString();
    internal static BoardProduct Appearance(string model, string finish) => All.Single(p => p.Model == model && p.Finish == finish);
    internal static bool CanPaint(BoardProduct board, BoardProduct paint) => !board.IsPaint && paint.IsPaint &&
        !board.Legendary && board.Family == paint.Family && board.Finish != paint.Finish;

    internal static void ApplyConfiguredPrices()
    {
        for (var i = 0; i < All.Length; i++)
        {
            var product = All[i];
            All[i] = product with { Price = BoardTuning.Price(product.Model ?? "paint." + product.Finish, product.Price) };
        }
    }

    private static BoardProduct[] Create()
    {
        var result = new List<BoardProduct>();
        foreach (var d in BoardDesigns.All)
        {
            var finishes = d.Legendary ? new[] { "neutral" } : new[] { "neutral" }.Concat(BoardDesigns.Finishes.Where(f => f.Family == d.Family).Select(f => f.Id));
            foreach (var finish in finishes)
            {
                var id = d.Id == BoardDesigns.DefaultId && finish == "neutral" ? LegacyId : StableId(d.Id + "." + finish);
                var name = d.Name + (finish == "neutral" ? "" : " — " + BoardDesigns.Finishes.Single(f => f.Id == finish).Name);
                result.Add(new(id, name, d.Id, finish, d.Family, 2000 + (d.Mark - 1) * 1000, !d.Legendary && finish == "neutral", d.Legendary));
            }
        }
        foreach (var f in BoardDesigns.Finishes)
        {
            var family = f.Family == "hoba" ? "HOBA" : "HOBA-" + f.Family.ToUpperInvariant();
            result.Add(new(StableId("paint." + f.Id), family + " paint — " + f.Name, null, f.Id, f.Family, 300, true));
        }
        return result.ToArray();
    }
}
