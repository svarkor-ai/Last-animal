using Godot;
using LastAnimal.Combat;
using LastAnimal.Companion;
using LastAnimal.Core.Audio;
using LastAnimal.Core.Framework;
using LastAnimal.Dna;
using LastAnimal.Ecosystem;
using LastAnimal.Npc;
using System.Collections.Generic;

// Last Animal — M01 bridge CARD 2 (MC 1123.3, artemis, 2026-09-07).
//
// GameLoop: the composition root that BOOTS modules and TICKS systems (CARD 2
// of BRIDGE-MVP.md STEP B). An autoload Node (I1: orchestration only — binding,
// boot order, per-frame ticking; NO gameplay logic of its own). This is the
// class that gives M02-M13 a live driver and makes GameBootstrap.Boot() actually
// boot them, closing root gap RG4 (before this, the ONLY IGameModule in the tree
// was the test probe OrderProbe in tests/BootTest.cs ).
//
// Two-layer design (BRIDGE-MVP STEP B: "bind wrapper objects adapt each pure
// module to IGameModule, OR GameLoop calls the pure Tick()s directly; smallest
// true root does the direct-call form and binds a single IGameModule adapter"):
//   - Each pure module is bound behind a tiny BootAdapter (an IGameModule) so
//     GameBootstrap.Boot() logs "GameBootstrap: booting module <Name> (order N)"
//     for each — the CARD 3/7 DoD expects exactly those markers, proving RG4.
//   - Per-frame ticking calls the pure Tick()/Pay()/OnZoneEnter() surfaces
//     directly (the direct-call form — no logic duplicated behind the seam).
namespace LastAnimal.Core;

public partial class GameLoop : Node
{
    private EventBus _bus = null!;
    private GameBootstrap _bootstrap = null!;

    // System instances held by the root and ticked per-frame (direct-call form).
    private readonly SalarySystem _salary = new();
    private readonly BetrayalSystem _betrayal = new();
    private readonly CompanionComponent _companionCore = new() { Id = 1 };
    private readonly CompanionNeeds _needs = new();
    private CompanionStateMachine _companion = null!;
    private readonly CombatSystem _combat = new();
    private readonly PlayerController _player = new(new CombatVec3(0, 0, 0));
    private readonly EnemyAI _enemy = new(new CombatVec3(3, 0, 3), 1000, EnemyAI.Type.Goblin, seed: 42);
    private readonly EcosystemSpawner _ecosystem = new(seed: 7);
    private readonly DnaLanguage _dna = new();

    private readonly List<LanguageSignature> _spokenDna = new();
    private string _zone = EcosystemSpawner.DefaultZone;
    private int _companionLoyaltyLast;

    public override void _Ready()
    {
        _bus = GetNode<EventBus>("/root/EventBus");
        _bootstrap = GetNode<GameBootstrap>("/root/GameBootstrap");

        // M05 companion machine observes the M03 CompanionCore (no loyalty math here).
        // Bond the companion so the M03 -> M05 follow/need loop has a live subject.
        _companionCore.SetCompanion(7);
        _companion = new CompanionStateMachine("companion", _companionCore, _needs, _salary, _betrayal);

        // --- M06: route SFX off the bus + start the music theme. ------------
        var music = GetNode<MusicManager>("/root/MusicManager");
        var sfx = GetNode<SfxRouter>("/root/SfxRouter");
        music.Boot();                                   // ensure buses + load SFX streams
        sfx.Subscribe(_bus, music);                     // route the 5 C2 signals to SFX
        var theme = GD.Load<AudioStreamOggVorbis>("res://assets/audio/music_theme.ogg");
        if (theme != null) music.PlayMusic(theme);      // cross-scene music loop

        // --- RG4: bind every pure module behind an IGameModule BootAdapter ---
        // so GameBootstrap.Boot() logs each boot marker in registration order.
        _bootstrap.Bind(new BootAdapter("Salary",        () => { }));
        _bootstrap.Bind(new BootAdapter("Betrayal",      () => { }));
        _bootstrap.Bind(new BootAdapter("Companion",     () => { }));
        _bootstrap.Bind(new BootAdapter("Combat",        () => { _combat.DnaExtracted += OnDnaExtracted; }));
        _bootstrap.Bind(new BootAdapter("Ecosystem",     () => EnterZone(_zone)));
        _bootstrap.Bind(new BootAdapter("DnaLanguage",   () => { }));
        _bootstrap.Bind<EventBus>(_bus);
        _bootstrap.Boot();

        _companionLoyaltyLast = _companion.Companion.Loyalty;
        GD.Print("GameLoop: ready — composition root booted (RG4 closed), ticks enabled");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // --- companion drift (M03 -> M05): tick needs + state machine. ------
        _needs.TickAccompaniment();
        var state = _companion.Tick();
        // settlement policy: pay when a wage is due and the player can afford it;
        // otherwise SkipPayment drains loyalty (the unpaid arm of the DoD path).
        if (_needs.SalaryDue)
            _companion.Pay();
        else if (state == CompanionState.Needing)
            _companion.SkipPayment();

        // emit loyalty / betrayal on thresholds (closes M03 -> M05 edge).
        int loy = _companion.Companion.Loyalty;
        if (loy != _companionLoyaltyLast)
        {
            _bus.EmitLoyaltyChanged(new CompanionId(_companion.Name), loy);
            _companionLoyaltyLast = loy;
        }
        if (state == CompanionState.Betrayed)
            _bus.EmitBetrayal(new CompanionId(_companion.Name), new TargetId("player"));

        // --- enemy AI tick (M08/C11) toward the player + combat damage. ------
        int dealt = _enemy.SetBehavior(_player.Position, dt);
        if (dealt > 0) _player.TakeDamage(dealt);

        // --- player engages enemies in range; attack map handled by M08 scene --.
        if (_enemy.Position.DistanceXZ(_player.Position) <= _player.AttackRange)
            _player.EngageEnemy(_enemy.EntityId);
    }

    // C10 -> M02 -> C2: an OnKill extraction forwards to the EventBus.
    private void OnDnaExtracted(LanguageSignature signature)
    {
        _spokenDna.Add(signature);
        _bus.EmitDnaExtracted(new DnaSignature(signature.SpeciesHash, signature.Id.ToString()));
    }

    // C15: (re)populate the current zone's SpawnSet when the player enters it.
    private void EnterZone(string zoneId)
    {
        _zone = zoneId;
        var profile = EcosystemAdaptation.ModelPlayerDna(_spokenDna);
        _ecosystem.OnZoneEnter(zoneId, profile);
        GD.Print($"GameLoop: entered zone '{zoneId}' (profile obs={profile.ObservedCount})");
    }

    /// <summary>
    /// Tiny IGameModule wrapper: adapts a pure module's boot callback to the
    /// framework seam so GameBootstrap.Boot() walks them all in order (RG4).
    /// The adapter carries NO gameplay logic — it only names + triggers the
    /// module's actual boot work via the delegate (I1).
    /// </summary>
    private sealed class BootAdapter : IGameModule
    {
        private readonly System.Action _boot;
        public string Name { get; }
        public BootAdapter(string name, System.Action boot) { Name = name; _boot = boot; }
        public void Boot() => _boot();
    }
}
