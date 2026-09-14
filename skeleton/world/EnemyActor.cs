using Godot;
using LastAnimal.Combat;

// Last Animal — W3-fix composition (MC 1123.10, artemis, 2026-09-08).
//
// EnemyActor: the VISIBLE enemy body in the playable scene. It wraps a pure
// M08 EnemyAI (C11, engine-free, REUSED verbatim — no AI logic re-authored
// here) in a thin CharacterBody3D shell: gravity + MoveAndSlide so it rests
// on the meadow heightfield terrain exactly like the Player, a coloured mesh
// so it is actually visible, and velocity toward the AI's target so it
// approaches/chases the player.
//
// Each frame the shell ticks the pure EnemyAI toward the player's XZ position
// and steers the physics body toward where the AI wants to be, then reports
// how much damage the AI dealt this frame (so WorldDirector can apply it to
// the HUD/player). Movement Y is handled purely by gravity (the AI is planar).
namespace LastAnimal.World;

[GlobalClass]
public partial class EnemyActor : CharacterBody3D
{
    private const float Gravity = 9.8f;

    public EnemyAI Ai { get; private set; } = null!;
    public EnemyAI.Type Kind { get; private set; }
    public bool IsDead => Ai.IsDead;

    public MeshInstance3D? Visual { get; private set; }

    /// <summary>Configure the enemy: type, spawn position, entity id, mesh colour.</summary>
    public void Configure(EnemyAI.Type type, float x, float z, int entityId, Color colour)
    {
        Kind = type;
        Ai = new EnemyAI(new CombatVec3(x, 0f, z), entityId, type, seed: entityId);
        Position = new Vector3(x, 4f, z);   // spawn above the terrain, gravity settles it down

        var mesh = new MeshInstance3D { Name = "Visual" };
        var box = new BoxMesh { Size = new Vector3(0.9f, 1.5f, 0.9f) };
        mesh.Mesh = box;
        // Emission = albedo so the enemy is clearly visible even where the
        // single top-down light leaves shadow (robust on llvmpipe software GL).
        var mat = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.7f };
        mat.EmissionEnabled = true;
        mat.Emission = colour;
        mat.EmissionEnergyMultiplier = 0.6f;
        mesh.MaterialOverride = mat;
        AddChild(mesh);
        Visual = mesh;

        var shape = new CollisionShape3D();
        shape.Shape = new BoxShape3D { Size = new Vector3(0.9f, 1.5f, 0.9f) };
        AddChild(shape);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Ai == null || Ai.IsDead) return;
        float dt = (float)delta;

        // Tick the pure AI toward the player and read the resulting position.
        int dealt = Ai.SetBehavior(new CombatVec3(PlayerTargetX, 0f, PlayerTargetZ), dt);
        DamageDealt = dealt;

        // Steer the physics body toward the AI's planned XZ position.
        float dx = Ai.Position.X - GlobalPosition.X;
        float dz = Ai.Position.Z - GlobalPosition.Z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);

        Vector3 vel = Velocity;
        vel.Y -= Gravity * dt;
        if (dist > 0.02f)
        {
            vel.X = dx / dist * Ai.Speed * (Ai.CurrentState == EnemyAI.State.Patrol ? 0.5f : 1f);
            vel.Z = dz / dist * Ai.Speed * (Ai.CurrentState == EnemyAI.State.Patrol ? 0.5f : 1f);
            if (Visual != null)
                Visual.LookAt(new Vector3(GlobalPosition.X + dx, GlobalPosition.Y, GlobalPosition.Z + dz), Vector3.Up);
        }
        else
        {
            vel.X = 0f;
            vel.Z = 0f;
        }
        Velocity = vel;
        MoveAndSlide();
    }

    /// <summary>The player's XZ position, set by WorldDirector each frame.</summary>
    public float PlayerTargetX { get; set; }
    public float PlayerTargetZ { get; set; }

    /// <summary>Damage this enemy dealt to the player on the last AI tick.</summary>
    public int DamageDealt { get; private set; }

    public void Damage(int amount)
    {
        if (Ai != null && !Ai.IsDead)
            Ai.TakeDamage(amount);
    }

    public void KillHide()
    {
        Visible = false;
        SetPhysicsProcess(false);
    }
}
