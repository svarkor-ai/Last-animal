using System;
using LastAnimal.Empathy;
using LastAnimal.Npc;
using Xunit;

// Last Animal — M04 empathy-book harness self-test (MC 890.11, dobbie, 2026-09-06).
//
// The Phase-8 DoD requires a deliberately-failing case (two-sided calibration:
// prove the harness can fail). This test asserts a KNOWN-WRONG value — it is
// expected to FAIL (go red) when the suite runs. The gate script
// (ci/empathy_book_test.sh) runs the suite TWICE: once with this file present,
// expecting exactly one failure, then once without it, expecting zero failures.
namespace LastAnimal.Tests.Empathy;

public class EmpathyHarnessSelfTest
{
    [Fact]
    public void DeliberatelyBroken_ForgiveAtZeroEmpathy_IsWrong()
    {
        // This test asserts the WRONG routing: it claims that zero empathy and a
        // Neutral companion (loyalty 60) still forgives. Correct: empathy 0.0 < 0.5
        // -> PermanentBreak. This is DELIBERATELY BROKEN — it must go red.
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 60 };
        var entry = EmpathyBook.Query(comp);
        // WRONG: claims zero empathy forgives. Correct is PermanentBreak.
        Assert.Equal(Resolution.Forgive, EmpathyBook.RouteResolution(entry, 0.0f));
    }
}
