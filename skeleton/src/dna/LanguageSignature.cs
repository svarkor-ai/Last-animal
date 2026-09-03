using System;

// Last Animal — M02 dna-language-combat (MC 890.2, dobbie, 2026-09-03).
//
// Port of /srv/workspace/animal/src/Animal.Entities/Components/LanguageSignature.cs.
// Pure data: no Godot types, no window — runs headless under `dotnet test` (I3).
//
// Port notes (semantics preserved 1:1 from the prior art):
//   - Nucleotides is an int[] of values 0-3 (A,C,G,T); null input becomes an
//     empty array (same guard as the source).
//   - SpeciesHash is the concatenated nucleotide digits (e.g. "0123") — the
//     source's ComputeHash is reproduced verbatim.
//   - Compatibility(other) = matches / minLen over the overlapping prefix;
//     0f when either side is empty or null.
//   - ToBase maps 0/1/2/3 -> A/C/G/T, anything else -> '?'.
//   - Equals/GetHashCode are keyed on SpeciesHash (same as the source).
//   - The source's `Component` base (Animal.Core.IComponent) is dropped: this
//     port is engine-free and the component-registry seam is not part of the
//     C4 contract surface.
//   - `Id` is added (not in the prior art) so the C4 Extract/Speak/Counter
//     port can carry the entity identity the source read from Entity.Id.
namespace LastAnimal.Dna;

/// <summary>
/// Language signature component for the "DNA Language" mechanic.
/// Each animal has a unique DNA signature that forms a language pattern.
/// The signature is an array of integer "nucleotides" that define the animal's
/// communication ability with other animals.
/// </summary>
public class LanguageSignature
{
    /// <summary>
    /// The DNA signature - a sequence of nucleotide values (0-3).
    /// 0=A, 1=C, 2=G, 3=T (biological bases).
    /// </summary>
    public int[] Nucleotides { get; }

    /// <summary>
    /// The species identifier derived from the signature hash.
    /// </summary>
    public string SpeciesHash { get; }

    /// <summary>Entity identity carried alongside the signature (C4 port seam).</summary>
    public int Id { get; }

    public int Length => Nucleotides.Length;

    public LanguageSignature(int[] nucleotides, int id = 0)
    {
        Nucleotides = nucleotides ?? Array.Empty<int>();
        SpeciesHash = ComputeHash(Nucleotides);
        Id = id;
    }

    /// <summary>
    /// Creates a random DNA signature of the given length.
    /// </summary>
    public static LanguageSignature Generate(int length, System.Random rng)
    {
        var nucleotides = new int[length];
        for (int i = 0; i < length; i++)
        {
            nucleotides[i] = rng.Next(4); // 0-3 (A,C,G,T)
        }
        return new LanguageSignature(nucleotides);
    }

    /// <summary>
    /// Computes a compatibility score with another signature.
    /// Score ranges from 0 (no match) to 1 (identical).
    /// </summary>
    public float Compatibility(LanguageSignature other)
    {
        if (other == null) return 0f;
        var minLen = Math.Min(Nucleotides.Length, other.Nucleotides.Length);
        if (minLen == 0) return 0f;

        int matches = 0;
        for (int i = 0; i < minLen; i++)
        {
            if (Nucleotides[i] == other.Nucleotides[i]) matches++;
        }
        return (float)matches / minLen;
    }

    /// <summary>
    /// Returns the nucleotide as a base letter: A, C, G, or T.
    /// </summary>
    public static char ToBase(int nucleotide) => nucleotide switch
    {
        0 => 'A',
        1 => 'C',
        2 => 'G',
        3 => 'T',
        _ => '?'
    };

    /// <summary>
    /// Returns the signature as a string of base letters.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var n in Nucleotides)
            sb.Append(ToBase(n));
        return sb.ToString();
    }

    private static string ComputeHash(int[] nucleotides)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var n in nucleotides)
            sb.Append(n);
        return sb.ToString();
    }

    public override bool Equals(object? obj) =>
        obj is LanguageSignature other && SpeciesHash == other.SpeciesHash;

    public override int GetHashCode() => SpeciesHash.GetHashCode();
}
