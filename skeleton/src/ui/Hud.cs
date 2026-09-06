using Godot;
using LastAnimal.Core;
using LastAnimal.Core.Framework;

// Last Animal — M10 ui-hud (MC 890.14, dobbie, 2026-09-06).
//
// C13 (PHASE0.md line 382): `Hud.Bind(Life, Manna, DnaMeter, CompanionHearts)`.
// The head-up display: the player's four readouts, rendered as a Godot Control
// tree of Labels. It is a View (I4 seam): it binds the C2 EventBus so its
// gauge values move when the game speaks, and it owns NO gameplay logic.
//
//   Life / Manna          — driven by combat (M08). There is no C2 signal in
//                           the contract for these, so the HUD exposes
//                           UpdateLife/UpdateManna for the combat seam to call.
//   DnaMeter              — updated FROM the bus: every DnaExtracted /
//                           DnaSpoken fires and bumps the meter the player
//                           reads before speaking.
//   CompanionHearts       — updated FROM the bus: every LoyaltyChanged fires
//                           and re-draws the hearts for that companion.
//
// The Headless test (tests/ui/UiRenderTest.cs) drives every one of these and
// asserts the rendered labels reflect the bus-updated values.
namespace LastAnimal.Ui;

/// <summary>
/// The four-gauge heads-up display (C13). Renders Life / Manna / DnaMeter /
/// CompanionHearts as child Labels. Two gauges move on the C2 EventBus
/// (DnaMeter, CompanionHearts); Life/Manna move through the combat seam.
/// </summary>
public partial class Hud : Control
{
    // --- the four bound readouts -----------------------------------------
    public int Life { get; private set; }
    public int Manna { get; private set; }
    public int DnaMeter { get; private set; }
    public int CompanionHearts { get; private set; }

    private Label? _lifeLabel;
    private Label? _mannaLabel;
    private Label? _dnaLabel;
    private Label? _heartsLabel;

    private EventBus? _bus;

    /// <summary>
    /// Bind the four values that seed the gauges. The bus is wired separately
    /// via <see cref="ConnectBus"/> so composition sites (game scene vs headless
    /// test) choose how the bus reaches the HUD — no fragile absolute-path
    /// autoload lookup inside a View (I4 seam).
    /// </summary>
    public void Bind(int life, int manna, int dnaMeter, int companionHearts)
    {
        Life = life;
        Manna = manna;
        DnaMeter = dnaMeter;
        CompanionHearts = companionHearts;

        BuildControls();
        Redraw();
    }

    /// <summary>
    /// Subscribe to the C2 EventBus so DnaMeter (DnaExtracted/DnaSpoken) and
    /// CompanionHearts (LoyaltyChanged) follow the game from the bus.
    /// </summary>
    public void ConnectBus(EventBus bus)
    {
        if (_bus == bus) return;
        if (_bus != null) DisconnectBus(_bus);
        _bus = bus;
        _bus.DnaExtracted += OnDnaEvent;
        _bus.DnaSpoken += OnDnaEvent;
        _bus.LoyaltyChanged += OnLoyaltyChanged;
        Redraw();
    }

    /// <summary>Unsubscribe from the bus on teardown.</summary>
    public override void _ExitTree()
    {
        if (_bus != null)
        {
            DisconnectBus(_bus);
            _bus = null;
        }
        base._ExitTree();
    }

    private void DisconnectBus(EventBus bus)
    {
        bus.DnaExtracted -= OnDnaEvent;
        bus.DnaSpoken -= OnDnaEvent;
        bus.LoyaltyChanged -= OnLoyaltyChanged;
    }

    /// <summary>Combat seam: set the Life readout and redraw. Clamped to [0,100].</summary>
    public void UpdateLife(int life)
    {
        Life = Clamp(life);
        Redraw();
    }

    /// <summary>Combat seam: set the Manna readout and redraw. Clamped to [0,100].</summary>
    public void UpdateManna(int manna)
    {
        Manna = Clamp(manna);
        Redraw();
    }

    private void OnDnaEvent(string signature)
    {
        DnaMeter = Mathf.Min(DnaMeter + 1, 100);
        Redraw();
    }

    private void OnLoyaltyChanged(string companion, int loyalty)
    {
        // Hearts = loyalty percent translated to 1-5 hearts, bus-driven.
        CompanionHearts = Mathf.Clamp((int)Mathf.Round(loyalty / 20f), 0, 5);
        Redraw();
    }

    private static int Clamp(int v) => Mathf.Clamp(v, 0, 100);

    private void BuildControls()
    {
        if (_lifeLabel != null) return;          // already built

        SetAnchorsPreset(LayoutPreset.FullRect);

        _lifeLabel = MakeLabel("Life");
        _mannaLabel = MakeLabel("Manna");
        _dnaLabel = MakeLabel("DNA");
        _heartsLabel = MakeLabel("Hearts");

        // Stack the four gauges down the top-left corner so all are readable.
        for (int i = 0; i < 4; i++)
        {
            Label l = i switch
            {
                0 => _lifeLabel,
                1 => _mannaLabel,
                2 => _dnaLabel,
                _ => _heartsLabel
            };
            l.Position = new Vector2(8, 8 + i * 28);
            AddChild(l);
        }
    }

    private static Label MakeLabel(string title)
    {
        var label = new Label();
        label.Name = $"{title}Gauge";
        label.AddThemeFontSizeOverride("font_size", 20);
        label.Modulate = new Color(1.0f, 1.0f, 1.0f);
        return label;
    }

    private void Redraw()
    {
        if (_lifeLabel == null) return;
        _lifeLabel!.Text = $"Life: {Life}/100";
        _mannaLabel!.Text = $"Manna: {Manna}/100";
        _dnaLabel!.Text = $"DNA meter: {DnaMeter}";
        _heartsLabel!.Text = $"Hearts: {CompanionHearts}";
    }
}
