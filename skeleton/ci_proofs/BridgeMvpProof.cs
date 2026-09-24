using Godot;
using LastAnimal.Combat;
using LastAnimal.Core;
using LastAnimal.Core.Framework;
using LastAnimal.Empathy;
using LastAnimal.Npc;
using LastAnimal.Save;
using LastAnimal.Ui;
using LastAnimal.World;
using System.Collections.Generic;
using System.Linq;
using System;

// Last Animal — W7 bridge_mvp_proof (MC 1123.14; redo of MC 1123.8, artifact never landed).
//
// Proves the FULL playable MVP loop (BRIDGE-MVP.md §3) with all six markers,
// printing one marker per closed loop and exiting non-zero on any missing one:
//   1 PLAYER_MOVED      — simulated WASD drives Player -> CharacterBody3D (C10/RG3).
//   2 DNA_EXTRACTED     — a kill fires OnKill -> EventBus.DnaExtracted (C10+C2+M02)
//                         and Hud.DnaMeter increments (C13).
//   3 HUD_BOUND         — Hud is bound to the bus; Life/Hearts move on events (C13).
//   4 BOOK_OPENED       — EmpathyPanel.Open(BookEntry) surfaces a real M04 entry (C9+C13).
//   5 COMPANION_FOLLOWS — CompanionActor closes distance to the Player over frames (M05).
//   6 SAVE_ROUNDTRIP    — SaveSystem.Save(GameState, GodotSaveStore) -> Load round-trips (C14).
//
// Marker 2 drives the REAL playable path first (Input.ActionPress("attack") with the
// player teleported into range, so WorldDirector.TryAttack fires on IsActionJustPressed);
// if the input path has not fired within its frame budget, the proof falls back to the
// combat seam directly (CombatSystem.DealDamage -> OnKill -> bus), which §3 explicitly
// allows. The log names which path fired — markers are never faked.
//
// Marker 6 exercises the pure save seam (SaveSystem + GodotSaveStore -> user://) —
// save/load is NOT wired into the playable scene (W6 wired pure logic only); this card
// adds no scene wiring.
//
// After PASS the proof HOLDS the live scene (1200 physics ticks = ~20s, only
// reached with a display) so graphical-test-helper can capture a non-blank frame
// of the real scene at --wait 15 (8-9s catches only the engine splash). Headless
// frames run uncapped, so the hold is near-instant there.
//
// Run:  $GODOT --headless --path <proj> --script res://ci_proofs/BridgeMvpProof.cs
public partial class BridgeMvpProof : SceneTree
{
    private const int MovePhysicsFrames = 30;  // WASD accumulation in PHYSICS ticks (0.5s @ 60Hz) before the move assert (headless uncapped frames are tiny; 12 gave dx=0.031 < 0.05, MC 1256.14)
    private const int AttackBudgetFrames = 60; // input-path kill budget before seam fallback
    private const int FollowFrames = 120;      // frames the companion gets to close distance (headless uncapped frames are tiny; 40 gave moved<0.2, MC 1256.14)
    private const int HoldFrames = 1200;       // post-PASS hold: 1200 physics ticks @60Hz = 20s
                                               // > the helper's 15s capture window (MC 1344.2)
    private const int FrameBudget = 2400;      // hard overall budget (raised: MoveFrames 60 + FollowFrames 120 + attack budget need headroom, MC 1256.14)

    private EventBus? _bus;
    private Node3D? _main;
    private CharacterBody3D? _player;
    private Hud? _hud;
    private EmpathyPanel? _empathy;
    private CompanionFollowBody? _companion;
    private readonly List<EnemyActor> _enemies = new();

    private int _frames;
    private int _stage;
    private int _stageFrames;
    private bool _failed;
    private bool _asserted;
    private bool _killedViaSeam;
    private bool _composed;
    private int _dnaCount;
    private int _bookOpenedCount;
    private int _dnaBefore;
    private Vector3 _playerStart;
    private Vector3 _companionStart;
    private float _followDist0;
    private int _attackToggle;
    private int _physFrames;    // physics ticks elapsed since compose (movement is physics-driven)
    private int _pressPhysFrame; // physics tick at which move_right was pressed (MC 1344.1)
    private int _holdStartPhys;  // physics tick when the render-bar hold began (MC 1344.2)
    // (loyalty, hearts) pairs recorded from every LoyaltyChanged event (MC 1344.1 marker-3).
    private readonly List<(int loy, int hearts)> _loyaltyPairs = new();
    private int _loyaltyPending; // loyalty of the emit currently in flight (MC 1344.1)

    public override bool _PhysicsProcess(double delta)
    {
        _physFrames++;
        return false; // false = keep the main loop running (true would quit the SceneTree)
    }

    public override void _Initialize()
    {
        GD.Print("BRIDGE_MVP_PROOF: start");

        // Harness fix (MC 1344.1): headless --script process frames are uncapped,
        // so 60 process frames span <1/60s of engine time and only ~0-2 physics
        // ticks fire — the player barely moves. Cap the FPS so process-frame
        // deltas match a real 60 FPS run and physics ticks accumulate normally.
        Engine.MaxFps = 60;

        // The C2 bus: the real autoload when the engine loaded it, else a local one.
        // (SceneTree has no GetNodeOrNull — use Root.GetNodeOrNull, MC 1256.5 build fix.)
        // MC 1344.1: in --script mode the autoload EventBus loads AFTER _Initialize,
        // so a local bus created here COLLIDES with the autoload at /root/EventBus and
        // the HUD ends up wired to the autoload while the proof emits on the local
        // bus — the loyalty emit never reaches the HUD. Do NOT create a local bus:
        // resolve the autoload lazily in _Compose (after the scene _Ready ran) and
        // subscribe there.
        _bus = Root.GetNodeOrNull<EventBus>("/root/EventBus");
        if (_bus != null)
        {
            _bus.DnaExtracted += _ => _dnaCount++;
            _bus.EmpathyBookOpened += () => _bookOpenedCount++;
            _bus.LoyaltyChanged += (_, loy) => _loyaltyPending = (int)loy;
        }

        // Instance the composition root (a --script run does not load main_scene).
        // NOTE: the HUD/Empathy/Companion lookups are DEFERRED to the first
        // _Process frame (see _Compose) — WorldDirector.BuildUi() runs in
        // _Ready(), which has not executed yet during _Initialize(), and
        // get_node with absolute paths is illegal from outside the active
        // tree here (MC 1256.14).
        var packed = GD.Load<PackedScene>("res://main.tscn");
        if (packed == null) { Fail("cannot load res://main.tscn"); return; }
        Node3D main = packed.Instantiate<Node3D>();
        Root.AddChild(main);
        _main = main;
    }

    private void _Compose()
    {
        // First _Process frame: main.tscn is inside the tree and WorldDirector
        //._Ready() (incl. BuildUi) has run — the lookups are now valid.
        Node3D main = _main!;
        _player = main.GetNodeOrNull<CharacterBody3D>("Player");
        _hud = main.GetNodeOrNull<Hud>("UI/HudLayer/Hud");
        _empathy = main.GetNodeOrNull<EmpathyPanel>("UI/Empathy");
        _companion = main.GetNodeOrNull<CompanionFollowBody>("Companion");
        for (int i = 0; i < 8; i++)
        {
            var e = main.GetNodeOrNull<EnemyActor>($"Enemy{i}");
            if (e != null) _enemies.Add(e);
        }

        if (_player == null) { Fail("Player node not found in main.tscn"); return; }
        if (_hud == null) { Fail("Hud not found at UI/HudLayer/Hud (WorldDirector UI not built)"); return; }
        if (_empathy == null) { Fail("EmpathyPanel not found at UI/Empathy"); return; }
        if (_companion == null) { Fail("CompanionFollowBody not found at Main/Companion"); return; }
        if (_enemies.Count == 0) { Fail("WorldDirector spawned no EnemyActor"); return; }

        // MC 1344.1: the autoload EventBus may only exist by now (it loads after
        // _Initialize in --script mode). Resolve it here if _Initialize missed it,
        // and subscribe the counters on the SAME bus the game's nodes use.
        if (_bus == null)
        {
            _bus = Root.GetNodeOrNull<EventBus>("/root/EventBus");
            if (_bus == null) { Fail("EventBus autoload not present even after scene _Ready"); return; }
            _bus.DnaExtracted += _ => _dnaCount++;
            _bus.EmpathyBookOpened += () => _bookOpenedCount++;
            _bus.LoyaltyChanged += (_, loy) => _loyaltyPending = (int)loy;
        }

        _dnaBefore = _hud.DnaMeter;

        // Capture the movement baseline BEFORE pressing the input (MC 1344.1 root
        // cause): the previous code captured it in stage 0 — the _Process frame
        // AFTER the press — by which time the player had already walked ~0.5 units
        // and Enemy0 (spawned 2 units ahead, chasing at 3 u/s) had closed to contact
        // range and body-blocked further motion, so the late baseline measured dx=0.
        _playerStart = _player.GlobalPosition;
        _pressPhysFrame = _physFrames;

        // Press walk-right BEFORE any remaining early return: from here on every
        // fail path releases the inputs, so no loop runs them unpressed.
        Input.ActionPress("move_right");
        GD.Print($"BRIDGE_MVP_PROOF: composed — Player + {_enemies.Count} enemies + Hud + EmpathyPanel + Companion (dnaBefore={_dnaBefore})");
        _composed = true;
    }

    public override bool _Process(double delta)
    {
        if (_failed) return true;
        if (!_composed)
        {
            // First frame: the scene is in the tree and _Ready() has run —
            // resolve the HUD/Empathy/Companion references now (MC 1256.14).
            if (_main == null) return true;
            _Compose();
            return false;
        }
        if (_player == null || _hud == null || _empathy == null || _companion == null || _bus == null)
            return true;

        _frames++;
        _stageFrames++;
        if (_frames > FrameBudget) { Fail("frame budget exhausted before all six markers"); return true; }

        switch (_stage)
        {
            case 0:
                // Baseline was captured in _Compose BEFORE the press (MC 1344.1) —
                // go straight to the movement stage.
                _stage = 1;
                _stageFrames = 0;
                break;

            case 1:
                // Movement is applied in _PhysicsProcess ticks, not process frames —
                // count physics ticks SINCE THE PRESS so the assert measures real
                // engine time from the moment input went down (MC 1344.1).
                if (_physFrames >= _pressPhysFrame + MovePhysicsFrames)
                {
                    Vector3 now = _player.GlobalPosition;
                    float dx = now.X - _playerStart.X;
                    float dz = now.Z - _playerStart.Z;
                    if (!(dx > 0.05f || dz > 0.05f))
                    {
                        Fail($"player did not move on simulated WASD (dx={dx:0.###}, dz={dz:0.###}, physFrames={_physFrames})");
                        return true;
                    }
                    Input.ActionRelease("move_right");
                    GD.Print("BRIDGE_MVP_PROOF: MARKER 1/6 PLAYER_MOVED — simulated WASD -> PlayerController -> CharacterBody3D");
                    TeleportIntoRange();
                    _stage = 2;
                    _stageFrames = 0;
                }
                break;

            case 2:
                if (_dnaCount > 0)
                {
                    Input.ActionRelease("attack");
                    Check("Hud.DnaMeter incremented by the kill's DnaExtracted",
                          _hud.DnaMeter > _dnaBefore, $"dna={_hud.DnaMeter} (before {_dnaBefore})");
                    if (_failed) return true;
                    string via = _killedViaSeam ? "CombatSystem seam" : "WorldDirector attack input";
                    GD.Print($"BRIDGE_MVP_PROOF: MARKER 2/6 DNA_EXTRACTED — kill via {via}: OnKill -> EventBus.DnaExtracted -> Hud.DnaMeter");
                    _stage = 3;
                    _stageFrames = 0;
                }
                else if (_stageFrames > AttackBudgetFrames)
                {
                    // §3 allows driving the combat seam directly; name the path in the log.
                    Input.ActionRelease("attack");
                    EnemyActor? target = FirstLiveEnemy();
                    if (target == null) { Fail("no live enemy left to kill"); return true; }
                    var cs = new CombatSystem();
                    cs.DnaExtracted += sig => _bus.EmitDnaExtracted(new DnaSignature(sig.SpeciesHash, sig.Id.ToString()));
                    cs.DealDamage(attackerId: 0, target.Ai, 999);
                    if (!target.IsDead) { Fail("seam DealDamage did not kill the enemy"); return true; }
                    target.KillHide();
                    _killedViaSeam = true;
                    GD.Print("BRIDGE_MVP_PROOF: input path did not fire — kill driven via the CombatSystem seam (DealDamage -> OnKill -> bus)");
                }
                else
                {
                    // Press attack 2 frames, release 2, repeat: WorldDirector.TryAttack
                    // fires on IsActionJustPressed with the player in range.
                    _attackToggle++;
                    if (_attackToggle % 4 == 1) Input.ActionPress("attack");
                    else if (_attackToggle % 4 == 3) Input.ActionRelease("attack");
                }
                break;

            case 3:
                {
                    // HUD_BOUND: the bus moves the bus-driven gauges; the combat seam
                    // moves Life. Synchronous checks right after each emit.
                    // MC 1344.1: the game's own companion tick ALSO emits LoyaltyChanged
                    // (fresh core loyalty 100 -> 5 hearts) and can land after the proof's
                    // emit, overwriting the HUD. Assert the MAPPING on every event seen
                    // instead of the final HUD value: subscribe in _Compose, record
                    // (loyalty, hearts) pairs, and require hearts == round(loy/20) for
                    // each pair — plus the proof's own 80 -> 4 pair present.
                    _bus.EmitLoyaltyChanged(new CompanionId("proof-companion"), 80);
                    // Godot signals are synchronous: when EmitLoyaltyChanged returns,
                    // the HUD handler has already run — record the pair now.
                    if (_loyaltyPending >= 0)
                        _loyaltyPairs.Add((_loyaltyPending, _hud!.CompanionHearts));
                    _loyaltyPending = -1;
                    bool sawProofPair = _loyaltyPairs.Any(p => p.loy == 80 && p.hearts == 4);
                    bool mappingHolds = _loyaltyPairs.All(p =>
                        p.hearts == Math.Clamp((int)Math.Round(p.loy / 20.0), 0, 5));
                    Check("Hud.CompanionHearts follows LoyaltyChanged (mapping holds for every event; 80 -> 4 seen)",
                          sawProofPair && mappingHolds,
                          $"pairs={string.Join(";", _loyaltyPairs.Select(p => $"{p.loy}->{p.hearts}"))}");
                    if (_failed) return true;
                    _hud.UpdateLife(77);
                    Check("Hud.Life follows the combat seam (UpdateLife)",
                          _hud.Life == 77, $"life={_hud.Life}");
                    if (_failed) return true;
                    Check("Hud is bus-wired (DnaMeter moved on DnaExtracted)",
                          _hud.DnaMeter > _dnaBefore, $"dna={_hud.DnaMeter}");
                    if (_failed) return true;
                    GD.Print("BRIDGE_MVP_PROOF: MARKER 3/6 HUD_BOUND — Hud bound to the bus; Life/DnaMeter/Hearts update on events");

                    // BOOK_OPENED: surface a REAL M04 BookEntry via EmpathyBook.Query.
                    var comp = new CompanionComponent { Id = 7, CompanionEntityId = 2, Loyalty = 55 };
                    BookEntry? entry = EmpathyBook.Query(comp);
                    Check("EmpathyBook.Query returns a real entry", entry != null, $"entry={entry}");
                    if (_failed || entry == null) return true;
                    _empathy.Open(entry);
                    Check("EmpathyPanel.Open surfaces the entry",
                          _empathy.Current == entry && _empathy.Visible,
                          $"current={_empathy.Current == entry} visible={_empathy.Visible}");
                    if (_failed) return true;
                    Check("panel shows the M04 hidden state",
                          _empathy.Current!.EmotionalState == "Neutral",
                          $"state={_empathy.Current.EmotionalState}");
                    if (_failed) return true;
                    Check("EmpathyBookOpened fired exactly once on open", _bookOpenedCount == 1,
                          $"count={_bookOpenedCount}");
                    if (_failed) return true;
                    GD.Print("BRIDGE_MVP_PROOF: MARKER 4/6 BOOK_OPENED — EmpathyPanel.Open(BookEntry) surfaced the M04 entry + fired C2");

                    // COMPANION_FOLLOWS baseline: companion -> player distance now.
                    _companionStart = _companion.GlobalPosition;
                    _followDist0 = _companion.GlobalPosition.DistanceTo(_player.GlobalPosition);
                    _stage = 4;
                    _stageFrames = 0;
                }
                break;

            case 4:
                if (_stageFrames >= FollowFrames)
                {
                    float d1 = _companion.GlobalPosition.DistanceTo(_player.GlobalPosition);
                    float moved = _companionStart.DistanceTo(_companion.GlobalPosition);
                    if (moved < 0.2f || d1 > _followDist0 - 0.25f)
                    {
                        Fail($"companion did not follow (moved={moved:0.###}, dist {_followDist0:0.###} -> {d1:0.###})");
                        return true;
                    }
                    GD.Print($"BRIDGE_MVP_PROOF: MARKER 5/6 COMPANION_FOLLOWS — companion closed on the player (dist {_followDist0:0.###} -> {d1:0.###}, moved {moved:0.###})");
                    _stage = 5;
                    _stageFrames = 0;
                }
                break;

            case 5:
                {
                    // SAVE_ROUNDTRIP: pure save seam (save/load is NOT wired into the
                    // playable scene — W6 wired pure logic only; no scene wiring here).
                    var store = new GodotSaveStore();
                    var state = GameState.Representative();
                    state.ZoneId = "bridge-mvp-proof";
                    Check("SaveSystem.Save(GameState, GodotSaveStore) succeeded",
                          SaveSystem.Save(state, store), $"path={store.SavePath}");
                    if (_failed) return true;
                    GameState? loaded = SaveSystem.Load(store);
                    Check("Load returned the saved state", loaded != null, $"loaded={loaded}");
                    if (_failed || loaded == null) return true;
                    Check("round-trip preserved zone/progression/loyalty/emotion/dna",
                          loaded.ZoneId == state.ZoneId
                          && loaded.Progression == state.Progression
                          && loaded.CompanionLoyalty == state.CompanionLoyalty
                          && loaded.EmotionState == state.EmotionState
                          && loaded.LearnedDnaCounters.Count == state.LearnedDnaCounters.Count,
                          $"zone={loaded.ZoneId} prog={loaded.Progression} loy={loaded.CompanionLoyalty} emo={loaded.EmotionState} dna={loaded.LearnedDnaCounters.Count}");
                    if (_failed) return true;
                    GD.Print("BRIDGE_MVP_PROOF: MARKER 6/6 SAVE_ROUNDTRIP — SaveSystem.Save -> Load round-trip via GodotSaveStore (user://)");
                    GD.Print("BRIDGE_MVP_PROOF: PASS — all six playable-MVP markers asserted (BRIDGE-MVP.md §3)");
                    _asserted = true;
                    _stage = 6;
                    _stageFrames = 0;
                    _holdStartPhys = _physFrames;
                }
                break;

            case 6:
                // Hold the live scene so graphical-test-helper (--wait 15) captures a
                // real rendered frame, not the splash. Physics-tick gated like
                // RuntimeIntegrationProof: process frames raced the 15s grab — the
                // proof quit first and the root window read uniform black (MC 1344.2).
                if (_physFrames >= _holdStartPhys + HoldFrames) { Quit(0); return true; }
                break;
        }
        return false;
    }

    public override void _Finalize()
    {
        Input.ActionRelease("move_right");   // unconditional: never leak a pressed action
        Input.ActionRelease("attack");
        if (!_asserted && !_failed)
            GD.PrintErr("BRIDGE_MVP_PROOF: FAIL — finished without asserting all six markers");
    }

    private EnemyActor? FirstLiveEnemy()
    {
        foreach (var e in _enemies)
            if (!e.IsDead) return e;
        return null;
    }

    private void TeleportIntoRange()
    {
        EnemyActor? target = FirstLiveEnemy();
        if (target == null) { Fail("no live enemy to teleport next to"); return; }
        // Put the player just inside melee range (AttackRange 1.5) of the enemy, at
        // the enemy's height so the 3D distance check is dominated by the XZ offset.
        Vector3 e = target.GlobalPosition;
        _player!.GlobalPosition = new Vector3(e.X - 0.8f, e.Y, e.Z);
        GD.Print($"BRIDGE_MVP_PROOF: player teleported into attack range of {target.Name} at ({e.X:0.##},{e.Y:0.##},{e.Z:0.##})");
    }

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"BRIDGE_MVP_PROOF: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) Fail(what);
    }

    private void Fail(string why)
    {
        Input.ActionRelease("move_right");
        Input.ActionRelease("attack");
        _failed = true;
        GD.PrintErr($"BRIDGE_MVP_PROOF: FAIL — {why}");
        Quit(1);
    }
}
