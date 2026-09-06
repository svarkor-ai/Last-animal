using Godot;
using LastAnimal.Core;

// Last Animal — M04 empathy-book signal DoD test (MC 890.11, dobbie, 2026-09-06).
//
// Phase-8 DoD (PHASE0.md Phase 8): "`EmpathyBookOpened()` signal fires (C2)".
// The Empathy-Book's pure logic (EmpathyBook.Query/RouteResolution) is proven by
// the standalone xunit suite (tests/empathy/). This script proves the OTHER half
// of the DoD: the C2 signal is wired on the Godot EventBus autoload and actually
// fires to a subscriber on open.
//
// Note: the C9 module itself does NOT touch the bus (I4 — no cross-module refs).
// The signal is emitted by the caller (the M10 EmpathyPanel) via
// EventBus.EmitEmpathyBookOpened(); this test stands in for that caller and
// asserts the signal reaches a subscriber headlessly. Run via
// `godot --headless --script res://tests/EmpathySignalTest.cs` (see ci/empathy_book_test.sh).
public partial class EmpathySignalTest : SceneTree
{
    private int _failures = 0;

    public override void _Initialize()
    {
        GD.Print("M04_EMPATHY_SIGNAL_TEST: start");

        var root = new Node3D { Name = "EmpathySignalTestRoot" };
        Root.AddChild(root);

        // Compose the EventBus autoload in the tree so signals deliver (headless).
        var bus = new EventBus();
        root.AddChild(bus);

        // Subscribe to EmpathyBookOpened before emitting.
        int openedCount = 0;
        bus.EmpathyBookOpened += () => openedCount++;

        // Assert the clean state, then fire the open signal exactly once.
        Check("signal starts at 0", openedCount == 0, $"got {openedCount}");
        bus.EmitEmpathyBookOpened();
        Check("EmpathyBookOpened() fired to subscriber", openedCount == 1,
              $"got {openedCount} (want 1)");

        // Fire twice more to prove it is a live, repeatable signal (not a one-shot).
        bus.EmitEmpathyBookOpened();
        bus.EmitEmpathyBookOpened();
        Check("signal delivers on repeat emits", openedCount == 3, $"got {openedCount} (want 3)");

        // --- verdict ------------------------------------------------------
        if (_failures == 0)
            GD.Print("M04_EMPATHY_SIGNAL_TEST: PASS — EmpathyBookOpened() fired to subscriber (C2)");
        else
            GD.Print($"M04_EMPATHY_SIGNAL_TEST: FAIL ({_failures} check(s) failed)");

        Quit(_failures == 0 ? 0 : 1);
    }

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"M04_EMPATHY_SIGNAL_TEST: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) _failures++;
    }
}
