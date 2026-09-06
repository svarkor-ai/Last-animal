using LastAnimal.Companion;
using LastAnimal.Npc;
using Xunit;

// Last Animal — M05 companion-system test suite (MC 890.12, bernie, 2026-09-06).
//
// Phase 9 DoD (PHASE0.md): "A headless test drives a companion through
// follow -> need -> (unpaid) -> loyalty-drop transitions using M03's
// SalarySystem (exit 0)".
//
// Every loyalty mutation in these tests flows through M03 (SalarySystem /
// CompanionComponent); the M05 machine only reads M03 state and calls M03
// methods, performing no loyalty arithmetic itself (Phase-9 gate). The suite
// is enumerated (named [Fact]s), matching the dobbie M02/M03 suite style.
namespace LastAnimal.Tests;

public class CompanionNeedsTests
{
    [Fact]
    public void TickAccompaniment_NoSalaryDue_BeforeGrace()
    {
        var needs = new CompanionNeeds(graceTicks: 3);
        needs.TickAccompaniment();
        needs.TickAccompaniment();
        Assert.False(needs.SalaryDue);
    }

    [Fact]
    public void TickAccompaniment_SalaryDue_AtGraceBoundary()
    {
        var needs = new CompanionNeeds(graceTicks: 3);
        needs.TickAccompaniment();
        needs.TickAccompaniment();
        needs.TickAccompaniment();
        Assert.True(needs.SalaryDue);
    }

    [Fact]
    public void MarkPaid_ClearsDue_AndRestartsClock()
    {
        var needs = new CompanionNeeds(graceTicks: 2, payIntervalTicks: 2);
        needs.TickAccompaniment();
        needs.TickAccompaniment();
        Assert.True(needs.SalaryDue);

        needs.MarkPaid();
        Assert.False(needs.SalaryDue);
        Assert.Equal(0, needs.UnpaidTicks);

        // Next due comes PayIntervalTicks later, not immediately.
        needs.TickAccompaniment();
        Assert.False(needs.SalaryDue);
    }

    [Fact]
    public void AdvanceUnpaid_AccumulatesUnmetDuration()
    {
        var needs = new CompanionNeeds();
        Assert.Equal(0, needs.UnpaidTicks);
        needs.AdvanceUnpaid();
        needs.AdvanceUnpaid();
        Assert.Equal(2, needs.UnpaidTicks);
    }
}

public class CompanionStateMachineTests
{
    private const string CompanionName = "garn";

    [Fact]
    public void Follow_To_Need_WhenSalaryBecomesDue()
    {
        var needs = new CompanionNeeds(graceTicks: 2);
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 80 };
        var sm = new CompanionStateMachine(CompanionName, comp, needs);

        Assert.Equal(CompanionState.Following, sm.State);

        // Before the grace boundary the machine stays Following.
        needs.TickAccompaniment();
        Assert.Equal(CompanionState.Following, sm.Tick());

        // Once the world advance crosses the grace boundary the salary is
        // due, and the machine transitions Following -> Needing.
        needs.TickAccompaniment(); // crosses grace (accum 2 >= grace 2)
        Assert.Equal(CompanionState.Needing, sm.Tick());
    }

    [Fact]
    public void Follow_RemainsFollowing_WhileNoSalaryDue()
    {
        var needs = new CompanionNeeds(graceTicks: 100);
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 80 };
        var sm = new CompanionStateMachine(CompanionName, comp, needs);

        needs.TickAccompaniment();
        needs.TickAccompaniment();
        Assert.Equal(CompanionState.Following, sm.Tick());
    }

    [Fact]
    public void Pay_UsesM03SalarySystem_AndReturnsToFollowing()
    {
        // Loyalty 30 -> PayBonus +5 -> 35, via M03's SalarySystem.PaySalary.
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 30 };
        var needs = new CompanionNeeds(graceTicks: 1);
        var sm = new CompanionStateMachine(CompanionName, comp, needs);

        needs.TickAccompaniment();
        sm.Tick(); // -> Needing
        Assert.Equal(CompanionState.Needing, sm.State);

        sm.Pay(); // delegates to M03 SalarySystem.PaySalary

        Assert.Equal(CompanionState.Following, sm.State);
        Assert.Equal(35, comp.Loyalty); // M03 did +5, not the machine
        Assert.Equal(0, sm.UnpaidCycles);
        Assert.False(needs.SalaryDue);
    }

    [Fact]
    public void SkipPayment_DelegatesLoyaltyDrop_ToM03SalarySystem()
    {
        // Loyalty 20 -> SkipPenalty -3 -> 17, via M03 SalarySystem.SkipSalary.
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 20 };
        var needs = new CompanionNeeds(graceTicks: 1);
        var sm = new CompanionStateMachine(CompanionName, comp, needs);

        needs.TickAccompaniment();
        sm.Tick(); // -> Needing
        sm.SkipPayment(); // the "unpaid" arm — M03 drops loyalty

        Assert.Equal(17, comp.Loyalty); // M03 did -3, machine added none
        Assert.Equal(1, sm.UnpaidCycles);
    }

    [Fact]
    public void RepeatedUnpaid_UntilBetrayal_ThroughM03State()
    {
        // Drive loyalty from 10 down through repeated skips (each -3 via M03)
        // until M03's CheckBetrayal flips, and the machine mirrors Betrayed.
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 10 };
        var needs = new CompanionNeeds(graceTicks: 1);
        var sm = new CompanionStateMachine(CompanionName, comp, needs);

        needs.TickAccompaniment();
        sm.Tick(); // -> Needing (loyalty 10, not yet betrayable: >0)

        // Skips: 10->7->4->1->(-2 clamped to 0). On reaching 0 the next
        // Tick() reads betrayal through M03 and terminal-transitions.
        int skips = 0;
        while (sm.State != CompanionState.Betrayed && skips < 20)
        {
            sm.SkipPayment();
            sm.Tick();
            skips++;
        }

        Assert.Equal(CompanionState.Betrayed, sm.State);
        Assert.Equal(0, comp.Loyalty); // M03 clamped it at 0
        Assert.True(sm.UnpaidCycles >= 4); // 4-5 unpaid cycles to drain 10->0
    }

    [Fact]
    public void Betrayed_IsTerminal()
    {
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 0 };
        var needs = new CompanionNeeds(graceTicks: 1);
        var sm = new CompanionStateMachine(CompanionName, comp, needs);

        needs.TickAccompaniment();
        sm.Tick(); // betrayal already present -> Betrayed
        Assert.Equal(CompanionState.Betrayed, sm.State);

        // Paying a betrayed companion must no-op (M03 PaySalary returns true
        // but the machine is terminal; loyalty still climbs via M03 only).
        sm.Pay();
        Assert.Equal(CompanionState.Betrayed, sm.State);
    }
}

public class CompanionAnimationHookTests
{
    private const string Walk = "walkBaked"; // M09's retargeted clip

    [Fact]
    public void Resolve_ReturnsPerStateClips()
    {
        var hook = new CompanionAnimationHook(Walk, "needIdle", "betrayedIdle");
        Assert.Equal(Walk, hook.Resolve(CompanionState.Following));
        Assert.Equal("needIdle", hook.Resolve(CompanionState.Needing));
        Assert.Equal("betrayedIdle", hook.Resolve(CompanionState.Betrayed));
    }

    [Fact]
    public void Resolve_FromMachine_FollowingUsesWalk()
    {
        // Following companion resolves to M09's walkBaked clip (the animate-
        // via-retargeted-rig hook), matching the shared AnimationLibrary.
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 80 };
        var sm = new CompanionStateMachine("garn", comp, new CompanionNeeds(graceTicks: 99));
        var hook = new CompanionAnimationHook(Walk, "needIdle", "betrayedIdle");

        Assert.Equal(Walk, hook.Resolve(sm));
    }

    [Fact]
    public void Resolve_UnknownState_UsesFallback()
    {
        var hook = new CompanionAnimationHook(Walk, "needIdle", "betrayedIdle", fallbackClip: "RESET");
        Assert.Equal("RESET", hook.Resolve((CompanionState)999));
    }
}
