using Godot;
using LastAnimal.Core;
using LastAnimal.Core.Framework;
using System;
using System.Collections.Generic;

// Last Animal — M01 core-framework headless boot test (MC 839.5, teddy, 2026-09-02).
//
// The Phase-3 DoD gate (PHASE0.md Phase 3): instantiate GameBootstrap + EventBus,
// fire ONE global signal with a subscriber receiving it, exit 0; plus prove C3
// (DI Bind/Resolve round-trip + boot order = registration order).
//
// Run via `godot --headless --script res://tests/BootTest.cs` (see ci/boot_test.sh).
// `--script` requires a SceneTree/MainLoop root, so this is a SceneTree: it builds
// the scene tree, runs all checks in _Initialize(), and Quits with that code.
// No render, no window, no X — fully headless.
//
// M09 repair (MC 839.4, henrik): Godot 4.7.2's C# generator rejects [Signal]
// parameters that are user types (GD0202) and supplies a generated SignalName
// class. So each EventBus signal carries its carrier's string Id, and the C#
// subscriber below wraps that Id back into a DnaSignature (the carrier stays the
// canonical in-C# payload). This test swallows the Id and re-wraps it.
public partial class BootTest : SceneTree
{
    private int _failures = 0;

    public override void _Initialize()
    {
        GD.Print("BOOT_TEST: start");

        var root = new Node3D { Name = "BootTestRoot" };
        Root.AddChild(root);

        // --- C3: GameBootstrap + EventBus instantiated + DI round-trip -----
        var boot = new GameBootstrap();
        var bus = new EventBus();
        root.AddChild(boot);                     // node-tree parent for signal delivery
        boot.AddChild(bus);
        boot.Bind<EventBus>(bus);                 // C3 Bind<T>
        Check("C3 DI round-trip", boot.Resolve<EventBus>() == bus,
              $"expected same instance, got {boot.Resolve<EventBus>()}");
        Check("C3 Resolve-missing -> null", boot.Resolve<IGameModule>() == null,
              "an unbound optional module must resolve to null, not throw");

        // --- C2: one global signal, one subscriber, must be received ------
        // EventBus emits the signal with the carrier's Id (a string — see M09
        // repair note). The subscriber receives that Id and re-wraps it into the
        // canonical carrier so the check asserts on the record, not the wire form.
        DnaSignature? received = null;
        bus.DnaExtracted += id => received = new DnaSignature(id, "<wrapped>");
        var payload = new DnaSignature("sig_001", "wolf");
        bus.EmitDnaExtracted(payload);
        Check("C2 signal received by subscriber",
              received != null && received.Id == payload.Id,
              $"got {received?.Id ?? "<null>"} (want {payload.Id})");

        // --- C3: boot order = module registration order (I6) ---------------
        var order = new List<string>();
        boot.Bind(new OrderProbe("m1", order));   // registered first -> must boot first
        boot.Bind(new OrderProbe("m2", order));
        boot.Boot();
        Check("C3 boot order = registration order",
              order.Count == 2 && order[0] == "m1" && order[1] == "m2",
              $"got [{string.Join(",", order)}]");

        // --- Verdict -------------------------------------------------------
        if (_failures == 0)
            GD.Print("BOOT_TEST: PASS signal received");
        else
            GD.Print($"BOOT_TEST: FAIL ({_failures} check(s) failed)");

        // Stop the engine with the appropriate exit code.
        Quit(_failures == 0 ? 0 : 1);
    }

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"BOOT_TEST: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) _failures++;
    }

    // Minimal IGameModule: records its boot name to prove registration order.
    private sealed class OrderProbe : IGameModule
    {
        private readonly string _name;
        private readonly List<string> _sink;
        public OrderProbe(string name, List<string> sink) { _name = name; _sink = sink; }
        public string Name => _name;
        public void Boot() => _sink.Add(_name);
    }
}
