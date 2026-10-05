namespace NivalisMods.Hoba;

internal static class PaintTransaction
{
    // The target board is detached before its native type changes. Paint is the
    // final commit: failed placement or consumption restores the original board.
    internal static bool Apply(Func<bool> detachOriginal, Action<bool> setPainted, Func<bool> attachPainted,
        Func<bool> consumePaint, Func<bool> detachPainted, Action recoverOriginal)
    {
        if (!detachOriginal()) return false;
        var added = false;
        var committed = false;
        try
        {
            setPainted(true);
            added = attachPainted();
            if (!added || !consumePaint()) return false;
            committed = true;
            return true;
        }
        finally
        {
            if (!committed)
            {
                if (added && !detachPainted()) throw new InvalidOperationException("Cannot detach painted board during rollback.");
                setPainted(false);
                recoverOriginal();
            }
        }
    }
}
