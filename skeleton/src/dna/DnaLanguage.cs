using System;

// Last Animal — M02 dna-language-combat (MC 890.2, dobbie, 2026-09-03).
//
// Port of /srv/workspace/animal/src/Animal.Gameplay/DnaLanguage.cs (C4) + the
// C5 ecosystem-adaptation extension. Pure logic: no Godot types, no window —
// runs headless under `dotnet test` (I3).
//
// Port notes (semantics preserved 1:1 from the prior art):
//   - InversionMap {3,2,1,0} (A<->T, C<->G) is unchanged.
//   - Extract/Speak/Counter return null when the entity has no
//     LanguageSignature component (same guard as the source).
//   - Counter inverts each nucleotide in [0..3] and passes any out-of-range
//     value through untouched; strength = Compatibility(counter sig,
//     incoming payload) — identical formula.
//   - Parse maps A/C/G/T (case-insensitive) to 0/1/2/3, anything else to -1;
//     empty/null input -> null.
//   - ExtractCodon returns a 3-nucleotide group, a shorter tail group at the
//     end of the sequence, or null when the index is out of range.
//
// The prior art's `Entity` (Animal.Core) is a component registry; this port
// takes the LanguageSignature directly so the module stays engine-free. The
// null-guard semantics are preserved: a null signature == no component.
namespace LastAnimal.Dna;

/// <summary>
/// DNA Language System (C4 port + C5 extension).
///
/// Three core operations (C4):
///   Extract — read an entity's DNA signature as a message
///   Speak   — broadcast or direct a DNA message
///   Counter — generate an inverted counter-signal to respond to incoming messages
/// Plus the C5 extension:
///   EcosystemAdaptation.ModelPlayerDna — learn/mutate counters the ecosystem
///   uses to strike back (feeds the C2 EcosystemAdapted signal).
/// </summary>
public class DnaLanguage
{
    /// <summary>
    /// Nucleotide inversion map for counter-language: A&lt;-&gt;T, C&lt;-&gt;G.
    /// </summary>
    private static readonly int[] InversionMap = { 3, 2, 1, 0 };

    /// <summary>
    /// The ONE id-seeded signature builder: deterministic per entity id, 6
    /// nucleotides in 0..3, Id carries the entity identity. Both readers of a
    /// creature's DNA — the kill-extraction path (CombatSystem.OnKill) and the
    /// interact/speak path (WorldDirector) — build signatures here, so the
    /// seeding has exactly one implementation.
    /// </summary>
    public static LanguageSignature SignatureForEntity(int entityId)
    {
        var rng = new System.Random(entityId);
        var nucleotides = new int[6];
        for (int i = 0; i < nucleotides.Length; i++)
            nucleotides[i] = rng.Next(4); // 0-3 (A,C,G,T)
        return new LanguageSignature(nucleotides, entityId);
    }

    /// <summary>
    /// Extract the DNA language signature from an entity as a message.
    /// Returns null if the entity has no LanguageSignature component.
    /// </summary>
    public DnaMessage? Extract(LanguageSignature? sig)
    {
        if (sig == null) return null;

        return new DnaMessage(sig.Id, (int[])sig.Nucleotides.Clone());
    }

    /// <summary>
    /// Speak — an entity broadcasts its DNA signature as a message.
    /// Returns null if the entity has no LanguageSignature component.
    /// </summary>
    public DnaMessage? Speak(LanguageSignature? sig, int? targetEntityId = null)
    {
        if (sig == null) return null;

        return new DnaMessage(sig.Id, (int[])sig.Nucleotides.Clone(), targetEntityId);
    }

    /// <summary>
    /// Counter — generate an inverted counter-signal against an incoming message.
    /// The counter inverts each nucleotide (A&lt;-&gt;T, C&lt;-&gt;G) to create a response.
    /// Counter strength depends on the compatibility between the counter entity's
    /// signature and the incoming message.
    /// </summary>
    public DnaMessage? Counter(LanguageSignature? sig, DnaMessage incoming)
    {
        if (sig == null) return null;

        // Generate inverted payload
        var counterPayload = new int[incoming.Payload.Length];
        for (int i = 0; i < incoming.Payload.Length; i++)
        {
            int n = incoming.Payload[i];
            counterPayload[i] = (n >= 0 && n <= 3) ? InversionMap[n] : n;
        }

        // Calculate counter strength based on compatibility
        float strength = CalculateCounterStrength(sig, incoming);

        var counterMessage = new DnaMessage(sig.Id, counterPayload, incoming.SourceEntityId);
        counterMessage.CounterStrength = strength;

        return counterMessage;
    }

    /// <summary>
    /// Calculate the strength of a counter response based on signature compatibility.
    /// Higher compatibility = stronger counter.
    /// </summary>
    private static float CalculateCounterStrength(LanguageSignature sig, DnaMessage incoming)
    {
        // Create a temporary signature from the incoming payload for compatibility check
        var incomingSig = new LanguageSignature(incoming.Payload);
        return sig.Compatibility(incomingSig);
    }

    /// <summary>
    /// Parse a base-letter string back into nucleotide values.
    /// </summary>
    public static int[]? Parse(string baseString)
    {
        if (string.IsNullOrEmpty(baseString)) return null;
        var nucleotides = new int[baseString.Length];
        for (int i = 0; i < baseString.Length; i++)
        {
            nucleotides[i] = baseString[i] switch
            {
                'A' or 'a' => 0,
                'C' or 'c' => 1,
                'G' or 'g' => 2,
                'T' or 't' => 3,
                _ => -1
            };
        }
        return nucleotides;
    }

    /// <summary>
    /// Generate a codon (3-nucleotide group) from a signature at a given index.
    /// Returns null if the index is out of range.
    /// </summary>
    public static int[]? ExtractCodon(LanguageSignature sig, int codonIndex)
    {
        int startIndex = codonIndex * 3;
        if (startIndex >= sig.Nucleotides.Length) return null;

        int length = Math.Min(3, sig.Nucleotides.Length - startIndex);
        var codon = new int[length];
        Array.Copy(sig.Nucleotides, startIndex, codon, 0, length);
        return codon;
    }
}
