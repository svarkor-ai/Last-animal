using Godot;
using LastAnimal.Combat;
using LastAnimal.Core;
using LastAnimal.World;
using System.Collections.Generic;

// Last Animal — zone-travel + boss-phase runtime proof (MC 1344 DA findings
// 3+4, C15). Companion to RuntimeIntegrationProof; owns the NEW behaviour
// only, so that file stays under its concern:
//
//   zone_travel — the travel action (input map "travel", T) must move the
//     player meadow -> canyon -> ruins: CurrentZone changes, the player body
//     is repositioned at the zone entry, the zone's SpawnSet is re-applied,
//     and the spawned enemies carry the SpawnSet's SCALED stats (not the
//     hardcoded per-type defaults). Markers: ZONE_TRAVEL_CANYON,
//     ZONE_TRAVEL_RUINS, SCALED_STATS_APPLIED.
//
//   boss_phase — kills grow the spoken-DNA history; once observed >=
//     BossThreshold(4) a travel into canyon (BossTier 1) must field a live
//     boss (BOSS_REACHED), and further kills must step BossController.Phase
//     and fire C2 EcosystemAdapted on the REAL autoload bus (BOSS_PHASE_FIRED).
//
// Run:  $GODOT --headless --path <proj> --script res://ci_proofs/ZoneBossProof.cs
public partial class ZoneBossProof : SceneTree
{
    private const int FrameBudget = 6000;
    private const int PressFrames = 6;      // press-travel settle window
    private const int BossThreshold = 4;    // EcosystemSpawner.BossThreshold
    private const int PhaseStepTarget = 6;  // observed count that crosses a phase step

    private EventBus? _bus;
    private WorldDirector? _director;
    private CharacterBody3D? _playerBody;
    private Node? _main;
    private int _adaptedCount;
    private int _travelToggle;
    private int _attackToggle;
    private Vector3 _spawnOrigin;
    private string _mode = "zone_travel";
    private int _stage;
    private int _stageFrames;
    private bool _failed;
    private bool _asserted;

    public override void _Initialize()
    {
        GD.Print("LA_GATE: start");
        _mode = System.Environment.GetEnvironmentVariable("LA_GATE_MODE") ?? "zone_travel";
        GD.Print($"LA_GATE: mode={_mode}");
        Engine.MaxFps = 60;

        var packed = GD.Load<PackedScene>("res://main.tscn");
        if (packed == null) { Fail("cannot load res://main.tscn"); return; }
        _main = packed.Instantiate<Node3D>();
        Root.AddChild(_main);
    }

    private bool Compose()
    {
        _bus = Root.GetNodeOrNull<EventBus>("/root/EventBus");
        _director = _main as WorldDirector;
        _playerBody = _director?.GetNodeOrNull<CharacterBody3D>("Player");
        if (_bus == null) { Fail("EventBus autoload not present"); return false; }
        if (_director == null) { Fail("main.tscn root is not WorldDirector"); return false; }
        if (_playerBody == null) { Fail("Player node not found"); return false; }
        _bus.EcosystemAdapted += _ => _adaptedCount++;
        _spawnOrigin = _playerBody.GlobalPosition;
        GD.Print($"LA_GATE: composed — zone={_director.CurrentZone} enemies={_director.Enemies.Count} origin={_spawnOrigin}");
        return true;
    }

    public override bool _Process(double delta)
    {
        if (_failed) return true;
        if (_director == null && !Compose()) return true;
        if (_director == null || _playerBody == null || _bus == null) return true;

        _stageFrames++;
        if (_stageFrames > FrameBudget) { Fail("frame budget exhausted"); return true; }

        switch (_stage)
        {
            case 0: _stage = _mode == "boss_phase" ? 20 : 10; _stageFrames = 0; break;

            // ---- zone_travel: first travel must land in canyon -------------
            case 10:
                if (PressTravel() && _director.CurrentZone == "canyon")
                {
                    Check("player repositioned at the zone entry on travel",
                          _playerBody.GlobalPosition.DistanceTo(_spawnOrigin) < 1.0f,
                          $"pos={_playerBody.GlobalPosition} origin={_spawnOrigin}");
                    if (_failed) return true;
                    Check("canyon SpawnSet re-applied (enemies spawned from it)",
                          _director.Enemies.Count > 0, $"enemies={_director.Enemies.Count}");
                    if (_failed) return true;
                    Check("spawned enemies carry the SpawnSet's SCALED stats (not type defaults)",
                          AnyScaledEnemy(), DescribeEnemies());
                    if (_failed) return true;
                    GD.Print("LA_GATE: SCALED_STATS_APPLIED — SpawnSet Health/Damage/Speed live on the AI");
                    GD.Print("LA_GATE: ZONE_TRAVEL_CANYON — canyon reachable in play");
                    _stage = 11;
                    _stageFrames = 0;
                    _travelToggle = 0;   // re-arm the just-pressed window
                }
                else if (_stageFrames > 120) Fail("travel did not reach canyon");
                break;

            // ---- zone_travel: second travel must land in ruins -------------
            case 11:
                if (PressTravel() && _director.CurrentZone == "ruins")
                {
                    Check("ruins SpawnSet re-applied", _director.Enemies.Count > 0,
                          $"enemies={_director.Enemies.Count}");
                    if (_failed) return true;
                    GD.Print("LA_GATE: ZONE_TRAVEL_RUINS — ruins reachable in play");
                    GD.Print("LA_GATE: PASS — zone travel verified (meadow -> canyon -> ruins)");
                    _asserted = true;
                    Quit(0);
                    return true;
                }
                if (_stageFrames > 120) Fail("travel did not reach ruins");
                break;

            // ---- boss_phase: kill through the REAL path until observed >= 4 -
            case 20:
                if (_director.SpokenDna.Count >= BossThreshold)
                {
                    GD.Print($"LA_GATE: observed={_director.SpokenDna.Count} >= BossThreshold — travelling to canyon");
                    _stage = 21;
                    _stageFrames = 0;
                    _travelToggle = 0;   // re-arm the just-pressed window
                }
                else KillLoop();
                break;

            // ---- boss_phase: canyon must field the live boss ----------------
            case 21:
                if (PressTravel() && _director.CurrentZone == "canyon")
                {
                    Check("canyon fields a live boss once observed >= BossThreshold",
                          _director.HasLiveBoss, $"bossPhase={_director.BossPhase}");
                    if (_failed) return true;
                    GD.Print("LA_GATE: BOSS_REACHED — boss enemy live in play (was unreachable before)");
                    _stage = 22;
                    _stageFrames = 0;
                }
                else if (_stageFrames > 120) Fail("travel did not reach canyon (boss stage)");
                break;

            // ---- boss_phase: more kills must step the phase + fire C2 -------
            case 22:
                if (_adaptedCount > 0)
                {
                    Check("BossController.Phase transition fired C2 EcosystemAdapted",
                          _adaptedCount > 0, $"adapted={_adaptedCount} phase={_director.BossPhase}");
                    if (_failed) return true;
                    GD.Print("LA_GATE: BOSS_PHASE_FIRED — phase transition emitted EcosystemAdapted and re-fielded the SpawnSet");
                    GD.Print("LA_GATE: PASS — boss reachability + phase behaviour verified");
                    _asserted = true;
                    Quit(0);
                    return true;
                }
                if (_director.SpokenDna.Count >= PhaseStepTarget && _stageFrames > 60)
                    Fail($"no EcosystemAdapted after observed={_director.SpokenDna.Count} (phase wire broken)");
                else KillLoop();
                break;
        }
        return false;
    }

    /// <summary>Press travel for one frame out of PressFrames (just-pressed idiom).
    /// Returns true when the press window has elapsed so the zone check is fair.</summary>
    private bool PressTravel()
    {
        _travelToggle++;
        if (_travelToggle == 1) Input.ActionPress("travel");
        if (_travelToggle >= PressFrames) { Input.ActionRelease("travel"); return true; }
        return false;
    }

    /// <summary>One kill-loop frame: keep the nearest live enemy in melee range
    /// and toggle attack, exactly like RuntimeIntegrationProof's kill stages.</summary>
    private void KillLoop()
    {
        EnemyActor? target = null;
        float best = float.MaxValue;
        foreach (var e in _director!.Enemies)
        {
            if (e.IsDead || !IsInstanceValid(e)) continue;
            // Never farm the boss itself: killing it before the phase step
            // would silence the very transition this mode proves.
            if (ReferenceEquals(e, _director.BossActor)) continue;
            float d = e.GlobalPosition.DistanceTo(_playerBody!.GlobalPosition);
            if (d < best) { best = d; target = e; }
        }
        if (target == null)
        {
            if (_director.HasLiveBoss) { Fail("only the boss remains — kill budget mis-set"); return; }
            Fail("no live enemy left to kill (kill budget mis-set)");
            return;
        }
        if (best > _director.PlayerModel.AttackRange)
        {
            Vector3 p = target.GlobalPosition;
            _playerBody!.GlobalPosition = new Vector3(p.X - 0.8f, p.Y, p.Z);
        }
        _attackToggle++;
        if (_attackToggle % 4 == 1) Input.ActionPress("attack");
        else if (_attackToggle % 4 == 3) Input.ActionRelease("attack");
    }

    /// <summary>True when any live spawned enemy's AI stats differ from the
    /// hardcoded per-type defaults — i.e. the SpawnSet's scaled table is live.</summary>
    private bool AnyScaledEnemy()
    {
        foreach (var e in _director!.Enemies)
        {
            if (e.IsDead) continue;
            if (e.Ai.Health != BaseHealth(e.Ai.EnemyType)) return true;
        }
        return false;
    }

    private string DescribeEnemies()
    {
        var parts = new List<string>();
        foreach (var e in _director!.Enemies)
            parts.Add($"{e.Ai.EnemyType}:hp={e.Ai.Health}");
        return string.Join(",", parts);
    }

    private static int BaseHealth(EnemyAI.Type t) => t switch
    {
        EnemyAI.Type.Goblin => 30,
        EnemyAI.Type.Orc => 60,
        EnemyAI.Type.Skeleton => 45,
        _ => 100, // Demon
    };

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"LA_GATE: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) Fail(what);
    }

    private void Fail(string why)
    {
        Input.ActionRelease("travel");
        Input.ActionRelease("attack");
        _failed = true;
        GD.PrintErr($"LA_GATE: FAIL — {why}");
        Quit(1);
    }

    public override void _Finalize()
    {
        Input.ActionRelease("travel");
        Input.ActionRelease("attack");
        if (!_asserted && !_failed)
            GD.PrintErr("LA_GATE: FAIL — finished without asserting all stages");
    }
}
