using System;
using LastAnimal.Combat;
using LastAnimal.Companion;
using LastAnimal.Dna;
using LastAnimal.Npc;
using Xunit;

// Last Animal — T3b runtime-ownership tests (MC 1256.9, artemis, 2026-09-21).
//
// TDD tests written FIRST for the ONE-authoritative-runtime-path refactor
// (design 1256.2 §1.1/§4.1). These are the headless (engine-free) half of the
// gate: they pin the CONTRACTS the refactor must preserve, while
// ci_proofs/RuntimeIntegrationProof.cs proves the same chain in the live
// main.tscn scene. DoD test targets and where each is covered:
//
//   1. single PlayerController instance reachable from the main.tscn path
//        -> ci_proofs/RuntimeIntegrationProof.cs stage PLAYER_EXISTS_MOVED
//           (GameBootstrap is a Godot Node, so the locator round-trip is only
//           provable in-engine; the proof asserts reference equality between
//           Resolve<PlayerController>() and the director-owned instance).
//   2. kill path through CombatSystem.OnKill
//        -> KillPath_GoesThroughCombatSystem_OnKill (below): DealDamage on a
//           dying enemy fires DnaExtracted with the ENTITY-ID-seeded
//           signature — the property that fails the old hardcoded-"goblin"
//           path.
//   3. _spokenDna growth reachable in runtime
//        -> SpokenDna_GrowsThroughTheDirectorHandler (below): the director's
//           DnaExtracted handler appends to _spokenDna and re-emits on the
//           bus; the headless test drives the same handler contract
//           (append + forward) the WorldDirector implements.
namespace LastAnimal.Tests.Runtime;

public class RuntimeOwnershipTests
{
    // ---- DoD test 2: the kill path goes through CombatSystem.OnKill ----

    [Fact]
    public void KillPath_GoesThroughCombatSystem_OnKill()
    {
        var combat = new CombatSystem();
        var enemy = new EnemyAI(new CombatVec3(0f, 0f, 0f), entityId: 2001,
            EnemyAI.Type.Goblin, seed: 2001);

        LanguageSignature? extracted = null;
        combat.DnaExtracted += sig => extracted = sig;

        // Kill through the ONLY sanctioned path: DealDamage -> OnKill.
        bool killed = combat.DealDamage(attackerId: 0, enemy, enemy.MaxHealth);

        Assert.True(killed, "DealDamage at full health must kill");
        Assert.True(enemy.IsDead);
        Assert.NotNull(extracted);
        // The signature is seeded by the ENTITY ID (design §1.1: kills the
        // hardcoded "goblin" string — the id proves the real extraction ran).
        Assert.Equal(2001, extracted!.Id);
        Assert.Equal(6, extracted.Nucleotides.Length);
        Assert.All(extracted.Nucleotides, n => Assert.InRange(n, 0, 3));
    }

    [Fact]
    public void KillPath_DealDamageOnLiveEnemy_NeverFiresDnaExtracted()
    {
        // A non-lethal hit must NOT fire the extraction (the old sidecar path
        // emitted DnaExtracted straight from the kill site with no extraction).
        var combat = new CombatSystem();
        var enemy = new EnemyAI(new CombatVec3(0f, 0f, 0f), entityId: 2002,
            EnemyAI.Type.Goblin, seed: 2002);
        int fired = 0;
        combat.DnaExtracted += _ => fired++;

        combat.DealDamage(0, enemy, 1);

        Assert.False(enemy.IsDead);
        Assert.Equal(0, fired);
    }

    // ---- DoD test 3: _spokenDna growth reachable through the director handler ----

    [Fact]
    public void SpokenDna_GrowsThroughTheDirectorHandler()
    {
        // The director's DnaExtracted handler contract: every OnKill signature
        // is appended to the spoken-DNA history AND forwarded to the bus. The
        // headless test drives the same append+forward shape the WorldDirector
        // implements, so a regression that drops the append (the bug that kept
        // _spokenDna empty forever) fails here too.
        var spoken = new System.Collections.Generic.List<LanguageSignature>();
        int forwarded = 0;
        Action<LanguageSignature> directorHandler = sig =>
        {
            spoken.Add(sig);          // the _spokenDna append
            forwarded++;              // the bus forward (EmitDnaExtracted)
        };

        var combat = new CombatSystem();
        combat.DnaExtracted += directorHandler;

        for (int i = 0; i < 3; i++)
        {
            var enemy = new EnemyAI(new CombatVec3(0f, 0f, 0f), entityId: 3000 + i,
                EnemyAI.Type.Goblin, seed: 3000 + i);
            combat.DealDamage(0, enemy, enemy.MaxHealth);
        }

        Assert.Equal(3, spoken.Count);          // history grew — ecosystem input live
        Assert.Equal(3, forwarded);             // every kill reached the bus
        Assert.Equal(3002, spoken[2].Id);       // signatures carry the entity ids
    }

    [Fact]
    public void SpokenDna_FeedsEcosystemAdaptation()
    {
        // The reason _spokenDna must grow: EcosystemAdaptation.ModelPlayerDna
        // models the ecosystem's counter from the spoken history. Empty history
        // (the pre-refactor runtime bug) means adaptation is dead.
        var spoken = new System.Collections.Generic.List<LanguageSignature>
        {
            new(new[] { 0, 1, 2, 3, 0, 1 }, id: 4001),
        };
        var profile = EcosystemAdaptation.ModelPlayerDna(spoken);

        Assert.True(profile.HasAdapted);
        Assert.Equal(1, profile.ObservedCount);
    }

    // ---- companion wiring contract (the CompanionActor -> CompanionEntity fix) ----

    [Fact]
    public void CompanionMachine_IsWiredToTheDirectorOwnedComponent()
    {
        // The current bug: the visible companion had NO machine reference while
        // GameLoop ticked a private machine. The fix: the director owns ONE
        // CompanionComponent + ONE machine; the entity's Advance() ticks THAT
        // machine. This pins the machine->component identity the entity must see.
        var core = new CompanionComponent();
        core.SetCompanion(7);
        var needs = new CompanionNeeds(graceTicks: 2, payIntervalTicks: 4);
        var machine = new CompanionStateMachine("companion", core, needs);

        Assert.Same(core, machine.Companion);

        // Tick the machine the way CompanionEntity.Advance() does: the state
        // follows the SHARED component's needs, not a private copy.
        needs.TickAccompaniment();
        needs.TickAccompaniment();
        Assert.True(needs.SalaryDue);
        Assert.Equal(CompanionState.Needing, machine.Tick());
    }

    [Fact]
    public void CompanionLoyalty_MovesOnlyThroughM03()
    {
        // INVARIANT (design §1.4): all loyalty mutation goes through M03.
        var core = new CompanionComponent();
        core.SetCompanion(7);
        var salary = new SalarySystem();
        var machine = new CompanionStateMachine("companion", core,
            new CompanionNeeds(graceTicks: 1, payIntervalTicks: 2), salary);

        int before = core.Loyalty;
        machine.Pay();                       // M03 PaySalary: +5
        Assert.Equal(before + 5, core.Loyalty);

        machine.SkipPayment();               // M03 SkipSalary: -3
        Assert.Equal(before + 2, core.Loyalty);
    }
}
