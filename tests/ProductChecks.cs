using NivalisMods.Hoba;

internal static class ProductChecks
{
    internal static void Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var products = BoardProducts.All;
        Check(products.Length == 48 && products.Select(p => p.Id).Distinct().Count() == 48, "48 unique stable inventory types");
        Check(products.Count(p => p.ForSale && !p.IsPaint) == 9, "Nine purchasable boards");
        Check(products.Count(p => p.ForSale && p.IsPaint) == 9, "Nine purchasable paints");
        Check(products.Where(p => p.Legendary).All(p => !p.ForSale), "No legendary is sold");
        Check(BoardProducts.Get(BoardProducts.LegacyId).Model == BoardDesigns.DefaultId, "Old saves keep their existing board GUID");
        foreach (var board in products.Where(p => !p.IsPaint))
        foreach (var paint in products.Where(p => p.IsPaint))
        {
            var allowed = !board.Legendary && board.Family == paint.Family && board.Finish != paint.Finish;
            Check(BoardProducts.CanPaint(board, paint) == allowed, "Family/legendary/already-painted validation");
            if (allowed)
            {
                var changed = BoardProducts.Appearance(board.Model!, paint.Finish);
                Check(changed.Model == board.Model && changed.Price == board.Price && !changed.ForSale, "Paint keeps model/value tier and cannot enter regular stock");
            }
        }
        foreach (var fail in new[] { "none", "detach", "add", "consume", "throw" })
        {
            var boardCount = 1; var paints = 1; var painted = false; var recovered = false; var succeeded = false;
            try
            {
                succeeded = PaintTransaction.Apply(
                    () => { if (fail == "detach") return false; boardCount--; return true; },
                    value => painted = value,
                    () => { if (fail == "add") return false; boardCount++; return true; },
                    () => { if (fail == "throw") throw new InvalidOperationException("Injected native failure"); if (fail == "consume") return false; paints--; return true; },
                    () => { boardCount--; return true; },
                    () => { boardCount++; recovered = true; });
            }
            catch (InvalidOperationException) when (fail == "throw") { }
            Check(boardCount == 1, "No lost/duplicated board after " + fail);
            Check(paints == (fail == "none" ? 0 : 1), "Consume exactly one paint on successful commit only: " + fail);
            Check(painted == (fail == "none") && succeeded == (fail == "none"), "Correct board appearance after " + fail);
            Check(recovered == (fail is "add" or "consume" or "throw"), "Rollback restores detached original");
        }
        for (var i = 0; i < 300; i++)
        {
            var a = BoardArticulation.Sample(i*.016f, .405f, 1, 25);
            var b = BoardArticulation.Sample((i+1)*.016f, .405f, 1, 25);
            Check(Math.Abs(a.Height) <= .0141f && Math.Abs(a.Roll) < 2.1f, "Articulation stays subtle");
            Check(Math.Abs(a.Height-b.Height)<.001f, "Continuous bob without stepping");
        }
        Check(BoardArticulation.SpineTwist(0) == 0, "Straight travel untwists FC rings");
        Check(Math.Abs(BoardArticulation.SpineTwist(260)) > Math.Abs(BoardArticulation.SpineTwist(160)), "Sharper steering twists rings further");
        Check(BoardArticulation.SpineTwist(-160) == -BoardArticulation.SpineTwist(160), "Ring twist follows turn direction symmetrically");
        Check(Math.Abs(BoardArticulation.SpineTwist(1000)) <= 40, "Ring twist remains bounded");
        foreach (var turn in new[] { -260f, 160f, 260f })
        {
            var front = BoardArticulation.SteeringArc(.405f, turn, true);
            var rear = BoardArticulation.SteeringArc(-.405f, turn, true);
            var centre = BoardArticulation.SteeringArc(0, turn, false);
            Check(Math.Sign(front.Yaw) == Math.Sign(turn) && front.Yaw == -rear.Yaw, "Platforms yaw into a shared steering arc");
            Check(centre.Yaw == 0 && Math.Sign(centre.X) == -Math.Sign(turn), "Spine bows continuously between steered sockets");
            var left = BoardArticulation.SteeringArc(-.08f, turn, false);
            var right = BoardArticulation.SteeringArc(.08f, turn, false);
            Check(Math.Abs(left.X - right.X) < .00001f && Math.Abs(left.Yaw + right.Yaw) < .00001f, "Spine arc has symmetric tangents rather than an S bend");
        }
        Check(BoardArticulation.SteeringArc(.1f, 0, false) == (0f, .1f, 0f), "Straight travel restores straight spine");
        var stroke = new SprayStroke();
        stroke.Begin();
        Check(!stroke.Step(.3f, true), "Spray does not commit before animation finishes");
        Check(!stroke.Step(.4f, false) && !stroke.Active, "Lost aim/menu cancels before consuming paint");
        Check(!stroke.Step(1, true), "Cancelled stroke cannot resume and commit");
        stroke.Begin();
        Check(!stroke.Step(.3f, true), "New stroke restarts duration");
        stroke.Begin(); // Repeated click must not restart the stroke.
        Check(stroke.Step(.36f, true), "Valid stroke commits once");
        Check(!stroke.Step(1, true), "Completed stroke cannot consume another can");
        stroke.Begin(); stroke.Cancel();
        Check(!stroke.Active && stroke.Progress == 0, "Unequip/load clears a pending stroke");
        Console.WriteLine("HOBA spray: completion, cancellation, repeated clicks and one-shot commit checks passed.");
        Console.WriteLine("HOBA products: native-save IDs, sale exclusions, paint compatibility/rollback and bounded animation checks passed.");
    }
}
