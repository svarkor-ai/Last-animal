using Godot;
using LastAnimal.Combat;
using LastAnimal.Companion;
using LastAnimal.Core;
using LastAnimal.Core.Framework;
using LastAnimal.Dna;
using LastAnimal.Ecosystem;
using LastAnimal.Npc;
using LastAnimal.Save;
using LastAnimal.Ui;
using System.Collections.Generic;

// Last Animal — W3-fix composition (MC 1123.10, artemis, 2026-09-08).
// T3b ownership refactor (MC 1256.9, artemis, 2026-09-21): THE composition root.
//
// WorldDirector: the scene's composition root + live-loop owner on main.tscn.
// Per the approved design (1256.2 §1.1/§2) this is the ONLY production site
// that constructs gameplay systems, and it binds every one of them into
// GameBootstrap so tests/proofs resolve the SAME instances (instance identity,
// not class identity). Single concern: own the authoritative runtime path —
//   - constructs ONE instance of each pure system (PlayerController,
//     CombatSystem, EcosystemSpawner, DnaLanguage, the companion stack),
//   - binds them into GameBootstrap (Bind<T>),
//   - spawns EnemyActor shells, each wrapping an EnemyAI the DIRECTOR
//     constructs and injects (EnemyActor no longer self-constructs),
//   - spawns CompanionFollowBody wrapping the machine-wired CompanionEntity,
//   - routes attacks through CombatSystem.DealDamage -> OnKill (the ONLY kill
//     path; OnKill's extraction feeds _spokenDna and the bus — no hardcoded
//     species string),
//   - applies the zone's SpawnSet on zone entry (spawns from it, not discards),
//   - owns the save/load actions (SaveSystem + GodotSaveStore),
//   - builds + wires the HUD / DialogueSystem / EmpathyPanel on the UI
//     CanvasLayer (Bind + ConnectBus) so the C2 bus drives the readouts.
//
// It owns NO gameplay rules of its own — it wires the existing pure modules
// into the scene (I1). The two Set*Enabled hooks are one-line gate seams named
// in the design (§4.2); they carry no gameplay behavior.
namespace LastAnimal.World;

[GlobalClass]
public partial class WorldDirector : Node3D
{
    /// <summary>The Player shell (movement handled by Player.cs).</summary>
    [Export] public Node3D? Player { get; set; }

    /// <summary>The CanvasLayer into which the HUD/panels are placed.</summary>
    [Export] public CanvasLayer? UICanvas { get; set; }

    // --- director-owned systems (the ONE instance of each per running game) ---
    private PlayerController _player = null!;
    private CombatSystem _combat = null!;
    private EcosystemSpawner _ecosystem = null!;
    private DnaLanguage _dna = null!;
    private CompanionComponent _companionCore = null!;
    private CompanionNeeds _needs = null!;
    private CompanionStateMachine _companion = null!;
    private SalarySystem _salary = null!;
    private BetrayalSystem _betrayal = null!;

    private readonly List<LanguageSignature> _spokenDna = new();
    private readonly List<EnemyActor> _enemies = new();
    private CompanionFollowBody? _companionBody;
    private int _companionLoyaltyLast;
    private string _zone = EcosystemSpawner.DefaultZone;

    // Gate seams (design §4.2): one-line bool guards, default true, no gameplay behavior.
    private bool _spawningEnabled = true;
    private bool _dnaForwarding = true;

    private EventBus _bus = null!;
    private GameBootstrap _bootstrap = null!;

    private Hud _hud = null!;
    private DialogueSystem _dialogue = null!;
    private EmpathyPanel _empathy = null!;

    // Enemy spawn ring around the player's start, so several stay in the
    // default camera frame.
    private static readonly (float X, float Z)[] SPAWNS =
    {
        ( 5f,  0f),
        (-4f,  4f),
        ( 0f, -5f),
    };

    // --- read-only surface the runtime proof reads (proof-only, no logic) ----
    public PlayerController PlayerModel => _player;
    public CombatSystem Combat => _combat;
    public IReadOnlyList<LanguageSignature> SpokenDna => _spokenDna;
    public CompanionStateMachine Companion => _companion;

    public override void _Ready()
    {
        // Resolve the composition references at runtime (robust on any load
        // path) rather than relying on .tscn exported NodePath binding.
        Player ??= GetNodeOrNull<Node3D>("Player");
        UICanvas ??= GetNodeOrNull<CanvasLayer>("UI");

        _bus = GetNode<EventBus>("/root/EventBus");
        _bootstrap = GetNode<GameBootstrap>("/root/GameBootstrap");

        // G-OWNERSHIP (design §4.5): a second composition root trying to
        // construct is a regression — fail loudly, never silently duplicate.
        if (_bootstrap.Has<PlayerController>())
        {
            GD.PushError("LA_GATE: DUPLICATE ROOT — PlayerController already bound; a second composition root is constructing systems");
        }

        // --- construct the ONE instance of each gameplay system --------------
        Vector3 origin = Player?.GlobalPosition ?? Vector3.Zero;
        _player = new PlayerController(new CombatVec3(origin.X, 0f, origin.Z));
        _combat = new CombatSystem();
        _ecosystem = new EcosystemSpawner(seed: 7);
        _dna = new DnaLanguage();
        _salary = new SalarySystem();
        _betrayal = new BetrayalSystem();
        _companionCore = new CompanionComponent { Id = 1 };
        _needs = new CompanionNeeds();
        _companionCore.SetCompanion(7);
        _companion = new CompanionStateMachine("companion", _companionCore, _needs, _salary, _betrayal);

        // --- bind into GameBootstrap: instance identity for tests/proofs -----
        _bootstrap.Bind(_player);
        _bootstrap.Bind(_combat);
        _bootstrap.Bind(_ecosystem);
        _bootstrap.Bind(_dna);
        _bootstrap.Bind(_companion);

        // --- the ONLY kill path: DealDamage -> OnKill -> DnaExtracted --------
        // The handler appends to _spokenDna (the ecosystem's input) and
        // forwards the REAL extraction to the bus (C10 -> M02 -> C2).
        _combat.DnaExtracted += OnDnaExtracted;
        _ecosystem.ZoneEntered += OnZoneEntered;

        BuildUi();
        SpawnEnemies();
        SpawnCompanion();
        EnterZone(_zone);
        _companionLoyaltyLast = _companionCore.Loyalty;
        GD.Print($"W3DBG: director ready (composition root) player={(Player != null)} uicanvas={(UICanvas != null)} enemies={_enemies.Count} origin={origin}");
    }

    public override void _Process(double delta)
    {
        if (Player == null) return;
        Vector3 ppos = Player.GlobalPosition;

        // Point each enemy's AI at the live player; collect incoming damage.
        int incoming = 0;
        foreach (var e in _enemies)
        {
            e.PlayerTargetX = ppos.X;
            e.PlayerTargetZ = ppos.Z;
            incoming += e.DamageDealt;
        }

        if (incoming > 0)
        {
            _player.TakeDamage(incoming);
            _hud.UpdateLife(_player.Health);
        }

        // Companion loop (M03 -> M05): tick needs + the machine the visible
        // entity is wired to; settlement policy as GameLoop's was.
        _needs.TickAccompaniment();
        var state = _companion.Tick();
        if (_needs.SalaryDue)
            _companion.Pay();
        else if (state == CompanionState.Needing)
            _companion.SkipPayment();

        int loy = _companionCore.Loyalty;
        if (loy != _companionLoyaltyLast)
        {
            _bus.EmitLoyaltyChanged(new CompanionId(_companion.Name), loy);
            _companionLoyaltyLast = loy;
        }
        if (state == CompanionState.Betrayed)
            _bus.EmitBetrayal(new CompanionId(_companion.Name), new TargetId("player"));

        // The visible companion body ticks the SAME machine (the integration fix).
        _companionBody?.Entity.Advance();

        if (Input.IsActionJustPressed("attack"))
            TryAttack();
    }

    private void BuildUi()
    {
        if (UICanvas == null) return;

        // Hud rides its own higher CanvasLayer layer (2) so the four gauge
        // readouts always draw clean on top, never overlapped by the dialogue
        // or empathy panels (which live on the base UI layer).
        var hudLayer = new CanvasLayer { Name = "HudLayer", Layer = 2 };
        _hud = new Hud { Name = "Hud" };
        _hud.Bind(100, 100, 0, 3);
        _hud.ConnectBus(_bus);
        hudLayer.AddChild(_hud);
        UICanvas.AddChild(hudLayer);

        // Dialogue + empathy panels: instanced (per the composition DoD) but
        // not force-opened on boot, so the full-width intro line does not
        // collide with the top-left HUD. They remain available to show later.
        _dialogue = new DialogueSystem { Name = "Dialogue" };
        UICanvas.AddChild(_dialogue);

        _empathy = new EmpathyPanel { Name = "Empathy" };
        _empathy.ConnectBus(_bus);
        UICanvas.AddChild(_empathy);
    }

    private void SpawnEnemies()
    {
        if (!_spawningEnabled) return;   // gate seam (design §4.2)

        Color[] colours =
        {
            new Color(0.9f, 0.25f, 0.2f),   // red goblin
            new Color(0.2f, 0.7f, 0.35f),   // green goblin
            new Color(0.55f, 0.45f, 0.9f),  // purple goblin
        };
        Vector3 origin = Player?.GlobalPosition ?? Vector3.Zero;
        for (int i = 0; i < SPAWNS.Length; i++)
        {
            // The DIRECTOR constructs the AI and injects it (design §2 row 2).
            var ai = new EnemyAI(
                new CombatVec3(origin.X + SPAWNS[i].X, 0f, origin.Z + SPAWNS[i].Z),
                2000 + i, EnemyAI.Type.Goblin, seed: 2000 + i);
            var actor = new EnemyActor { Name = $"Enemy{i}" };
            actor.Configure(ai, origin.X + SPAWNS[i].X, origin.Z + SPAWNS[i].Z, colours[i]);
            AddChild(actor);
            _enemies.Add(actor);
        }
    }

    /// <summary>Apply a SpawnSet: spawn an EnemyActor per entry (the design's
    /// "SpawnSet APPLIED — enemies spawned from it, not discarded").</summary>
    private void ApplySpawnSet(SpawnSet set)
    {
        if (!_spawningEnabled) return;
        Vector3 origin = Player?.GlobalPosition ?? Vector3.Zero;
        foreach (var spawned in set.Enemies)
        {
            var ai = new EnemyAI(
                new CombatVec3(origin.X + spawned.Position.X, 0f, origin.Z + spawned.Position.Z),
                spawned.EntityId, spawned.Type, seed: spawned.EntityId);
            var actor = new EnemyActor { Name = $"Spawned{spawned.EntityId}" };
            actor.Configure(ai, origin.X + spawned.Position.X, origin.Z + spawned.Position.Z,
                new Color(0.9f, 0.25f, 0.2f));
            AddChild(actor);
            _enemies.Add(actor);
        }
    }

    private void SpawnCompanion()
    {
        // The machine-wired companion: CompanionEntity (rig + animation driven
        // by the director's machine) inside the follow body — one companion,
        // not a visible body plus a phantom machine.
        var hook = new CompanionAnimationHook("walkBaked", "walkBaked", "walkBaked");
        _companionBody = new CompanionFollowBody(_companion, hook)
        {
            Name = "Companion",
            Target = Player,
            Y = 0.55f,
        };
        AddChild(_companionBody);
    }

    private void TryAttack()
    {
        if (Player == null) return;
        Vector3 ppos = Player.GlobalPosition;

        EnemyActor? best = null;
        float bestDist = _player.AttackRange;
        foreach (var e in _enemies)
        {
            if (e.IsDead) continue;
            float d = (e.GlobalPosition - ppos).Length();
            if (d <= bestDist) { best = e; bestDist = d; }
        }
        if (best == null) return;

        // The ONLY kill path (design §1.1): through the director-owned
        // CombatSystem. OnKill extracts the entity-id-seeded signature.
        _combat.DealDamage(attackerId: 0, best.Ai, _player.MeleeDamage);
        if (best.IsDead)
        {
            best.KillHide();
            GD.Print("W3: enemy killed via CombatSystem.DealDamage -> OnKill (HUD DNA meter +1)");
        }
    }

    // C10 -> M02 -> C2: an OnKill extraction appends to the spoken history and
    // forwards to the EventBus. This is the ONLY writer of _spokenDna.
    private void OnDnaExtracted(LanguageSignature signature)
    {
        _spokenDna.Add(signature);
        if (!_dnaForwarding) return;   // gate seam (design §4.2)
        _bus.EmitDnaExtracted(new DnaSignature(signature.SpeciesHash, signature.Id.ToString()));
    }

    // C15: (re)populate the current zone's SpawnSet when the player enters it.
    private void OnZoneEntered(string zoneId, SpawnSet set)
    {
        _zone = zoneId;
        ApplySpawnSet(set);
        GD.Print($"W3: zone '{zoneId}' SpawnSet applied (enemies={set.Count}, adaptation={set.AdaptationLevel:0.##})");
    }

    private void EnterZone(string zoneId)
    {
        var profile = EcosystemAdaptation.ModelPlayerDna(_spokenDna);
        _ecosystem.OnZoneEnter(zoneId, profile);
    }

    // ---- save/load (design §4.4): the director owns the actions ------------

    public void SaveGame()
    {
        var state = new GameState
        {
            ZoneId = _zone,
            Progression = 0,
            // LearnedDnaCounters: the per-position most-common nucleotide of the
            // spoken history (the C14 "persists DNA counters" snapshot).
            LearnedDnaCounters = BuildLearnedCounters(),
            CompanionEntityId = _companionCore.CompanionEntityId,
            CompanionLoyalty = _companionCore.Loyalty,
        };
        SaveSystem.Save(state, new GodotSaveStore());
    }

    /// <summary>Per-position most-common nucleotide across the spoken history
    /// (the C14 LearnedDnaCounters snapshot; empty when nothing was spoken).</summary>
    private List<int> BuildLearnedCounters()
    {
        var counters = new List<int>();
        if (_spokenDna.Count == 0) return counters;
        int len = 0;
        foreach (var s in _spokenDna) len = System.Math.Max(len, s.Nucleotides.Length);
        for (int i = 0; i < len; i++)
        {
            var tally = new int[4];
            foreach (var s in _spokenDna)
                if (i < s.Nucleotides.Length) tally[s.Nucleotides[i]]++;
            int best = 0;
            for (int n = 1; n < 4; n++) if (tally[n] > tally[best]) best = n;
            counters.Add(best);
        }
        return counters;
    }

    public void LoadGame()
    {
        var loaded = SaveSystem.Load(new GodotSaveStore());
        if (loaded == null) return;
        _zone = loaded.ZoneId;
        _companionCore.Loyalty = loaded.CompanionLoyalty;
        _companionLoyaltyLast = loaded.CompanionLoyalty;
        _hud.UpdateLife(_player.Health);
    }

    // ---- gate seams (design §4.2): one-line bool guards, no gameplay logic --

    /// <summary>Gate seam: disable enemy spawning (no_spawn negative control).</summary>
    public void SetSpawningEnabled(bool enabled) => _spawningEnabled = enabled;

    /// <summary>Gate seam: block the DnaExtracted bus forward (no_dna negative control).</summary>
    public void SetDnaForwarding(bool enabled) => _dnaForwarding = enabled;
}
