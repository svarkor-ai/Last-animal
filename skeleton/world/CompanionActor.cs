using Godot;

// Last Animal — W3-fix composition (MC 1123.10, artemis, 2026-09-08).
//
// CompanionActor: a VISIBLE companion that follows the Player. A lightweight
// Node3D with a coloured mesh that lerps toward a target node (the Player),
// staying on a fixed Y baseline. This gives the scene a companion you can
// actually see trail the player, per the composition bar CARD 3/CARD 6.
//
// (Note: the committed src/companion/CompanionEntity.cs is a Skeleton3D rig +
// animation hook with NO follow-to-player logic and no visible skinned mesh,
// so a bare instance renders nothing visible. This actor is the small visible
// follow body; the rig/anim pipeline (M09/C12) is out of this MVP's scope.)
namespace LastAnimal.World;

[GlobalClass]
public partial class CompanionActor : Node3D
{
    /// <summary>The node to follow (the Player).</summary>
    [Export] public Node3D? Target { get; set; }

    /// <summary>Follow distance behind the target.</summary>
    [Export] public float FollowDistance { get; set; } = 3.5f;

    /// <summary>How quickly the companion catches up.</summary>
    [Export] public float LerpSpeed { get; set; } = 3f;

    /// <summary>Height the companion sits above the terrain baseline.</summary>
    [Export] public float Y { get; set; } = 0.55f;

    public override void _Ready()
    {
        // A clearly visible companion marker (gold), distinct from player/enemies.
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
