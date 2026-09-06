using Godot;

// Last Animal — M10 ui-hud (MC 890.14, dobbie, 2026-09-06).
//
// C13 (PHASE0.md line 382): `DialogueSystem.Show(nodeId)`.
// The dialogue box view: a Godot Control that, given a node id, shows that
// node's dialogue text in an on-screen box and reports which node is active.
// A View (I4 seam): it renders text the gameplay modules ask it to show; it
// owns no dialogue-authoring or branching logic.
//
// The headless test (tests/ui/UiRenderTest.cs) drives Show() and proves the
// box renders (non-blank framebuffer via graphical-test-helper) AND that the
// active node id mirrors the last Show() call (Close() resets to none).
namespace LastAnimal.Ui;

/// <summary>
/// On-screen dialogue box (C13). `Show(nodeId)` renders the dialogue for a
/// node and makes that node the active one; `Close()` dismisses it.
/// </summary>
public partial class DialogueSystem : Control
{
    /// <summary>The node id currently on screen, or empty when closed.</summary>
    public string ActiveNode { get; private set; } = string.Empty;

    private Label? _textLabel;

    /// <summary>
    /// Open a dialogue node on screen: record it as active and paint its text.
    /// </summary>
    public void Show(string nodeId)
    {
        ActiveNode = nodeId ?? string.Empty;
        BuildIfNeeded();
        EnsureVisible();
        _textLabel!.Text = DialogueFor(ActiveNode);
    }

    /// <summary>Dismiss the dialogue box (clears the active node).</summary>
    public void Close()
    {
        ActiveNode = string.Empty;
        Visible = false;
    }

    /// <summary>Is a dialogue node currently on screen?</summary>
    public bool IsOpen => ActiveNode.Length > 0;

    private void BuildIfNeeded()
    {
        if (_textLabel != null) return;
        SetAnchorsPreset(LayoutPreset.CenterBottom);
        var panel = new Panel { Name = "DialoguePanel" };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        _textLabel = new Label { Name = "DialogueText" };
        _textLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _textLabel.AddThemeFontSizeOverride("font_size", 22);
        _textLabel.Modulate = new Color(1.0f, 1.0f, 0.9f);
        panel.AddChild(_textLabel);
    }

    private void EnsureVisible() => Visible = true;

    /// <summary>
    /// Map a node id to its on-screen text. The rich narrative lives in the
    /// script/data layer; this stub keeps the View honest with a diegetic
    /// fallback so the box never renders blank.
    /// </summary>
    private static string DialogueFor(string nodeId) => nodeId switch
    {
        "intro"  => "The last animal stands at the edge of the world.",
        "meadow" => "A breeze moves the tall grass. Something watches.",
        "betray" => "The companion turns. There may be no turning back.",
        _        => $"Dialogue node [{nodeId}]."
    };
}
