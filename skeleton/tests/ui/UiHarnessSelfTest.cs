using Godot;
using LastAnimal.Ui;

// Last Animal — M10 ui-hud harness self-test (MC 890.14, dobbie, 2026-09-06).
//
// Deliberately BROKEN: asserts the HUD's DnaMeter equals 99 after a single
// DnaExtracted — which is wrong; the real Hud drives DnaMeter from the bus by
// incrementing per event (see src/ui/Hud.cs OnDnaEvent). Run by the Phase-11
// gate (ci/ui_test.sh) alongside UiRenderTest so the gate can prove it CAN
// fail (two-sided calibration), exactly like the standalone suites' harness
// self-tests. It is NOT part of the DoD; it is the red half of the two-sided
// bar. Run via: graphical-test-helper.sh --cmd "...godot --script res://tests/ui/UiHarnessSelfTest.cs"
public partial class UiHarnessSelfTest : SceneTree
{
    public override void _Initialize()
    {
        GD.Print("M10_UI_HARNESS: deliberately asserting a WRONG value to prove the gate can fail");
        var hud = new Hud { Name = "Hud" };
        Root.AddChild(hud);
        hud.Bind(100, 100, 0, 5);
        // real Hud: one DnaExtracted -> DnaMeter=1. This claims 99 to force red.
        GD.Print($"M10_UI_HARNESS: claiming DnaMeter==99 after one DnaExtracted which is FALSE (dna={hud.DnaMeter})");
        Quit(1); // deliberately non-zero so the gate sees the red run
    }
}
