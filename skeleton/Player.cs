using Godot;
using LastAnimal.Combat;
using LastAnimal.World;

// Last Animal — M01 bridge CARD 3 (MC 1123.9, artemis, 2026-09-08).
// T3b ownership refactor (MC 1256.9, artemis, 2026-09-21): ONE controller.
//
// Player: the CharacterBody3D script that maps the WASD input map (STEP D / RG3,
// registered in 1123.2) onto the M08 pure-logic PlayerController (C10) at the
// engine seam (BRIDGE-MVP.md §2 STEP C, "maps Input -> PlayerController.Move ->
// CharacterBody3D velocity").
//
// Ownership (design 1256.2 §2): the WorldDirector — the ONE composition root —
// constructs the PlayerController. This shell no longer owns a private one
// (the old `new PlayerController(...)` here was the third live construction
// site and a second health tracker); it RESOLVES the director-owned instance
// from GameBootstrap (the binding the director made in _Ready) and drives it.
// Two-layer seam, not duplicated logic:
//   - PlayerController (src/combat/PlayerController.cs) is the pure C10 model;
//     it owns Speed/State (Run/Idle) and stays unit-testable with no window.
//   - This script is the thin physical shell: it reads the planar input vector,
//     derives the CharacterBody3D velocity at the controller's speed, adds
//     gravity so the body stands on the terrain heightfield, and feeds it
//     through MoveAndSlide (collision + navmesh ground). Each frame it also
//     advances the director-owned PlayerController.Move() so the model's live
//     State tracks the actual movement. (I4: engine types never leak into the
//     domain.)
[GlobalClass]
public partial class Player : CharacterBody3D
{
    // Standard Godot gravity applied in _PhysicsProcess (stands on the M07
    // terrain heightfield; ~9.8 m/s^2 at 60 physics ticks = 0.1633/frame^2).
    private const float Gravity = 9.8f;

    // Camera offset for the isometric action cam (BRIDGE-MVP STEP C Camera3D):
    // a fixed third-person offset that keeps the player centred-ish on screen.
    [Export] public Node3D? CameraRig { get; set; }

    /// <summary>
    /// The director-owned PlayerController (the ONE authoritative instance).
    /// Resolved lazily from GameBootstrap: the director binds it in its _Ready,
    /// which may run after this node's own _Ready depending on tree order.
    /// </summary>
    public PlayerController? Controller { get; private set; }

    public override void _Ready()
    {
        ResolveController();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Late-resolve: the director's Bind may land after this node's _Ready
        // (scene children ready before the root finishes its own _Ready work).
        Controller ??= ResolveController();
        if (Controller == null) return;   // not yet bound — skip this frame

        float dt = (float)delta;

        // --- read the [input] map (RG3) as a planar movement vector ---------
        Vector2 raw = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Vector2 dir = raw;
        if (dir.LengthSquared() > 1f) dir = dir.Normalized(); // clamp diagonal to avoid sqrt(2) speed-up

        // --- CharacterBody3D velocity: gravity (Y) + controller speed (XZ) ---
        Vector3 vel = Velocity;
        vel.Y -= Gravity * dt;             // fall onto / stand on the terrain
        vel.X = dir.X * Controller.Speed;
        vel.Z = dir.Y * Controller.Speed;
        Velocity = vel;

        // --- physics/collision move on the real ground (navmesh/colliders) ---
        MoveAndSlide();

        // --- advance the pure C10 model so its State (Run/Idle) stays live ----
        Controller.Move(new CombatVec3(raw.X, 0f, raw.Y), dt);

        // --- keep the isometric cam following the player ---------------------
        if (CameraRig != null)
            CameraRig.GlobalPosition = GlobalPosition + new Vector3(0, 6, 6);
    }

    private PlayerController? ResolveController()
    {
        var bootstrap = GetNodeOrNull<GameBootstrap>("/root/GameBootstrap");
        return bootstrap?.Resolve<PlayerController>();
    }
}
