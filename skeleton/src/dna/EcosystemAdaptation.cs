using System;
using System.Collections.Generic;

// Last Animal — M02 dna-language-combat (MC 890.2, dobbie, 2026-09-03).
//
// C5 EXTENSION (not in the prior art): EcosystemAdaptation.
//
// Contract (PHASE0.md C5):
//   EcosystemAdaptation.ModelPlayerDna(playedSignatures[]) -> CounterProfile
//   — learn/mutate counters the ecosystem uses to strike back; feeds the C2
//     EcosystemAdapted signal.
//
// Design (pure logic, no Godot types, no window — I3):
//   The ecosystem watches the player's spoken DNA history and builds a
//   CounterProfile: for each nucleotide position the ecosystem has seen, it
//   records the most common nucleotide the player used there, and the
//   INVERSION of that nucleotide (the counter the ecosystem will fire back
//   with, using the same A<->T / C<->G inversion map as DnaLanguage.Counter).
//   The profile also carries the total number of spoken signatures observed
//   and the per-position coverage (how many signatures reached that position).
//
// This is the "learn/mutate" half of C5: the profile is a pure function of
// the spoken history, so it is deterministic and testable headless. The C2
// EcosystemAdapted signal is fired by the caller (the module that owns the
// EventBus) when the profile changes — this class does not touch the bus
// (I4: no cross-module refs).
namespace LastAnimal.Dna;

/// <summary>
/// A learned counter profile: the ecosystem's adaptation to the player's
/// spoken DNA history (C5).
/// </summary>
public class CounterProfile
{
    /// <summary>
    /// The counter nucleotide the ecosystem will fire back at each position.
    /// Index i = the inversion of the most common player nucleotide at
    /// position i across all observed signatures.
    /// </summary>
    public int[] Counters { get; }

    /// <summary>
    /// How many observed signatures reached each position (coverage).
    /// Index i = count of signatures with length > i.
    /// </summary>
    public int[] Coverage { get; }

    /// <summary>Total number of spoken signatures observed.</summary>
    public int ObservedCount { get; }

    public CounterProfile(int[] counters, int[] coverage, int observedCount)
    {
        Counters = counters ?? Array.Empty<int>();
        Coverage = coverage ?? Array.Empty<int>();
        ObservedCount = observedCount;
    }

    /// <summary>
    /// True when the profile has at least one observed signature.
    /// </summary>
    public bool HasAdapted => ObservedCount > 0;

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder("CounterProfile[");
        sb.Append(ObservedCount.ToString());
        sb.Append("obs");
        for (int i = 0; i < Counters.Length; i++)
            sb.Append(' ').Append(LanguageSignature.ToBase(Counters[i]));
        sb.Append(']');
        return sb.ToString();
    }
}

/// <summary>
/// Ecosystem adaptation (C5): learns counters from the player's spoken DNA
/// history. Pure function — no state, no bus, no window.
/// </summary>
public static class EcosystemAdaptation
{
    /// <summary>
    /// Nucleotide inversion map (same as DnaLanguage's): A&lt;-&gt;T, C&lt;-&gt;G.
    /// </summary>
    private static readonly int[] InversionMap = { 3, 2, 1, 0 };

    /// <summary>
    /// Model the player's spoken DNA history into a CounterProfile.
    ///
    /// For each nucleotide position, the profile records:
    ///   - the most common nucleotide the player used there (ties broken by
    ///     the lower nucleotide value, i.e. A &lt; C &lt; G &lt; T);
    ///   - the INVERSION of that nucleotide (the counter the ecosystem fires);
    ///   - the coverage (how many signatures reached that position).
    ///
    /// Returns an empty profile (ObservedCount=0, no counters) when the input
    /// is null or empty — the ecosystem has nothing to adapt to yet.
    /// </summary>
    public static CounterProfile ModelPlayerDna(IReadOnlyList<LanguageSignature>? playedSignatures)
    {
        if (playedSignatures == null || playedSignatures.Count == 0)
            return new CounterProfile(Array.Empty<int>(), Array.Empty<int>(), 0);

        // Find the maximum length across all signatures.
        int maxLen = 0;
        foreach (var sig in playedSignatures)
            if (sig.Nucleotides.Length > maxLen) maxLen = sig.Nucleotides.Length;

        if (maxLen == 0)
            return new CounterProfile(Array.Empty<int>(), Array.Empty<int>(), playedSignatures.Count);

        // Per-position nucleotide frequency (4 buckets: A,C,G,T).
        var freq = new int[maxLen, 4];
        var coverage = new int[maxLen];
        foreach (var sig in playedSignatures)
        {
            for (int i = 0; i < sig.Nucleotides.Length; i++)
            {
                int n = sig.Nucleotides[i];
                if (n >= 0 && n <= 3) freq[i, n]++;
                coverage[i]++;
            }
        }

        // For each position, pick the most common nucleotide (ties -> lower value),
        // then invert it to get the ecosystem's counter.
        var counters = new int[maxLen];
        for (int i = 0; i < maxLen; i++)
        {
            int best = 0;
            int bestCount = freq[i, 0];
            for (int n = 1; n < 4; n++)
            {
                if (freq[i, n] > bestCount)
                {
                    best = n;
                    bestCount = freq[i, n];
                }
            }
            counters[i] = InversionMap[best];
        }

        return new CounterProfile(counters, coverage, playedSignatures.Count);
    }
}
