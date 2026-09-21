using Godot;
using LastAnimal.Companion;

// Last Animal — T3b ownership refactor (MC 1256.9, artemis, 2026-09-21).
//
// CompanionFollowBody: the VISIBLE companion body in the playable scene,
// replacing world/CompanionActor.cs (deleted per design 1256.2 §3). The old
// CompanionActor was a bare lerp-follower with NO machine reference — the
// machine and the visible body were two different companions.
//
// This node composes the REAL CompanionEntity (src/companion/CompanionEntity.cs,
// the M05 rig + AnimationPlayer driven by the director-owned machine) as a
// child, and adds the follow-to-player movement the rig entity lacks. The
// director constructs it with the SAME CompanionStateMachine it owns and ticks
// the entity's Advance() in its own _Process — so the machine and the visible
// body are one companion (the integration fix the design names).
//
// It owns NO gameplay logic: movement is presentation, loyalty is M03's,
// behavior is the machine's (I1).
namespace LastAnimal.World;

[GlobalClass]
public partial class CompanionFollowBody : Node3D
{
    /// <summary>The node to follow (the Player).</summary>
    [Export] public Node3D? Target { get; set; }

    /// <summary>Follow distance behind the target.</summary>
    [Export] public float FollowDistance { get; set; } = 3.5f;

    /// <summary>How quickly the companion catches up.</summary>
    [Export] public float LerpSpeed { get; set; } = 3f;

    /// <summary>Height the companion sits above the terrain baseline.</summary>
    [Export] public float Y { get; set; } = 0.55f;

    /// <summary>The machine-wired entity (rig + animation), ticked by the director.</summary>
    public CompanionEntity Entity { get; }

    public CompanionFollowBody(CompanionStateMachine machine, CompanionAnimationHook hook)
    {
        Entity = new CompanionEntity(machine, hook) { Name = "Entity" };
    }

    public override void _Ready()
    {
        AddChild(Entity);

        // A clearly visible companion marker (gold), distinct from player/enemies,
        // so the rig's bones have a readable silhouette on llvmpipe software GL.
        var mesh = new MeshInstance3D { Name = "Visual" };
        mesh.Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.6f };
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.75f, 0.15f), Roughness = 0.6f };
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.85f, 0.65f, 0.1f);
        mat.EmissionEnergyMultiplier = 0.6f;
        mesh.MaterialOverride = mat;
        AddChild(mesh);
    }

    public override void _Process(double delta)
    {
        if (Target == null) return;

        Vector3 want = Target.GlobalPosition;
        // Keep a set distance behind the target on the XZ plane.
        Vector3 p = GlobalPosition;
        Vector3 d = p - want;
        d.Y = 0f;
        float len = d.Length();
        if (len < 1e-4f)
        {
            want += new Vector3(-FollowDistance, 0f, 0f);
        }
        else
        {
            want += d / len * FollowDistance;
        }
        want.Y = Y;

        float t = 1f - Mathf.Exp(-LerpSpeed * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(want, t);
    }
}
