using System;
using LastAnimal.Npc;

// Last Animal — M04 empathy-book (MC 890.11, dobbie, 2026-09-06).
//
// C9 (M04 port+extension, NEW — no prior art; PHASE0.md C9):
//   EmpathyBook.Query(CompanionId) -> BookEntry
//   EmpathyBook.RouteResolution(CompanionId, empathy) -> Forgive | PermanentBreak
//
// The diegetic Empathy-Book: persistent logic + read/write of hidden emotional
// states; empathy-metric -> forgiveness vs permanent betrayal routing. Its UI
// surface lives in M10 (EmpathyPanel); the logic here is pure (I3 — no Godot
// types, no window — testable headless under `dotnet test`).
//
// Reuse (I2 / reuse-first): the book READS the companion's hidden emotional
// state through M03's C8 extension `EmotionalDepth.ReadHiddenState`, which was
// built (MC 890.2) specifically as "the hidden-state read that Empathy-Book
// surfaces". This class does not re-derive the state; it consumes C8 and adds
// the book's own concern: the summary/hint read + the empathy-routing decision.
//
// The EmpathyBook does NOT touch the EventBus (I4 — no cross-module refs). The
// `EmpathyBookOpened()` C2 signal is emitted by the caller when the book opens;
// this module only provides the pure decision. (The signal itself is wired on
// the EventBus autoload and proven by a headless Godot test — see EmpathySignalTest.)
namespace LastAnimal.Empathy;

/// <summary>
/// The resolution the Empathy-Book routes a relationship toward: either the
/// companion is forgiven (bond kept) or the break is made permanent.
/// </summary>
public enum Resolution
{
    /// <summary>The owner forgives the companion; the bond continues.</summary>
    Forgive,

    /// <summary>The relationship is severed permanently.</summary>
    PermanentBreak
}

/// <summary>
/// One entry in the Empathy-Book (C9): the companion's identity plus the
/// surfaced read of its hidden emotional state — a short summary and a
/// "hidden hint" the player can act on. The hidden state itself comes from
/// M03's C8 (EmotionalDepth); the book only packages it for the reader.
/// </summary>
public class BookEntry
{
    /// <summary>The companion's identity (the same string-key the C2 signals carry).</summary>
    public string CompanionId { get; }

    /// <summary>The hidden emotional-state label (Content / Neutral / Anxious / Betrayed).</summary>
    public string EmotionalState { get; }

    /// <summary>The companion's raw loyalty (0-100) that produced the state.</summary>
    public int Loyalty { get; }

    /// <summary>The emotional depth score (0-1) from C8 — how deep the state runs.</summary>
    public float Depth { get; }

    /// <summary>A short diegetic summary of the companion's hidden state.</summary>
    public string Summary { get; }

    /// <summary>The hidden hint the book surfaces to the player.</summary>
    public string Hint { get; }

    public BookEntry(string companionId, string emotionalState, int loyalty, float depth,
                     string summary, string hint)
    {
        CompanionId = companionId;
        EmotionalState = emotionalState;
        Loyalty = loyalty;
        Depth = depth;
        Summary = summary;
        Hint = hint;
    }

    public override string ToString() =>
        $"BookEntry({CompanionId}: {EmotionalState}, loyalty={Loyalty}, depth={Depth:F2})";
}

/// <summary>
/// The Empathy-Book system (C9): queries a companion's hidden emotional state
/// into a readable BookEntry and routes the empathy decision toward forgiveness
/// or a permanent break. Pure logic — no state, no bus, no window.
/// </summary>
public static class EmpathyBook
{
    /// <summary>
    /// Empathy generosity at or above this value routes a non-severed relationship
    /// to Forgive. In [0,1].
    /// </summary>
    public const float ForgiveThreshold = 0.5f;

    /// <summary>
    /// Read a companion's hidden emotional state into an Empathy-Book entry.
    /// Returns null when there is no companion to read (same guard as C8).
    ///
    /// The hidden state is delegated to M03's C8 (EmotionalDepth.ReadHiddenState);
    /// the book only wraps it with a reader-facing Summary and Hint.
    /// </summary>
    public static BookEntry? Query(CompanionComponent? comp)
    {
        var state = EmotionalDepth.ReadHiddenState(comp);   // reuse C8 — do not re-derive
        if (state == null) return null;

        string summary = state.State switch
        {
            "Content"  => "The companion rests easy in your care.",
            "Neutral"  => "The companion is neither glad nor wary of you.",
            "Anxious"  => "The companion fears it may be left behind.",
            "Betrayed" => "The companion has turned against you.",
            _          => "The companion's state is unknown."
        };

        string hint = state.State switch
        {
            "Content"  => "Empathy here keeps a loyal heart close.",
            "Neutral"  => "A measure of care could tip this bond one way or the other.",
            "Anxious"  => "Pay the wage it is owed before loyalty slips to betrayal.",
            "Betrayed" => "The bond is already severed; empathy cannot mend it.",
            _          => "No hint to offer."
        };

        return new BookEntry(
            comp!.Id.ToString(),
            state.State,
            state.Loyalty,
            state.Depth,
            summary,
            hint);
    }

    /// <summary>
    /// Route a relationship toward Forgive or PermanentBreak given the player's
    /// empathy generosity (0-1) and the companion's hidden state.
    ///
    /// Rules:
    ///   - No companion / null entry  -> PermanentBreak (nothing to forgive).
    ///   - A companion already in the "Betrayed" hidden state is routed to
    ///     PermanentBreak: the bond is severed ("forge betrayed"); empathy
    ///     cannot mend what already turned.
    ///   - Otherwise empathy >= ForgiveThreshold (0.5) -> Forgive, else PermanentBreak.
    /// </summary>
    public static Resolution RouteResolution(BookEntry? entry, float empathy)
    {
        if (entry == null) return Resolution.PermanentBreak;
        if (entry.EmotionalState == "Betrayed") return Resolution.PermanentBreak;
        return empathy >= ForgiveThreshold ? Resolution.Forgive : Resolution.PermanentBreak;
    }
}
