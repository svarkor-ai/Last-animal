using System;
using LastAnimal.Combat;
using LastAnimal.Dna;
using Xunit;

// Last Animal — T3b runtime-ownership harness self-test (MC 1256.9, artemis,
// 2026-09-21). Two-sided calibration per the fleet convention: this test
// asserts a KNOWN-WRONG value and must FAIL when the suite runs with the
// harness included. The gate runs `dotnet test -p:IncludeHarness=false` for
// the green run; with the harness present the suite must report exactly this
// one failure.
namespace LastAnimal.Tests.Runtime;

public class RuntimeHarnessSelfTest
{
    [Fact]
    public void DeliberatelyBroken_KillPathBypass_IsWrong()
    {
        // DELIBERATELY BROKEN: claims a non-lethal hit fires the DNA extraction
        // (the old sidecar bug). CombatSystem only fires OnKill on a lethal
        // hit, so this assertion is wrong and must go red.
        var combat = new CombatSystem();
        var enemy = new EnemyAI(new CombatVec3(0f, 0f, 0f), entityId: 9001,
            EnemyAI.Type.Goblin, seed: 9001);
        int fired = 0;
        combat.DnaExtracted += _ => fired++;

        combat.DealDamage(0, enemy, 1);   // non-lethal

        Assert.Equal(1, fired);           // WRONG: must be 0 — this test is the calibration
    }
}
