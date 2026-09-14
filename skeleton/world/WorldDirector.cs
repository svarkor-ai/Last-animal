using Godot;
using LastAnimal.Combat;
using LastAnimal.Core;
using LastAnimal.Core.Framework;
using LastAnimal.Ui;
using System.Collections.Generic;

// Last Animal — W3-fix composition (MC 1123.10, artemis, 2026-09-08).
//
// WorldDirector: the scene's population + live-loop glue on the composition
// root. Single concern: make main.tscn a VISIBLE, PLAYABLE scene —
//   - spawns the visible enemies (EnemyActor, each wrapped around a pure M08
//     EnemyAI) at fixed spots on the terrain around the player,
//   - ticks those actors' AI targets at the live player, applies their damage
//     to a pure PlayerController sidecar, and reflects it on the HUD,
//   - lets the player attack (Space) the nearest in-range enemy; a kill hides
//     the actor and emits DnaExtracted on the bus so the HUD DNA meter moves,
//   - builds + wires the HUD / DialogueSystem / EmpathyPanel on the UI
//     CanvasLayer (Bind + ConnectBus) so the C2 bus drives the readouts.
//
// It owns NO gameplay logic of its own — it wires the existing pure modules
// (EnemyAI, PlayerController, EventBus, the M10 Controls) into the scene (I1).
namespace LastAnimal.World;

[GlobalClass]
public partial class WorldDirector : Node3D
{
    /// <summary>The Player shell (movement handled by Player.cs).</summary>
    [Export] public Node3D? Player { get; set; }

    /// <summary>The CanvasLayer into which the HUD/panels are placed.</summary>
    [Export] public CanvasLayer? UICanvas { get; set; }

    // Player sidecar (pure logic) mirroring the shell's position each frame.
    private PlayerController _player = null!;
    private EventBus _bus = null!;

    private Hud _hud = null!;
    private DialogueSystem _dialogue = null!;
    private EmpathyPanel _empathy = null!;

    private readonly List<EnemyActor> _enemies = new();

    // Enemy spawn ring around the player's start, so several stay in the
    // default camera frame.
    private static readonly (float X, float Z)[] SPAWNS =
    {
        ( 5f,  0f),
        (-4f,  4f),
        ( 0f, -5f),
    };

    public override void _Ready()
    {
        // Resolve the composition references at runtime (robust on any load
        // path) rather than relying on .tscn exported NodePath binding.
        Player ??= GetNodeOrNull<Node3D>("Player");
        UICanvas ??= GetNodeOrNull<CanvasLayer>("UI");

        _bus = GetNode<EventBus>("/root/EventBus");
        Vector3 origin = Player?.GlobalPosition ?? Vector3.Zero;
        _player = new PlayerController(new CombatVec3(origin.X, 0f, origin.Z));

        BuildUi();
        SpawnEnemies();
        AddChild(new CompanionActor { Name = "Companion", Target = Player, Y = 0.55f });
        GD.Print($"W3DBG: director ready player={(Player != null)} uicanvas={(UICanvas != null)} enemies={_enemies.Count} origin={origin}");
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
        Color[] colours =
        {
            new Color(0.9f, 0.25f, 0.2f),   // red goblin
            new Color(0.2f, 0.7f, 0.35f),   // green goblin
            new Color(0.55f, 0.45f, 0.9f),  // purple goblin
        };
        Vector3 origin = Player?.GlobalPosition ?? Vector3.Zero;
        for (int i = 0; i < SPAWNS.Length; i++)
        {
            var actor = new EnemyActor { Name = $"Enemy{i}" };
            actor.Configure(EnemyAI.Type.Goblin,
                origin.X + SPAWNS[i].X, origin.Z + SPAWNS[i].Z,
                2000 + i, colours[i]);
            AddChild(actor);
            _enemies.Add(actor);
        }
    }

    public override void _Process(double delta)
    {
        if (Player == null || _enemies.Count == 0) return;
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

        if (Input.IsActionJustPressed("attack"))
            TryAttack();
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

        best.Damage(_player.MeleeDamage);
        if (best.IsDead)
        {
            best.KillHide();
            _bus.EmitDnaExtracted(new DnaSignature(best.Ai.EntityId.ToString(), "goblin"));
            GD.Print("W3: enemy killed -> DnaExtracted emitted (HUD DNA meter +1)");
        }
    }
}
