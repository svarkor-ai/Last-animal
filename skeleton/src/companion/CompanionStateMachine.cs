using System;

// Last Animal — M05 companion-system (MC 890.12, bernie, 2026-09-06).
//
// The companion's behavioral state machine. Pure logic: no Godot types,
// no window — runs headless under `dotnet test` (I3).
//
// The Phase 9 gate (PHASE0.md Phase 9) says: "no loyalty math here
// (delegated to M03)". Every loyalty mutation goes through M03's
// SalarySystem / CompanionComponent; this machine only OBSERVES loyalty
// to decide what the companion does next. It does not change loyalty itself.
//
// Transition set (the DoD's follow -> need -> (unpaid) -> loyalty-drop path):
//   - FOLLOWING: the companion trails the player. No salary is due yet.
//   - NEEDING:   a salary payment is due (the companionship has a cost).
//                 Entered by Update() when Needs.SalaryDue becomes true.
//   - From NEEDING, each unpaid cycle is a real call to
//                 M03 SalarySystem.SkipSalary inside TryStarvation() (the
//                 "loyalty-drop" half) — the else-branch of trying to pay.
//                 So starving is literally repeated skips while needing.
//   - BETRAYED:  M03's BetrayalSystem.CheckBetrayal(comp) is true (loyalty
//                 dropped to 0 with an active companion); the M05 state
//                 mirrors the M03 betrayal the owner will Execute.
//
// The chosen play filter is `following -> needing -> (pay|skip)* -> betrayed`
// — deterministic, testable headless, and driven entirely by reading M03
// state + calling M03 methods. The companion never mutates loyalty itself.
namespace LastAnimal.Companion;

using LastAnimal.Npc;

/// <summary>The top-level companion behavioral state (the follow/need loop).</summary>
public enum CompanionState
{
    /// <summary>Trailing the player; companionship cost not yet due.</summary>
    Following,
    /// <summary>A salary payment is due and the companion is waiting for it.</summary>
    Needing,
    /// <summary>The companion has turned (loyalty hit 0 while bonded — M03 betrayal state).</summary>
    Betrayed,
}

/// <summary>
/// The companion behavior state machine (M05). Driven by M03's loyalty +
/// SalarySystem; performs NO loyalty math itself (Phase 9 gate).
/// </summary>
public class CompanionStateMachine
{
    private readonly CompanionNeeds _needs;
    private readonly SalarySystem _salary;
    private readonly BetrayalSystem _betrayal;

    /// <summary>Diag name for logs / tests; also the animation hook key.</summary>
    public string Name { get; }

    /// <summary>The M03 companion data this machine observes.</summary>
    public CompanionComponent Companion { get; }

    public CompanionState State { get; private set; } = CompanionState.Following;

    /// <summary>
    /// How many salary cycles were skipped while in the Needing state since
    /// the last successful pay (feeds the "unpaid -> loyalty drop" DoD path).
    /// Reset to 0 on a successful pay.
    /// </summary>
    public int UnpaidCycles { get; private set; }

    public CompanionStateMachine(
        string name,
        CompanionComponent companion,
        CompanionNeeds needs,
        SalarySystem? salary = null,
        BetrayalSystem? betrayal = null)
    {
        Name = name;
        Companion = companion;
        _needs = needs ?? new CompanionNeeds();
        _salary = salary ?? new SalarySystem();
        _betrayal = betrayal ?? new BetrayalSystem();
    }

    /// <summary>
    /// Advance one tick of the companion's behavior loop. Returns the new
    /// state after processing this tick (may be unchanged).
    /// </summary>
    public CompanionState Tick()
    {
        // Betrayal is an out-of-order terminal state: if M03 says this
        // companion has turned, mirror it and stop the follow/need loop.
        if (_betrayal.CheckBetrayal(Companion))
        {
            State = CompanionState.Betrayed;
            return State;
        }

        switch (State)
        {
            case CompanionState.Following:
                // Follow silently until a payment becomes due.
                if (_needs.SalaryDue)
                {
                    State = CompanionState.Needing;
                }
                break;

            case CompanionState.Needing:
                // Need is active: the accompany loop must try to settle the
                // payment — guarded by M03, which is the ONLY author of
                // loyalty change here.
                TrySettle();
                break;

            case CompanionState.Betrayed:
                // Terminal — no further transitions from the follow/need loop.
                break;
        }

        return State;
    }

    /// <summary>
    /// Mark the payment as made and settle the need this cycle (resets the
    /// unpaid counter). Pure chase-through of M03's PaySalary; the caller
    /// (or the salary due logic) decides whether funds exist.
    /// </summary>
    public void Pay()
    {
        if (_salary.PaySalary(Companion))
        {
            _needs.MarkPaid();
            UnpaidCycles = 0;
            if (State == CompanionState.Needing)
            {
                State = CompanionState.Following;
            }
        }
    }

    /// <summary>
    /// The "unpaid" half of the DoD path: the companion's needs went
    /// unsatisfied, so M03's SkipSalary drains loyalty (a real loyalty-drop).
    /// The machine itself still performs no arithmetic — M03 does.
    /// </summary>
    public void SkipPayment()
    {
        if (_salary.SkipSalary(Companion))
        {
            _needs.AdvanceUnpaid();
            UnpaidCycles++;
            // No state change here today — the NEXT Tick() reads the dropped
            // loyalty through M03's CheckBetrayal and terminal-transitions.
        }
    }

    private void TrySettle()
    {
        // This machine has no notion of a wallet; it defers to whatever
        // policy the caller wired onto _needs. By default a need is not
        // 'payable' until the caller decides, so SkipPayment is the steady
        // fall-through. The headless DoD test drives Pay() vs SkipPayment()
        // explicitly to prove both arms.
        if (_needs.TrySettlePayment())
        {
            Pay();
        }
    }
}
