using LastAnimal.Combat;
using Xunit;

// Last Animal — M08 combat-3d harness self-test (MC 890.13, artemis, 2026-09-06).
//
// The Phase-10 DoD requires ONE deliberately-broken case that goes red — a
// two-sided calibration proving the test harness can actually fail (same
// pattern as the Phase-5 harness self-test, ci/dna_npc_test.sh). This test
// asserts a KNOWN-WRONG value: it is expected to FAIL (go red).
//
// The gate script (ci/combat_test.sh) runs the suite TWICE:
//   1. With this file present -> exactly 1 failure (this test), all others green.
//   2. With this file excluded -> 0 failures.
// If the harness cannot fail, the gate fails — the harness is broken.
namespace LastAnimal.Tests;

public class CombatHarnessSelfTest
{
    [Fact]
    public void DeliberatelyBroken_EnemyStartState_IsWrong()
    {
        // WRONG: claims a fresh Goblin starts DEAD. A living goblin is NOT
        // dead. This is DELIBERATELY BROKEN — it must go red.
        var goblin = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        Assert.True(goblin.IsDead);
    }
}
