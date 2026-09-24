using System;
using System.Collections.Generic;
using LastAnimal.Combat;
using LastAnimal.Dna;

// Last Animal — M12 boss-ecosystem-adaptation (MC 890.16, artemis, 2026-09-06).
//
// C15 (PHASE0.md C15 row): `EcosystemSpawner.OnZoneEnter(zoneId)` — the
// ecosystem spawner hook that M07's zone.gd exposes a stable anchor for (the
// `Spawners` node's `zone_id` metadata; see zones/zone.gd). When the player
// enters a zone the ecosystem fielded its denizens, ADAPTED to the player's
// spoken-DNA history (the C5 CounterProfile from M02's EcosystemAdaptation).
//
// Design (pure logic, no Godot types, no window — I3):
//   - OnZoneEnter is the C15 entry point; it builds an immutable SpawnSet from
//     the canonical zone's denizen table and the player's CounterProfile.
//   - A zone ALWAYS fields its denizen count (meadow 3 / canyon 4 / ruins 5).
//     ADAPTATION (the module's whole point) does not change how many enemies a
//     zone holds — it changes how MEAN they are: their HP/damage scale with
//     the profile, the deepest slots escalate the enemy type up the ladder,
//     and once the ecosystem has observed enough of the player's spoken DNA
//     (CounterProfile.ObservedCount >= BossThreshold) a BOSS spawns whose
//     stats compound the zone depth. This is the "the world strikes back"
//     payoff of the DNA language: a player who leans on the mechanic teaches
//     the ecosystem counters and walks into meaner zones.
//   - Unknown zone ids fall back to the meadow (safest) table, canonicalized
//     so the SpawnSet reports the table actually used.
//   - The spawner raises a C# ZoneEntered event that the Godot seam binds to
//     EventBus (C2 EcosystemAdapted) when the profile changed. It does not
//     touch the bus itself (I4 — no cross-module refs into autoload/).
//
// Constant/uniform gameplay (no drift): every stat is a pure function of the
// zone + profile + a caller-supplied seed, so OnZoneEnter is deterministic
// and fully testable headless. A null/empty profile means the player has not
// yet used the DNA mechanic — the ecosystem has NOT adapted, so spawns stay
// at the base, weakest table.
namespace LastAnimal.Ecosystem;

/// <summary>A single spawn produced by OnZoneEnter: an enemy, or the zone boss.</summary>
public readonly struct SpawnedEnemy
{
    public EnemyAI.Type Type { get; }
    public int Health { get; }
    public int Damage { get; }
    public float Speed { get; }
    public CombatVec3 Position { get; }
    public bool IsBoss { get; }
    public int EntityId { get; }

    public SpawnedEnemy(EnemyAI.Type type, int health, int damage, float speed,
                        CombatVec3 position, bool isBoss, int entityId)
    {
        Type = type;
        Health = health;
        Damage = damage;
        Speed = speed;
        Position = position;
        IsBoss = isBoss;
        EntityId = entityId;
    }
}

/// <summary>The immutable result of an OnZoneEnter: the denizens spawned into
/// a zone, adapted to the player's DNA profile.</summary>
public sealed class SpawnSet
{
    public string ZoneId { get; }
    public IReadOnlyList<SpawnedEnemy> Enemies { get; }
    public bool HasBoss { get; }
    /// <summary>1.0 = base table; grows with the profile's observed history.</summary>
    public float AdaptationLevel { get; }

    public SpawnSet(string zoneId, IReadOnlyList<SpawnedEnemy> enemies, float adaptationLevel)
    {
        ZoneId = zoneId;
        Enemies = enemies ?? Array.Empty<SpawnedEnemy>();
        AdaptationLevel = adaptationLevel;
        foreach (var e in Enemies) if (e.IsBoss) HasBoss = true;
    }

    public int Count => Enemies.Count;
}

/// <summary>
/// Ecosystem spawner (C15). On zone enter, builds a SpawnSet of enemies and a
/// possible boss adapted to the player's DNA CounterProfile. Pure logic.
/// </summary>
public class EcosystemSpawner
{
    /// <summary>Raised each OnZoneEnter; the Godot seam forwards it to the
    /// EventBus (C2 EcosystemAdapted) when the profile changed.</summary>
    public event Action<string, SpawnSet>? ZoneEntered;

    /// <summary>How many observed signatures are needed before the ecosystem
    /// is "adapted enough" to field a boss in a deep zone.</summary>
    public const int BossThreshold = 4;

    // Zone denizen tables: base enemy type, a fixed denizen count, and a boss
    // tier (0 = none). meadow is the shallowest, ruins the deepest. (Zone ids
    // match the M07 zone scenes: zones/{meadow,canyon,ruins}/{zone}.tscn.)
    private static readonly IReadOnlyDictionary<string, ZoneTable> Tables =
        new Dictionary<string, ZoneTable>
        {
            ["meadow"] = new ZoneTable(EnemyAI.Type.Goblin, 3, 0),
            ["canyon"] = new ZoneTable(EnemyAI.Type.Orc,    4, 1),
            ["ruins"]  = new ZoneTable(EnemyAI.Type.Skeleton, 5, 2),
        };

    /// <summary>The canonical zone id an unknown zone falls back to.</summary>
    public static readonly string DefaultZone = "meadow";

    /// <summary>Ordered zone ids (shallowest -> deepest) — the travel path the
    /// WorldDirector cycles through so canyon and ruins are reachable in play.</summary>
    public static readonly string[] ZoneIds = { "meadow", "canyon", "ruins" };

    private readonly System.Random _rng;
    private int _nextEntityId = 1000;

    public EcosystemSpawner(int? seed = null)
    {
        _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
    }

    /// <summary>
    /// C15 entry point. Build the SpawnSet for <paramref name="zoneId"/>,
    /// adapted to the player's <paramref name="profile"/>. Unknown zones fall
    /// back to the meadow (safest) table. Raises ZoneEntered with the result.
    /// </summary>
    public SpawnSet OnZoneEnter(string? zoneId, CounterProfile? profile)
    {
        string canonical = Tables.ContainsKey(zoneId ?? "") ? zoneId! : DefaultZone;
        var table = Tables[canonical];

        int observed = profile?.ObservedCount ?? 0;
        // Adaptation 1.0 at no history; +0.25 per observed signature, capped
        // at 2.5. A player who has never spoken DNA keeps the 1.0 base.
        float adaptation = 1.0f + Math.Min(1.5f, observed * 0.25f);

        // A zone always fields its table's denizen count; adaptation does not
        // add enemies, it makes them meaner (keeps spawns uniform per zone).
        int count = table.Denizens;

        var enemies = new List<SpawnedEnemy>(count + 1);
        for (int i = 0; i < count; i++)
        {
            var type = Escalate(table.BaseType, adaptation, i, count);
            enemies.Add(MakeEnemy(type, adaptation, table.Depth, i));
        }

        if (table.BossTier > 0 && observed >= BossThreshold)
        {
            enemies.Add(MakeBoss(table.BossTier, adaptation, table.Depth));
        }

        var set = new SpawnSet(canonical, enemies, adaptation);
        ZoneEntered?.Invoke(set.ZoneId, set);
        return set;
    }

    private SpawnedEnemy MakeEnemy(EnemyAI.Type type, float adaptation, int depth, int slot)
    {
        var baseStats = StatsOf(type);
        int entityId = _nextEntityId++;
        // HP and damage scale with adaptation; speed is unscaled (keeps the
        // chase/patrol cadence lively at every difficulty).
        return new SpawnedEnemy(
            type,
            health: (int)Math.Round(baseStats.Health * adaptation) + (depth * 5),
            damage: (int)Math.Round(baseStats.Damage * adaptation),
            speed: baseStats.Speed,
            position: SlotPosition(slot),
            isBoss: false,
            entityId: entityId);
    }

    private SpawnedEnemy MakeBoss(int tier, float adaptation, int depth)
    {
        // Boss base = the tier's table (1=Orc, 2=Skeleton); steep HP/damage
        // multipliers make it a genuine fight. Deep zone + high adaptation
        // compounds the multiplier.
        var baseStats = StatsOf(tier == 1 ? EnemyAI.Type.Orc : EnemyAI.Type.Skeleton);
        int entityId = _nextEntityId++;
        float deepBonus = 1f + (depth * 0.25f);
        return new SpawnedEnemy(
            EnemyAI.Type.Demon, // boss is always the apex type
            health: (int)Math.Round(baseStats.Health * 2.2f * adaptation * deepBonus),
            damage: (int)Math.Round(baseStats.Damage * 1.8f * adaptation * deepBonus),
            speed: baseStats.Speed * 0.85f,
            position: new CombatVec3(8f, 0f, 4f),
            isBoss: true,
            entityId: entityId);
    }

    /// <summary>Raise the slot's type one ladder step when the ecosystem is
    /// adapted. Only the deepest one-third of slots escalate, stepping further
    /// up as adaptation grows.</summary>
    private static EnemyAI.Type Escalate(EnemyAI.Type baseType, float adaptation, int slot, int count)
    {
        if (adaptation < 1.5f) return baseType;
        int deepStart = count - Math.Max(1, count / 3);
        if (slot < deepStart) return baseType;
        int steps = adaptation >= 2.0f ? 3 : 2;
        int step = Math.Min(steps, (slot - deepStart) + 1);
        return StepUp(baseType, step);
    }

    private static EnemyAI.Type StepUp(EnemyAI.Type t, int steps)
    {
        var ladder = new[] { EnemyAI.Type.Goblin, EnemyAI.Type.Orc, EnemyAI.Type.Skeleton, EnemyAI.Type.Demon };
        int idx = Array.IndexOf(ladder, t);
        if (idx < 0) return t;
        return ladder[Math.Min(ladder.Length - 1, idx + steps)];
    }

    private static (int Health, int Damage, float Speed) StatsOf(EnemyAI.Type t) => t switch
    {
        EnemyAI.Type.Goblin => (30, 5, 3.0f),
        EnemyAI.Type.Orc => (60, 15, 1.5f),
        EnemyAI.Type.Skeleton => (45, 10, 2.0f),
        _ => (100, 25, 1.0f), // Demon
    };

    private CombatVec3 SlotPosition(int slot)
    {
        // Ring of spawn points around the zone's entry so enemies are spread,
        // not stacked. Deterministic from the slot index.
        float angle = slot * 1.7f;
        return new CombatVec3(
            (float)Math.Cos(angle) * (2.0f + slot),
            0f,
            (float)Math.Sin(angle) * (2.0f + slot));
    }

    private sealed class ZoneTable
    {
        public EnemyAI.Type BaseType { get; }
        public int Denizens { get; }
        public int Depth { get; }
        public int BossTier { get; }
        public ZoneTable(EnemyAI.Type baseType, int denizens, int bossTier)
        {
            BaseType = baseType;
            Denizens = denizens;
            Depth = denizens;   // depth tracks the denizen count (shallowest->deepest)
            BossTier = bossTier;
        }
    }
}
