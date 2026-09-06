using Godot;
using LastAnimal.Core;
using LastAnimal.Core.Framework;
using LastAnimal.Empathy;
using LastAnimal.Npc;
using LastAnimal.Ui;

// Last Animal — M10 ui-hud render DoD test (MC 890.14, dobbie, 2026-09-06).
//
// Phase-11 DoD (PHASE0.md Phase 11, C13):
//   "headless render of the HUD scene is non-blank with all bound values
//    updated from EventBus (graphical-test-helper exit 0); EmpathyPanel opens
//    a real BookEntry read from M04."
//
// This is a SceneTree script (NOT the pure-logic suite). It is run by
// graphical-test-helper.sh on vm105 WITHOUT --headless so an Xvfb display is
// live and the UI scene genuinely paints to the framebuffer; the helper
// asserts the capture is non-blank (the pixel bar, exit 0). In-code we assert
// the C13 contract: Hud binds four gauges and moves them from EventBus
// signals (DnaMeter from DnaExtracted/DnaSpoken, CompanionHearts from
// LoyaltyChanged; Life/Manna via the combat seam), DialogueSystem.Show paints
// a node, and EmpathyPanel.Open surfaces a REAL M04 BookEntry read from
// EmpathyBook.Query.
//
// Run: graphical-test-helper.sh --cmd ".../godot --path <proj> --script res://tests/ui/UiRenderTest.cs"
public partial class UiRenderTest : SceneTree
{
    private int _failures;
    private int _stage;               // 0=compose,1=drive,2=assert,3=done

    private Hud? _hud;
    private DialogueSystem? _dialogue;
    private EmpathyPanel? _panel;
    private EventBus? _bus;

    public override void _Initialize()
    {
        GD.Print("M10_UI_RENDER_TEST: start");
        _stage = 1;

        var canvas = new CanvasLayer { Name = "UiCanvas" };
        Root.AddChild(canvas);

        // Give the framebuffer a non-black backdrop so the pixel gate measures
        // the UI painting on real content, not stray anti-aliasing on black.
        var backdrop = new ColorRect();
        backdrop.Color = new Color(0.10f, 0.12f, 0.14f);
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(backdrop);

        // The C2 bus is wired explicitly into Hud + EmpathyPanel (I4 seam).
        _bus = new EventBus { Name = "EventBus" };
        Root.AddChild(_bus);

        // HUD: bind four gauges.
        _hud = new Hud { Name = "Hud" };
        canvas.AddChild(_hud);
        _hud.Bind(100, 100, 0, 5);   // order per C13: Life, Manna, DnaMeter, CompanionHearts
        _hud.ConnectBus(_bus);

        // Dialogue box.
        _dialogue = new DialogueSystem { Name = "DialogueSystem" };
        canvas.AddChild(_dialogue);

        // EmpathyPanel (M04 surface).
        _panel = new EmpathyPanel { Name = "EmpathyPanel" };
        canvas.AddChild(_panel);
        _panel.ConnectBus(_bus);

        GD.Print("M10_UI_RENDER_TEST: driving UI across real process frames");
    }

    public override bool _Process(double delta)
    {
        if (_stage == 1)
        {
            _stage = 2;

            // --- bind a real M04 BookEntry from EmpathyBook.Query ----------
            var companion = new CompanionComponent { Id = 7, CompanionEntityId = 2, Loyalty = 55 };
            BookEntry? entry = LastAnimal.Empathy.EmpathyBook.Query(companion);
            Check("M04 EmpathyBook.Query returns a real entry for a following companion",
                  entry != null, $"entry={(entry == null ? "null" : entry.ToString())}");
            if (entry != null)
            {
                _panel!.Open(entry);
                Check("EmpathyPanel surfaces M04 hidden EmotionalState",
                      _panel.Current != null && _panel.Current.EmotionalState == "Neutral",
                      $"state={_panel.Current?.EmotionalState}");
                Check("EmpathyPanel surfaces M04 summary text (non-empty)",
                      !string.IsNullOrEmpty(_panel.Current?.Summary),
                      $"summary=[{_panel.Current?.Summary}]");
                Check("EmpathyPanel surfaces M04 hidden Hint (non-empty)",
                      !string.IsNullOrEmpty(_panel.Current?.Hint),
                      $"hint=[{_panel.Current?.Hint}]");
            }

            // --- DialogueSystem.Show paints a node -------------------------
            _dialogue!.Show("meadow");
            Check("DialogueSystem.Show sets the active node",
                  _dialogue.ActiveNode == "meadow", $"active={_dialogue.ActiveNode}");

            // --- drive the HUD from the EventBus ---------------------------
            Check("Hud binds DnaMeter to 0 at open",
                  _hud!.DnaMeter == 0, $"dna={_hud.DnaMeter}");
            _bus!.EmitDnaExtracted(new DnaSignature("sig-1", "hunter"));
            _bus.EmitDnaSpoken(new DnaSignature("sig-1", "hunter"));
            Check("Hud.DnaMeter updated FROM EventBus (DnaExtracted+DnaSpoken)",
                  _hud.DnaMeter == 2, $"dna={_hud.DnaMeter} (want 2)");

            Check("Hud binds CompanionHearts to 5 at open",
                  _hud.CompanionHearts == 5, $"hearts={_hud.CompanionHearts}");
            _bus.EmitLoyaltyChanged(new CompanionId("garn"), 40);
            Check("Hud.CompanionHearts updated FROM EventBus (LoyaltyChanged)",
                  _hud.CompanionHearts == 2, $"hearts={_hud.CompanionHearts} (want 2: 40/20)");

            // --- combat seam moves Life/Manna ------------------------------
            _hud.UpdateLife(60);
            _hud.UpdateManna(25);
            Check("Hud.Life moved via combat seam", _hud.Life == 60, $"life={_hud.Life}");
            Check("Hud.Manna moved via combat seam", _hud.Manna == 25, $"manna={_hud.Manna}");

            GD.Print("M10_UI_RENDER_TEST: staged assertions done; letting UI paint");
        }

        // Keep the app alive long enough for the graphical-test-helper to capture
        // a live, still-painting frame (helper default --wait 3 ≈ 180 frames at
        // 60fps), then Quit with the verdict. Under --headless frames run uncapped
        // so the threshold is reached almost immediately — quick exit + verdict.
        if (++_frameCounter >= 600)
        {
            GD.Print(_failures == 0
                ? "M10_UI_RENDER_TEST: PASS — HUD + Dialogue + EmpathyPanel composed; bound values updated from EventBus; EmpathyPanel surfaced real M04 BookEntry; framebuffer via graphical-test-helper"
                : $"M10_UI_RENDER_TEST: FAIL ({_failures} check(s) failed)");
            Quit(_failures == 0 ? 0 : 1);
        }
        return false;
    }

    private int _frameCounter;

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"M10_UI_RENDER_TEST: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) _failures++;
    }
}
