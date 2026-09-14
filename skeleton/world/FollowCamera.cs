using Godot;

// Last Animal — W3-fix composition (MC 1123.10, artemis, 2026-09-08).
//
// FollowCamera: the SINGLE current camera of the playable composition root.
// A small isometric third-person follow cam. It is deliberately decoupled
// from the Player physics shell (Player.cs only drives movement), so framing
// is one concern in one small node — a fixed behind-and-above offset that
// keeps terrain, player, enemies and companion in frame, with sky visible.
//
// (I4 seam: a View-layer node; it only reads the target transform and frames
// it. It owns no gameplay logic.)
namespace LastAnimal.World;

[GlobalClass]
public partial class FollowCamera : Node3D
{
    /// <summary>The node to frame (the Player). Null -> stays put.</summary>
    [Export] public Node3D? Target { get; set; }

    /// <summary>World-space offset behind-and-above the target.</summary>
    [Export] public Vector3 Offset { get; set; } = new Vector3(8f, 6f, 8f);

    /// <summary>Smoothing factor (higher = snappier follow).</summary>
    [Export] public float LerpSpeed { get; set; } = 5f;

    public override void _Ready()
    {
        // Robust runtime resolution: this FollowCamera lives on the composition
        // root's Camera node, whose sibling Player is the frame target.
        Target ??= GetNodeOrNull<Node3D>("../Player");
        if (Target != null)
            GlobalPosition = Target.GlobalPosition + Offset;
    }

    public override void _Process(double delta)
    {
        if (Target == null) return;

        float t = 1f - Mathf.Exp(-LerpSpeed * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(Target.GlobalPosition + Offset, t);
        LookAt(Target.GlobalPosition + new Vector3(0, 1f, 0), Vector3.Up);
    }
}
