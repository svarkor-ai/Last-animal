using LastAnimal.Companion;
using LastAnimal.Npc;
using Xunit;

// Last Animal — M05 companion-system harness self-test (MC 890.12, bernie, 2026-09-06).
//
// ONE deliberately-broken test so the suite has two-sided calibration, matching
// the dobbie M02/M03 HarnessSelfTest pattern (Phase-5 DoD: "a deliberately-broken
// case that goes red"). It proves the runner really executes and fails on a wrong
// assertion rather than silently passing everything.
namespace LastAnimal.Tests;

public class CompanionHarnessSelfTest
{
    [Fact]
    public void DeliberatelyBroken_Unpaid_ShouldNotRaiseLoyalty_IsWrong()
    {
        // The intended law (Phase 9): skipping a payment DROPS loyalty via M03,
        // so it can never INCREASE. This test asserts the opposite and MUST fail.
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 40 };
        var needs = new CompanionNeeds(graceTicks: 1);
        var sm = new CompanionStateMachine("garn", comp, needs);

        needs.TickAccompaniment();
        sm.Tick(); // -> Needing
        sm.SkipPayment();

        Assert.True(comp.Loyalty > 40); // WRONG on purpose: SkipSalary lowers it
    }
}
