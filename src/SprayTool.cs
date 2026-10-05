using Nivalis;
using UnityEngine;

namespace NivalisMods.Hoba;

// A reversible first-person tool. The native inventory owns the consumable;
// holding/putting it away does not remove an item or create a saved entity.
internal static class SprayTool
{
    private static BoardProduct? _paint;
    private static PlayerCharacter? _player;
    private static SprayCanModel? _view;
    private static readonly SprayStroke Stroke = new();
    private static string? _targetId;
    private static int _equipFrame, _consumeFrame = -1;
    internal static bool ConsumesInput => _consumeFrame == Time.frameCount;
    private static bool _suspended;
    internal static bool Equipped => _paint != null;
    internal static string Prompt
    {
        get
        {
            if (Stroke.Active) return "Repainting…";
            var board = BoardItem.Deployed;
            if (board == null || _paint == null) return "";
            if (board.Legendary) return "Legendary finish — cannot repaint";
            if (board.Family != _paint.Family) return "Paint belongs to a different HOBA family";
            if (board.Finish == _paint.Finish) return "This board already has that finish";
            var name = BoardDesigns.Finishes.Single(f => f.Id == _paint.Finish).Name;
            return "[LMB] Repaint — " + name + "  •  [Shift + LMB] Pick up";
        }
    }

    internal static void Equip(BoardProduct paint, RideController ride)
    {
        if (_paint?.Id == paint.Id) { Unequip(); BoardItem.Notify("Spray can put away."); return; }
        Unequip();
        var player = PlayerManager._instance?.LocalPlayer?.Character;
        if (player == null || ride.IsMounted || !RideController.CanRide(player))
        { BoardItem.Notify("Get off the board and stand with empty hands to equip paint."); return; }
        if (!BoardItem.HasPaint(paint)) return;
        try
        {
            _view = new SprayCanModel(player, paint);
            _player = player; _paint = paint; _equipFrame = Time.frameCount;
            _consumeFrame = Time.frameCount;
            BoardItem.Notify("Spray can equipped. Aim at a compatible parked board and left-click. Use this paint again to put it away.");
        }
        catch (Exception e)
        {
            Unequip();
            Plugin.Logger.LogWarning("HOBA spray equip: " + e);
            BoardItem.Notify("Could not equip the spray can. Stand with empty hands and try again.");
        }
    }

    internal static void Click(RideController ride)
    {
        if (_paint == null || Stroke.Active || Time.frameCount <= _equipFrame || !ride.CanReachParked()) return;
        var board = BoardItem.Deployed;
        if (board == null || !BoardProducts.CanPaint(board, _paint)) { BoardItem.Notify(Prompt); return; }
        if (!BoardItem.HasPaint(_paint)) { Unequip(); return; }
        _targetId = board.Id;
        Stroke.Begin();
    }

    internal static void Tick(RideController ride)
    {
        if (_paint == null) return;
        try
        {
            var player = PlayerManager._instance?.LocalPlayer?.Character;
            if (player == null || _player == null || player.Pointer != _player.Pointer || ride.IsMounted || !BoardItem.HasPaint(_paint))
            { Unequip(); return; }
            _view?.ReleasePose();
            var active = RideController.GameplayAvailable();
            if (!active)
            {
                Stroke.Cancel(); _suspended = true;
                _view?.SetVisible(false);
                return;
            }
            if (!RideController.CanRide(player)) { Unequip(); return; }
            if (_suspended) { _suspended = false; _equipFrame = Time.frameCount; }
            _view?.SetVisible(true);
            var valid = Stroke.Active && BoardItem.Deployed?.Id == _targetId && ride.CanReachParked();
            if (Stroke.Step(Time.deltaTime, valid))
            {
                _consumeFrame = Time.frameCount;
                if (!ride.RepaintParked(_paint)) BoardItem.Notify("Repaint cancelled; paint was not consumed.");
                Stroke.Cancel();
                if (!BoardItem.HasPaint(_paint)) { Unequip(); return; }
            }
            _view?.Animate(Stroke.Active, Stroke.Progress, Stroke.Active && valid ? ride.PaintPoint : null);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("HOBA spray: " + e);
            Unequip();
            BoardItem.Notify("Spray tool stopped. See the HOBA log.");
        }
    }

    internal static void Present() => _view?.PoseHand();
    internal static void Unequip()
    {
        _paint = null; _player = null; _targetId = null; _suspended = false; Stroke.Cancel();
        var view = _view; _view = null;
        try { view?.Dispose(); }
        catch (Exception e) { Plugin.Logger.LogWarning("HOBA spray cleanup: " + e.Message); }
        ParkingPrompt.Release();
    }
}
