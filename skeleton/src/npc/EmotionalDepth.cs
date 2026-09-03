using System;

// Last Animal — M03 npc-emotion (MC 890.2, dobbie, 2026-09-03).
//
// C8 EXTENSION (not in the prior art): EmotionalDepth.
//
// Contract (PHASE0.md C8):
//   EmotionalDepth.ReadHiddenState(CompanionId) -> EmotionState
//   — the hidden-state read that Empathy-Book surfaces (not present in prior art).
//
// Design (pure logic, no Godot types, no window — I3):
//   The companion's hidden emotional state is derived from its loyalty level
//   and recent salary history. The state is one of:
//     - Content:   loyalty >= 70 (the companion is well-treated)
//     - Neutral:   loyalty 40-69 (the companion is neither pleased nor upset)
//     - Anxious:   loyalty 20-39 (the companion is worried about being abandoned)
//     - Betrayed:  loyalty <= 19 (the companion has turned against the owner)
//   The state also carries the raw loyalty value and a "depth" score (0-1)
//   that the Empathy-Book can use to gauge how deep the emotional state is.
//
// This is the "hidden-state read" half of C8: the state is a pure function
// of the companion's current loyalty, so it is deterministic and testable
// headless. The C2 LoyaltyChanged signal is fired by the caller (the module
// that owns the EventBus) when loyalty changes — this class does not touch
// the bus (I4: no cross-module refs).
namespace LastAnimal.Npc;

/// <summary>
/// The hidden emotional state of a companion (C8).
/// </summary>
public class EmotionState
{
    /// <summary>The companion's current loyalty level (0-100).</summary>
    public int Loyalty { get; }

    /// <summary>
    /// The emotional state label: "Content", "Neutral", "Anxious", or "Betrayed".
    /// </summary>
    public string State { get; }

    /// <summary>
    /// Depth score (0-1): how deep the emotional state is.
    /// Content=1.0, Neutral=0.5, Anxious=0.3, Betrayed=0.0.
    /// The Empathy-Book uses this to gauge emotional depth.
    /// </summary>
    public float Depth { get; }

    public EmotionState(int loyalty, string state, float depth)
    {
        Loyalty = loyalty;
        State = state;
        Depth = depth;
    }

    public override string ToString() =>
        $"EmotionState({State}, loyalty={Loyalty}, depth={Depth:F2})";
}

/// <summary>
/// Emotional depth read (C8): derives the companion's hidden emotional state
/// from its loyalty level. Pure function — no state, no bus, no window.
/// </summary>
public static class EmotionalDepth
{
    /// <summary>
    /// Read the hidden emotional state of a companion from its loyalty level.
    ///
    /// State thresholds (loyalty 0-100):
    ///   - Content:   loyalty >= 70  (depth 1.0)
    ///   - Neutral:   loyalty 40-69  (depth 0.5)
    ///   - Anxious:   loyalty 20-39  (depth 0.3)
    ///   - Betrayed:  loyalty <= 19  (depth 0.0)
    ///
    /// Returns null when the companion is null (no companion to read).
    /// </summary>
    public static EmotionState? ReadHiddenState(CompanionComponent? comp)
    {
        if (comp == null) return null;

        int loyalty = comp.Loyalty;
        string state;
        float depth;

        if (loyalty >= 70)
        {
            state = "Content";
            depth = 1.0f;
        }
        else if (loyalty >= 40)
        {
            state = "Neutral";
            depth = 0.5f;
        }
        else if (loyalty >= 20)
        {
            state = "Anxious";
            depth = 0.3f;
        }
        else
        {
            state = "Betrayed";
            depth = 0.0f;
        }

        return new EmotionState(loyalty, state, depth);
    }
}
