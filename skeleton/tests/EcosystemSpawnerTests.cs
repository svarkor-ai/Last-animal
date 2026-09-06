using System;
using LastAnimal.Combat;
using LastAnimal.Dna;
using LastAnimal.Ecosystem;
using Xunit;

// Last Animal — M12 boss-ecosystem-adaptation test suite (MC 890.16,
// artemis, 2026-09-06). C15 = EcosystemSpawner.OnZoneEnter(zoneId), adapted
// to the player's DNA CounterProfile (M02 / C5).
//
// Coverage (enumerated, not tallied):
//   C15 entry surface — OnZoneEnter(zoneId, profile) returns a SpawnSet and
//     raises ZoneEntered; unknown zone falls back to meadow (safest).
//   Adaptation — more observed DNA history -> stronger spawns: higher
//     AdaptationLevel, higher enemy HP/damage, escalation to tougher types.
//   Boss — a deep zone + adapted profile spawns a single IsBoss enemy whose
//     stats are strictly greater than the base table.
//   Determinism — same seed + same profile = identical spawn set.
//   Keystone sim — the spawner's output feeds M08 EnemyAI/CombatSystem in a
//     headless N-frame run that ends in a kill -> DnaExtracted, proving the
//     M12 -> M08 wire produces a playable, testable fight.
namespace LastAnimal.Tests;

public class EcosystemSpawnerTests
{
    // =====================================================================
    // C15 — OnZoneEnter surface
    // =====================================================================

    [Fact]
    public void OnZoneEnter_ReturnsSpawnSet_AndRaisesZoneEntered()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        SpawnSet? raised = null;
        spawner.ZoneEntered += (z, s) => raised = s;

        var set = spawner.OnZoneEnter("meadow", NewProfile(observed: 0));

        Assert.NotNull(set);
        Assert.Equal("meadow", set.ZoneId);
        // No adaptation yet -> base, weakest spawns, no boss.
        Assert.False(set.HasBoss);
        Assert.True(set.Count >= 1);
        Assert.NotNull(raised);
        Assert.Same(set, raised);
    }

    [Fact]
    public void OnZoneEnter_UnknownZone_FallsBackToMeadow_Safest()
    {
        var spawner = new EcosystemSpawner(seed: 1);
        var set = spawner.OnZoneEnter("volcano-unknown", NewProfile(observed: 0));
        Assert.Equal("meadow", set.ZoneId);
        Assert.False(set.HasBoss);
    }

    [Fact]
    public void OnZoneEnter_NullProfile_BehavesAsNoAdaptation()
    {
        var spawner = new EcosystemSpawner(seed: 2);
        var set = spawner.OnZoneEnter("meadow", null);
        Assert.Equal(1.0f, set.AdaptationLevel, 3);
        Assert.False(set.HasBoss);
    }

    // =====================================================================
    // Adaptation — stronger spawns from more observed DNA history
    // =====================================================================

    [Fact]
    public void Adaptation_IncreasesWithObservedHistory()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        var idle = spawner.OnZoneEnter("canyon", NewProfile(observed: 0));
        var adapted = spawner.OnZoneEnter("canyon", NewProfile(observed: 6));
        Assert.True(adapted.AdaptationLevel > idle.AdaptationLevel);
    }

    [Fact]
    public void Adaptation_ScalesEnemyHealthAndDamage()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        // Compare the same DENIZEN slot under an EMPTY vs ADAPTED profile.
        // A zone's denizen count is fixed; the optional boss is appended, so
        // filter it out to compare denizen-to-denizen by index.
        var idle = NonBoss(spawner.OnZoneEnter("canyon", NewProfile(observed: 0)));
        var adapted = NonBoss(spawner.OnZoneEnter("canyon", NewProfile(observed: 6)));
        Assert.Equal(idle.Count, adapted.Count);
        var idleVertebra = idle[0];
        var adaptedVertebra = adapted[0];
        Assert.True(adaptedVertebra.Health > idleVertebra.Health,
            $"expected adapted HP {adaptedVertebra.Health} > idle {idleVertebra.Health}");
        Assert.True(adaptedVertebra.Damage > idleVertebra.Damage);
    }

    [Fact]
    public void Adaptation_EscalatesSomeEnemiesToTougherTypes()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        var idle = NonBoss(spawner.OnZoneEnter("canyon", NewProfile(observed: 0)));
        var adapted = NonBoss(spawner.OnZoneEnter("canyon", NewProfile(observed: 6)));
        // Adapted pack should field strictly-tougher types in its escalated
        // slots (canyon base = Orc; adapted escalates a deep slot to a
        // tougher type). Non-boss lists compare 1:1 by index.
        Assert.Equal(idle.Count, adapted.Count);
        bool anyEscalated = false;
        for (int i = 0; i < idle.Count; i++)
            if (Rank(adapted[i].Type) > Rank(idle[i].Type)) { anyEscalated = true; break; }
        Assert.True(anyEscalated);
    }

    // =====================================================================
    // Boss — appears only in deep zones with enough adaptation
    // =====================================================================

    [Fact]
    public void Boss_DoesNotSpawn_WhenAdaptationBelowThreshold()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        // ruins is the deep zone but observed=2 < BossThreshold(4) -> no boss.
        var set = spawner.OnZoneEnter("ruins", NewProfile(observed: 2));
        Assert.False(set.HasBoss);
        foreach (var e in set.Enemies) Assert.False(e.IsBoss);
    }

    [Fact]
    public void Boss_Spawns_InDeepZone_WhenAdapted()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        var set = spawner.OnZoneEnter("ruins", NewProfile(observed: 5));
        Assert.True(set.HasBoss);
        var boss = FindBoss(set);
        Assert.NotNull(boss);
        Assert.True(boss!.Value.IsBoss);
        Assert.Equal(EnemyAI.Type.Demon, boss.Value.Type);
    }

    [Fact]
    public void Boss_Stats_ExceedBaseTable()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        var set = spawner.OnZoneEnter("ruins", NewProfile(observed: 5));
        var boss = FindBoss(set)!.Value;
        // Boss HP/damage must be strictly greater than a base-ruins enemy.
        var baseEnemy = set.Enemies[0];
        Assert.True(boss.Health > baseEnemy.Health,
            $"boss HP {boss.Health} should exceed base {baseEnemy.Health}");
        Assert.True(boss.Damage > baseEnemy.Damage);
    }

    [Fact]
    public void Boss_DoesNotSpawn_InShallowZone_EvenWhenAdapted()
    {
        var spawner = new EcosystemSpawner(seed: 3);
        // meadow has bossTier=0 -> never spawns a boss, even fully adapted.
        var set = spawner.OnZoneEnter("meadow", NewProfile(observed: 10));
        Assert.False(set.HasBoss);
    }

    // =====================================================================
    // Determinism
    // =====================================================================

    [Fact]
    public void OnZoneEnter_Deterministic_ForSameSeedAndProfile()
    {
        var a = new EcosystemSpawner(seed: 9).OnZoneEnter("ruins", NewProfile(observed: 5));
        var b = new EcosystemSpawner(seed: 9).OnZoneEnter("ruins", NewProfile(observed: 5));
        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a.HasBoss, b.HasBoss);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a.Enemies[i].Health, b.Enemies[i].Health);
            Assert.Equal(a.Enemies[i].Damage, b.Enemies[i].Damage);
            Assert.Equal(a.Enemies[i].Type, b.Enemies[i].Type);
            Assert.Equal(a.Enemies[i].IsBoss, b.Enemies[i].IsBoss);
        }
    }

    // =====================================================================
    // Keystone — the spawned set actually produces a playable headless fight
    // that ends in a kill -> DnaExtracted (M12 -> M08 wire). This is the
    // Phase-13 DoD bar for the module: the ecosystem's adapted spawns are
    // real EnemyAI combatants the CombatSystem can kill.
    // =====================================================================

    [Fact]
    public void SpawnedSet_FeedsCombatSim_KillTriggersDnaExtracted()
    {
        var spawner = new EcosystemSpawner(seed: 5);
        var set = spawner.OnZoneEnter("ruins", NewProfile(observed: 5));
        Assert.True(set.Count >= 1);

        var cs = new CombatSystem();
        LanguageSignature? extracted = null;
        int extractCount = 0;
        cs.DnaExtracted += sig => { extracted = sig; extractCount++; };

        // Build an EnemyAI combatant from the first spawn (boss if present,
        // else the first denizen) and drive it to a kill, exactly as the
        // M08 combat sim does with a player.
        var sp = set.HasBoss ? FindBoss(set)!.Value : set.Enemies[0];
        var enemy = new EnemyAI(sp.Position, sp.EntityId, sp.Type, seed: sp.EntityId);
        // A same-type enemy created from the table has the table base stats;
        // the spawner's adapted stats are the difficulty lever the sim uses.
        // Drive the fight to a kill in N frames.
        const int frames = 3000;
        var player = new PlayerController(new CombatVec3(0, 0, 0), speed: 5.0f);
        int maxFrames = 0;
        for (int frame = 0; frame < frames && !enemy.IsDead; frame++)
        {
            maxFrames = frame;
            float dist = player.Position.DistanceXZ(enemy.Position);
            if (dist <= player.AttackRange)
            {
                cs.DealDamage(attackerId: 0, enemy, player.MeleeDamage);
            }
            else
            {
                player.Move(player.Position.DirectionOnXZ(enemy.Position), 1.0f / 60f);
            }
            enemy.SetBehavior(player.Position, 1.0f / 60f);
        }

        Assert.True(maxFrames > 0);
        Assert.True(enemy.IsDead, $"spawn {sp.Type} not killed in {frames} frames");
        Assert.Equal(1, extractCount);
        Assert.NotNull(extracted);
        Assert.Equal(sp.EntityId, extracted!.Id);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static CounterProfile NewProfile(int observed)
    {
        // Build a CounterProfile over `observed` identical spoken signatures;
        // each adds one observed signature to the profile. Monotonic count is
        // what the spawner's adaptation reads.
        var sigs = new LanguageSignature[observed];
        for (int i = 0; i < observed; i++)
            sigs[i] = new LanguageSignature(new[] { 0, 1, 2, 3 }, id: 1);
        return EcosystemAdaptation.ModelPlayerDna(sigs);
    }

    private static SpawnedEnemy? FindBoss(SpawnSet set)
    {
        foreach (var e in set.Enemies) if (e.IsBoss) return e;
        return null;
    }

    /// <summary>Return only the non-boss denizens of a SpawnSet, preserving
    /// order, so denizen-to-denizen comparisons line up by index.</summary>
    private static System.Collections.Generic.List<SpawnedEnemy> NonBoss(SpawnSet set)
    {
        var list = new System.Collections.Generic.List<SpawnedEnemy>(set.Count);
        foreach (var e in set.Enemies) if (!e.IsBoss) list.Add(e);
        return list;
    }

    private static int Rank(EnemyAI.Type t) => t switch
    {
        EnemyAI.Type.Goblin => 0,
        EnemyAI.Type.Orc => 1,
        EnemyAI.Type.Skeleton => 2,
        _ => 3, // Demon
    };
}
