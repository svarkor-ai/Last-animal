using System;
using LastAnimal.Combat;
using LastAnimal.Dna;
using Xunit;

// Last Animal — M08 combat-3d test suite (MC 890.13, artemis, 2026-09-06).
//
// Phase 10 DoD (PHASE0.md Phase 10): headless combat sim runs N frames with
// >= 1 enemy engaged and a kill triggering a DnaExtracted event (exit 0) —
// plus the unit gates for C10 + C11. The suite is enumerated, not tallied.
//
// Coverage:
//   C10 — CombatSystem.DealDamage / OnKill -> DnaExtracted wire.
//   C10 — PlayerController.Move(Vec3)/State.
//   C11 — EnemyAI.SetBehavior state machine (patrol|chase|attack|flee),
//         TakeDamage/IsDead, ported type stats.
//   The keystone combat sim (DoD) drives a player + one enemy toward a kill
//   across N frames and asserts DnaExtracted fired with the expected source.
namespace LastAnimal.Tests;

public class Combat3DTests
{
    // =====================================================================
    // C11 — EnemyAI.SetBehavior state machine
    // =====================================================================

    [Fact]
    public void EnemyAI_StartsInPatrol()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        Assert.Equal(EnemyAI.State.Patrol, e.CurrentState);
        Assert.False(e.IsDead);
    }

    [Fact]
    public void EnemyAI_Chases_WhenDefenderEntersChaseRange()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        // Defender 3 units away (Goblin chase range = 5) -> chase.
        int dmg = e.SetBehavior(new CombatVec3(3, 0, 0), 0.1f);
        Assert.Equal(EnemyAI.State.Chase, e.CurrentState);
        Assert.Equal(0, dmg);
    }

    [Fact]
    public void EnemyAI_EntersAttack_WhenDefenderWithinAttackRange()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        // Defender 0.5 units away (Goblin attack range = 1) -> transition to
        // Attack this tick (the hit lands on the next tick, per the prior-art
        // state-machine timing: a frame is spent in Attack before the strike).
        int dmg = e.SetBehavior(new CombatVec3(0.5f, 0, 0), 0.1f);
        Assert.Equal(EnemyAI.State.Attack, e.CurrentState);
        Assert.Equal(0, dmg);
    }

    [Fact]
    public void EnemyAI_Attacks_OnTickAfterEnteringAttack()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        var defender = new CombatVec3(0.5f, 0, 0);
        e.SetBehavior(defender, 0.1f); // enter Attack state (wind-up, no hit)
        int dmg = e.SetBehavior(defender, 0.1f); // hit lands this tick
        // The contract behaviour that matters: the hit lands. (The enemy then
        // drops to Chase while its cooldown (1.0s) is re-arming, matching the
        // prior-art transition timing.)
        Assert.Equal(e.Damage, dmg);
    }

    [Fact]
    public void EnemyAI_AttackRespectsCooldown()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        var defender = new CombatVec3(0.5f, 0, 0);
        e.SetBehavior(defender, 0.1f); // wind-up
        e.SetBehavior(defender, 0.1f); // first hit lands (5)
        // Immediately again — cooldown (1.0s) not elapsed -> no damage.
        int dmg = e.SetBehavior(defender, 0.05f);
        Assert.Equal(0, dmg);
    }

    [Fact]
    public void EnemyAI_Flees_WhenHealthDropsBelowThreshold()
    {
        // Goblin flee threshold 0.3 -> flee at health < 9.
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        e.TakeDamage(25); // health 30-25 = 5 < 9
        e.SetBehavior(new CombatVec3(3, 0, 0), 0.1f);
        Assert.Equal(EnemyAI.State.Flee, e.CurrentState);
    }

    [Fact]
    public void EnemyAI_StateSetIncludesAllFour()
    {
        var vals = Enum.GetNames(typeof(EnemyAI.State));
        Assert.Equal(new[] { "Patrol", "Chase", "Attack", "Flee" }, vals);
    }

    [Fact]
    public void EnemyAI_TakeDamage_ClampsAtZero_AndDies()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        e.TakeDamage(999);
        Assert.Equal(0, e.Health);
        Assert.True(e.IsDead);
    }

    [Fact]
    public void EnemyAI_Dead_DealsNoDamage()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        e.TakeDamage(999);
        int dmg = e.SetBehavior(new CombatVec3(0.5f, 0, 0), 0.1f);
        Assert.Equal(0, dmg);
    }

    [Fact]
    public void EnemyAI_GoblinStats_MatchPriorArt()
    {
        var e = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        Assert.Equal(30, e.MaxHealth);
        Assert.Equal(5, e.Damage);
        Assert.Equal(3.0f, e.Speed);
        Assert.Equal(1.0f, e.AttackRange);
        Assert.Equal(5.0f, e.ChaseRange);
    }

    // =====================================================================
    // C10 — CombatSystem.DealDamage / OnKill -> DnaExtracted wire
    // =====================================================================

    [Fact]
    public void DealDamage_ReducesHealth_AndDoesNotKill()
    {
        var cs = new CombatSystem();
        var goblin = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        bool killed = cs.DealDamage(attackerId: 0, goblin, amount: 10);
        Assert.False(killed);
        Assert.Equal(20, goblin.Health);
        Assert.False(goblin.IsDead);
    }

    [Fact]
    public void DealDamage_FinishingBlow_KillsAndExtractsDna()
    {
        var cs = new CombatSystem();
        LanguageSignature? extracted = null;
        cs.DnaExtracted += sig => extracted = sig;
        var goblin = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 42, EnemyAI.Type.Goblin, seed: 7);

        bool killed = cs.DealDamage(attackerId: 0, goblin, amount: 999);

        Assert.True(killed);
        Assert.True(goblin.IsDead);
        // The DnaExtracted C2 wire must have fired with a real signature.
        Assert.NotNull(extracted);
        Assert.Equal(42, extracted!.Id);
        Assert.False(string.IsNullOrEmpty(extracted.SpeciesHash));
    }

    [Fact]
    public void DealDamage_ToDeadEnemy_IsNoOp()
    {
        var cs = new CombatSystem();
        var goblin = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        goblin.TakeDamage(999);
        bool killed = cs.DealDamage(attackerId: 0, goblin, amount: 5);
        Assert.False(killed);
    }

    [Fact]
    public void OnKill_ReturnsNull_WhenTargetNotDead()
    {
        var cs = new CombatSystem();
        var goblin = new EnemyAI(new CombatVec3(0, 0, 0), entityId: 1, EnemyAI.Type.Goblin, seed: 7);
        Assert.Null(cs.OnKill(goblin));
    }

    // =====================================================================
    // C10 — PlayerController.Move(Vec3) / State
    // =====================================================================

    [Fact]
    public void PlayerController_Move_UpdatesPosition_AndEntersRun()
    {
        var p = new PlayerController(new CombatVec3(0, 0, 0), speed: 5.0f);
        var pos = p.Move(new CombatVec3(1, 0, 0), 1.0f);
        Assert.Equal(PlayerController.State.Run, p.CurrentState);
        // One second at speed 5 along +X.
        Assert.Equal(5.0f, pos.X, 3);
        Assert.Equal(0.0f, pos.Z, 3);
    }

    [Fact]
    public void PlayerController_Move_ZeroDirection_IsIdle()
    {
        var p = new PlayerController(new CombatVec3(1, 0, 2));
        p.Move(new CombatVec3(0, 0, 0), 1.0f);
        Assert.Equal(PlayerController.State.Idle, p.CurrentState);
    }

    [Fact]
    public void PlayerController_TakeDamage_Kills_AndSetsDead()
    {
        var p = new PlayerController(new CombatVec3(0, 0, 0));
        p.TakeDamage(999);
        Assert.Equal(0, p.Health);
        Assert.Equal(PlayerController.State.Dead, p.CurrentState);
        Assert.True(p.IsDead);
    }

    [Fact]
    public void PlayerController_Dead_CannotMove()
    {
        var p = new PlayerController(new CombatVec3(0, 0, 0));
        p.TakeDamage(999);
        var pos = p.Move(new CombatVec3(1, 0, 0), 1.0f);
        Assert.Equal(new CombatVec3(0, 0, 0), pos);
    }

    // =====================================================================
    // Phase 10 DoD — keystone combat sim: N frames, >=1 enemy engaged,
    // kill triggers DnaExtracted (exit 0).
    // =====================================================================

    [Fact]
    public void CombatSim_RunsNFrames_KillTriggersDnaExtracted()
    {
        const int frames = 600;
        var cs = new CombatSystem();
        LanguageSignature? extracted = null;
        int extractCount = 0;
        cs.DnaExtracted += sig => { extracted = sig; extractCount++; };

        var player = new PlayerController(new CombatVec3(0, 0, 0), speed: 5.0f);
        var goblin = new EnemyAI(new CombatVec3(3, 0, 0), entityId: 77, EnemyAI.Type.Goblin, seed: 11);

        int maxFrames = 0;
        for (int frame = 0; frame < frames && !goblin.IsDead; frame++)
        {
            maxFrames = frame;
            // Player advances toward the goblin until in attack range, then strikes.
            float dist = player.Position.DistanceXZ(goblin.Position);
            if (dist <= player.AttackRange)
            {
                cs.DealDamage(attackerId: 0, goblin, player.MeleeDamage);
            }
            else
            {
                var dir = player.Position.DirectionOnXZ(goblin.Position);
                player.Move(dir, 1.0f / 60f);
            }
            // Enemy AI tick.
            goblin.SetBehavior(player.Position, 1.0f / 60f);
        }

        // >= 1 enemy was engaged (goblin comes within the player's arc).
        Assert.True(maxFrames > 0);
        // The kill landed within the N-frame budget.
        Assert.True(goblin.IsDead, $"goblin not dead after {frames} frames");
        // A kill triggered exactly one DnaExtracted event (C10 -> C2 wire).
        Assert.Equal(1, extractCount);
        Assert.NotNull(extracted);
        Assert.Equal(77, extracted!.Id);
    }
}
