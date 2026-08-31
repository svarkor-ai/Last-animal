using Godot;

// Last Animal - M00 preflight C# proof (MC 839.2, gunilla, 2026-08-31).
// Owner decision is C# (PHASE0.md Q1). This node is the deliberate proof that the
// pinned mono Godot 4.7.2 + .NET 8 toolchain compiles a Godot.Sharp-exposed class
// AND that the compiled assembly travels into the Windows export. M01 (coder)
// replaces this with the real composition root (GameBootstrap); see PREFLIGHT.md.
namespace LastAnimal.Preflight;

public partial class PreflightScript : Node
{
    public override void _Ready()
    {
        GD.Print("last-animal preflight skeleton: C# scene alive (",
                 Engine.GetVersionInfo(), ")");
    }
}
