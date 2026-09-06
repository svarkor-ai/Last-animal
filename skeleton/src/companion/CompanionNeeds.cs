using System;

// Last Animal — M05 companion-system (MC 890.12, bernie, 2026-09-06).
//
// The companion's material NEEDS: what makes a settlement due and when one
// is 'satisfied'. Pure logic (I3), no Godot types.
//
// Design note: the PHASE0 Phase-9 gate names "follow/needs" — a companion
// that currently needs something. The concrete need this module models is
// the companionship wage (the M03 force): a payment becomes due on a cadence
// and stays due until paid. This file is deliberately small — it only owns
// the *due-ness* bookkeeping, never the loyalty change (that is M03's).
namespace LastAnimal.Companion;

/// <summary>
/// The companion's need ledger: when a wage payment is due and how long it
/// has gone unmet. Payments are settled via M05's Pay()/SkipPayment(), which
/// delegate the loyalty effect to M03 SalarySystem.
/// </summary>
public class CompanionNeeds
{
    /// <summary>Ticks of accompaniment before the first wage is demanded.</summary>
    public int GraceTicks { get; set; }

    /// <summary>Ticks between demanded payments once the companion is bonded.</summary>
    public int PayIntervalTicks { get; set; }

    /// <summary>True when a wage is currently owed and unpaid.</summary>
    public bool SalaryDue { get; private set; }

    /// <summary>How many due-ticks the current wage has gone unpaid.</summary>
    public int UnpaidTicks { get; private set; }

    public CompanionNeeds(int graceTicks = 5, int payIntervalTicks = 8)
    {
        GraceTicks = graceTicks;
        PayIntervalTicks = payIntervalTicks;
    }

    /// <summary>Ticks of accompaniment accumulating (advance the due clock).</summary>
    public void TickAccompaniment()
    {
        // No loyalty math here — this only flips the "something is owed now"
        // flag as the companionship clock passes the grace period and then
        // recurs on the pay interval.
        if (!SalaryDue)
        {
            _accumulated++;
            if (_accumulated >= GraceTicks &&
                (_accumulated - GraceTicks) % PayIntervalTicks == 0)
            {
                SalaryDue = true;
            }
        }
    }

    /// <summary>Mark the current wage as paid; the next is due in PayIntervalTicks.</summary>
    public void MarkPaid()
    {
        SalaryDue = false;
        UnpaidTicks = 0;
        _accumulated = 0; // restart the interval clock from the pay moment
    }

    /// <summary>Advance the unmet-duration (an unpaid cycle passed).</summary>
    public void AdvanceUnpaid()
    {
        UnpaidTicks++;
    }

    /// <summary>
    /// The settlement policy hook: by default a wage is only 'settled' through
    /// an explicit Pay(); returns false so the steady state falls through to
    /// SkipPayment (the unpaid arm). Callers may override the policy.
    /// </summary>
    public virtual bool TrySettlePayment() => false;

    private int _accumulated;
}
