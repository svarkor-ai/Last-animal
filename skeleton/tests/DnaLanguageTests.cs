using System;
using System.Collections.Generic;
using LastAnimal.Dna;
using Xunit;

// Last Animal — M02 dna-language-combat test suite (MC 890.2, dobbie, 2026-09-03).
//
// C4 port tests: Extract/Speak/Counter/Parse/ExtractCodon semantics must match
// the prior art (/srv/workspace/animal/src/Animal.Gameplay/DnaLanguage.cs).
// C5 extension tests: EcosystemAdaptation.ModelPlayerDna must produce the
// expected CounterProfile from a spoken-DNA history.
//
// The suite is enumerated (each test is a named [Fact]), not tallied — the
// DoD requires the suite count to be enumerated, not just a pass/fail count.
namespace LastAnimal.Tests;

public class DnaLanguageTests
{
    // --- C4: Extract -------------------------------------------------------

    [Fact]
    public void Extract_ReturnsMessage_WhenSignaturePresent()
    {
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 0, 1, 2, 3 }, id: 42);

        var msg = lang.Extract(sig);

        Assert.NotNull(msg);
        Assert.Equal(42, msg!.SourceEntityId);
        Assert.Equal(new int[] { 0, 1, 2, 3 }, msg.Payload);
        Assert.Null(msg.TargetEntityId);
    }

    [Fact]
    public void Extract_ReturnsNull_WhenNoSignature()
    {
        var lang = new DnaLanguage();

        var msg = lang.Extract(null);

        Assert.Null(msg);
    }

    [Fact]
    public void Extract_ReturnsClone_NotReference()
    {
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 0, 1, 2 }, id: 1);

        var msg = lang.Extract(sig);
        msg!.Payload[0] = 99; // mutate the message's payload

        // The original signature must be unchanged.
        Assert.Equal(0, sig.Nucleotides[0]);
    }

    // --- C4: Speak ---------------------------------------------------------

    [Fact]
    public void Speak_ReturnsMessage_WhenSignaturePresent()
    {
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 3, 2, 1, 0 }, id: 7);

        var msg = lang.Speak(sig);

        Assert.NotNull(msg);
        Assert.Equal(7, msg!.SourceEntityId);
        Assert.Equal(new int[] { 3, 2, 1, 0 }, msg.Payload);
        Assert.Null(msg.TargetEntityId);
    }

    [Fact]
    public void Speak_ReturnsMessage_WithTarget_WhenTargetProvided()
    {
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 0, 0, 0 }, id: 3);

        var msg = lang.Speak(sig, targetEntityId: 99);

        Assert.NotNull(msg);
        Assert.Equal(99, msg!.TargetEntityId);
    }

    [Fact]
    public void Speak_ReturnsNull_WhenNoSignature()
    {
        var lang = new DnaLanguage();

        var msg = lang.Speak(null);

        Assert.Null(msg);
    }

    // --- C4: Counter -------------------------------------------------------

    [Fact]
    public void Counter_InvertsNucleotides()
    {
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 0, 1, 2, 3 }, id: 1);
        var incoming = new DnaMessage(2, new int[] { 0, 1, 2, 3 });

        var counter = lang.Counter(sig, incoming);

        Assert.NotNull(counter);
        // InversionMap = {3,2,1,0}: 0->3, 1->2, 2->1, 3->0
        Assert.Equal(new int[] { 3, 2, 1, 0 }, counter!.Payload);
        Assert.Equal(1, counter.SourceEntityId); // the counter entity's id
        Assert.Equal(2, counter.TargetEntityId); // targets the original speaker
    }

    [Fact]
    public void Counter_PassesOutOfRangeValuesThrough()
    {
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 0 }, id: 1);
        var incoming = new DnaMessage(2, new int[] { -1, 4, 5 });

        var counter = lang.Counter(sig, incoming);

        Assert.NotNull(counter);
        // Out-of-range values pass through untouched.
        Assert.Equal(new int[] { -1, 4, 5 }, counter!.Payload);
    }

    [Fact]
    public void Counter_CalculatesStrength_FromCompatibility()
    {
        var lang = new DnaLanguage();
        // Counter sig = [0,1,2,3], incoming payload = [0,1,2,3] -> compatibility 1.0
        var sig = new LanguageSignature(new int[] { 0, 1, 2, 3 }, id: 1);
        var incoming = new DnaMessage(2, new int[] { 0, 1, 2, 3 });

        var counter = lang.Counter(sig, incoming);

        Assert.NotNull(counter);
        Assert.Equal(1.0f, counter!.CounterStrength, 3);
    }

    [Fact]
    public void Counter_ReturnsNull_WhenNoSignature()
    {
        var lang = new DnaLanguage();
        var incoming = new DnaMessage(2, new int[] { 0, 1, 2 });

        var counter = lang.Counter(null, incoming);

        Assert.Null(counter);
    }

    // --- C4: Parse ---------------------------------------------------------

    [Fact]
    public void Parse_MapsBasesToNucleotides()
    {
        var result = DnaLanguage.Parse("ACGT");

        Assert.NotNull(result);
        Assert.Equal(new int[] { 0, 1, 2, 3 }, result);
    }

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        var result = DnaLanguage.Parse("acgt");

        Assert.NotNull(result);
        Assert.Equal(new int[] { 0, 1, 2, 3 }, result);
    }

    [Fact]
    public void Parse_ReturnsMinusOne_ForUnknownChars()
    {
        var result = DnaLanguage.Parse("AXC");

        Assert.NotNull(result);
        Assert.Equal(new int[] { 0, -1, 1 }, result);
    }

    [Fact]
    public void Parse_ReturnsNull_ForEmptyString()
    {
        var result = DnaLanguage.Parse("");

        Assert.Null(result);
    }

    [Fact]
    public void Parse_ReturnsNull_ForNullString()
    {
        var result = DnaLanguage.Parse(null!);

        Assert.Null(result);
    }

    // --- C4: ExtractCodon --------------------------------------------------

    [Fact]
    public void ExtractCodon_ReturnsFirstCodon()
    {
        var sig = new LanguageSignature(new int[] { 0, 1, 2, 3, 4, 5 });

        var codon = DnaLanguage.ExtractCodon(sig, 0);

        Assert.NotNull(codon);
        Assert.Equal(new int[] { 0, 1, 2 }, codon);
    }

    [Fact]
    public void ExtractCodon_ReturnsSecondCodon()
    {
        var sig = new LanguageSignature(new int[] { 0, 1, 2, 3, 4, 5 });

        var codon = DnaLanguage.ExtractCodon(sig, 1);

        Assert.NotNull(codon);
        Assert.Equal(new int[] { 3, 4, 5 }, codon);
    }

    [Fact]
    public void ExtractCodon_ReturnsShortTail_WhenNotEnoughNucleotides()
    {
        var sig = new LanguageSignature(new int[] { 0, 1, 2, 3, 4 });

        var codon = DnaLanguage.ExtractCodon(sig, 1);

        Assert.NotNull(codon);
        Assert.Equal(new int[] { 3, 4 }, codon);
    }

    [Fact]
    public void ExtractCodon_ReturnsNull_WhenIndexOutOfRange()
    {
        var sig = new LanguageSignature(new int[] { 0, 1, 2 });

        var codon = DnaLanguage.ExtractCodon(sig, 1);

        Assert.Null(codon);
    }

    // --- C5: EcosystemAdaptation -------------------------------------------

    [Fact]
    public void ModelPlayerDna_ReturnsEmptyProfile_WhenNoSignatures()
    {
        var profile = EcosystemAdaptation.ModelPlayerDna(null);

        Assert.False(profile.HasAdapted);
        Assert.Equal(0, profile.ObservedCount);
        Assert.Empty(profile.Counters);
    }

    [Fact]
    public void ModelPlayerDna_ReturnsEmptyProfile_WhenEmptyList()
    {
        var profile = EcosystemAdaptation.ModelPlayerDna(new List<LanguageSignature>());

        Assert.False(profile.HasAdapted);
        Assert.Equal(0, profile.ObservedCount);
    }

    [Fact]
    public void ModelPlayerDna_InvertsMostCommonNucleotide()
    {
        // Two signatures, both with nucleotide 0 (A) at position 0.
        // The most common is 0, inversion is 3 (T).
        var sigs = new List<LanguageSignature>
        {
            new(new int[] { 0, 1 }, id: 1),
            new(new int[] { 0, 2 }, id: 2),
        };

        var profile = EcosystemAdaptation.ModelPlayerDna(sigs);

        Assert.True(profile.HasAdapted);
        Assert.Equal(2, profile.ObservedCount);
        Assert.Equal(3, profile.Counters[0]); // inversion of 0
        Assert.Equal(2, profile.Coverage[0]); // both signatures reached position 0
    }

    [Fact]
    public void ModelPlayerDna_HandlesVariableLengths()
    {
        // One signature of length 3, one of length 1.
        var sigs = new List<LanguageSignature>
        {
            new(new int[] { 0, 1, 2 }, id: 1),
            new(new int[] { 3 }, id: 2),
        };

        var profile = EcosystemAdaptation.ModelPlayerDna(sigs);

        Assert.Equal(3, profile.Counters.Length);
        Assert.Equal(2, profile.Coverage[0]); // both reached position 0
        Assert.Equal(1, profile.Coverage[1]); // only the first reached position 1
        Assert.Equal(1, profile.Coverage[2]); // only the first reached position 2
    }

    [Fact]
    public void ModelPlayerDna_BreaksTies_ByLowerNucleotide()
    {
        // Position 0: one 0 (A) and one 1 (C) -> tie, lower wins (0).
        var sigs = new List<LanguageSignature>
        {
            new(new int[] { 0 }, id: 1),
            new(new int[] { 1 }, id: 2),
        };

        var profile = EcosystemAdaptation.ModelPlayerDna(sigs);

        // Most common is 0 (tie broken by lower value), inversion is 3.
        Assert.Equal(3, profile.Counters[0]);
    }

    // --- MC 1344: the ONE id-seeded signature builder -----------------------

    [Fact]
    public void SignatureForEntity_IsDeterministic_PerEntityId()
    {
        var a = DnaLanguage.SignatureForEntity(7);
        var b = DnaLanguage.SignatureForEntity(7);
        var c = DnaLanguage.SignatureForEntity(8);

        Assert.Equal(a.Nucleotides, b.Nucleotides);
        Assert.Equal(a.Id, b.Id);
        Assert.NotEqual(a.Nucleotides, c.Nucleotides);
        Assert.Equal(7, a.Id);
        Assert.Equal(8, c.Id);
    }

    [Fact]
    public void SignatureForEntity_SixNucleotides_InRange()
    {
        var sig = DnaLanguage.SignatureForEntity(2003);

        Assert.Equal(6, sig.Nucleotides.Length);
        foreach (int n in sig.Nucleotides)
            Assert.InRange(n, 0, 3);
    }

    [Fact]
    public void SignatureForEntity_MatchesTheOnKillSeeding()
    {
        // The kill-extraction path historically seeded System.Random(entityId)
        // and drew 6 nucleotides; the shared builder must reproduce exactly
        // that so extraction and speak agree on a creature's DNA.
        var rng = new System.Random(2004);
        var expected = new int[6];
        for (int i = 0; i < expected.Length; i++) expected[i] = rng.Next(4);

        var sig = DnaLanguage.SignatureForEntity(2004);

        Assert.Equal(expected, sig.Nucleotides);
    }
}
