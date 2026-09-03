using System;
using LastAnimal.Dna;
using LastAnimal.Npc;
using Xunit;

// Last Animal — M02+M03 harness self-test (MC 890.2, dobbie, 2026-09-03).
//
// The DoD requires ONE deliberately-broken case that goes red — a two-sided
// calibration proving the test harness can actually fail. This test asserts
// a KNOWN-WRONG value: it is expected to FAIL (go red) when the suite runs.
// The gate script (ci/dna_npc_test.sh) runs the suite TWICE:
//   1. With this file present -> the suite must report exactly 1 failure
//      (this test) and all other tests green.
//   2. With this file excluded -> the suite must report 0 failures.
// If the harness cannot fail (i.e. this test somehow passes), the gate
// fails — the harness is broken.
namespace LastAnimal.Tests;

public class HarnessSelfTest
{
    [Fact]
    public void DeliberatelyBroken_CounterInversion_IsWrong()
    {
        // This test asserts a WRONG inversion: it claims that nucleotide 0
        // inverts to 0 (identity), when the correct inversion is 3 (A<->T).
        // This is DELIBERATELY BROKEN — it must go red.
        var lang = new DnaLanguage();
        var sig = new LanguageSignature(new int[] { 0 }, id: 1);
        var incoming = new DnaMessage(2, new int[] { 0 });

        var counter = lang.Counter(sig, incoming);

        Assert.NotNull(counter);
        // WRONG: claims 0 inverts to 0 (identity). Correct is 3.
        Assert.Equal(0, counter!.Payload[0]);
    }
}
