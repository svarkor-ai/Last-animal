using System;
using LastAnimal.Empathy;
using LastAnimal.Npc;
using Xunit;

// Last Animal — M04 empathy-book test suite (MC 890.11, dobbie, 2026-09-06).
//
// C9 unit tests (PHASE0.md Phase 8 DoD): EmpathyBook.Query and
// EmpathyBook.RouteResolution must PASS, including the Forgive-vs-PermanentBreak
// branch and a deliberately-failing case (the latter lives in
// EmpathyHarnessSelfTest.cs — this file holds only the real, green tests).
//
// Design under test:
//   - Query delegates the hidden emotional state to M03's C8 (EmotionalDepth),
//     then wraps it in a reader-facing BookEntry (Summary + Hidden Hint). Null
//     guard mirrors C8: a null companion -> null entry.
//   - RouteResolution: null entry -> PermanentBreak; a companion already in the
//     "Betrayed" hidden state -> PermanentBreak (bond severed; cannot be mended);
//     otherwise empathy >= ForgiveThreshold (0.5) -> Forgive, else PermanentBreak.
//
// The suite is enumerated (each test a named [Fact]), not tallied — consistent
// with the M02/M03 convention (count is observed from the runner output).
namespace LastAnimal.Tests.Empathy;

public class EmpathyBookQueryTests
{
    // --- Query: hidden state read-through (C8 reuse) -----------------------

    [Fact]
    public void Query_ReturnsNull_WhenNoCompanion()
    {
        var entry = EmpathyBook.Query(null);
        Assert.Null(entry);
    }

    [Fact]
    public void Query_ReadsHiddenState_Content_WhenLoyaltyHigh()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 85 };
        var entry = EmpathyBook.Query(comp);
        Assert.NotNull(entry);
        Assert.Equal("Content", entry!.EmotionalState);
        Assert.Equal(85, entry.Loyalty);
        Assert.Equal(1.0f, entry.Depth);
    }

    [Fact]
    public void Query_ReadsHiddenState_Neutral_WhenLoyaltyMid()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 50 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("Neutral", entry!.EmotionalState);
    }

    [Fact]
    public void Query_ReadsHiddenState_Anxious_WhenLoyaltyLow()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 30 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("Anxious", entry!.EmotionalState);
    }

    [Fact]
    public void Query_ReadsHiddenState_Betrayed_WhenLoyaltyVeryLow()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 5 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("Betrayed", entry!.EmotionalState);
    }

    // --- Query: summary + hidden hint --------------------------------------

    [Fact]
    public void Query_Summary_IsNotEmptyForEveryState()
    {
        foreach (int loyalty in new[] { 5, 30, 50, 85 })
        {
            var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = loyalty };
            var entry = EmpathyBook.Query(comp);
            Assert.False(string.IsNullOrWhiteSpace(entry!.Summary), $"summary empty at loyalty {loyalty}");
            Assert.False(string.IsNullOrWhiteSpace(entry!.Hint), $"hint empty at loyalty {loyalty}");
        }
    }

    [Fact]
    public void Query_BetrayedState_SurfacesSeveredHint()
    {
        var comp = new CompanionComponent { CompanionEntityId = 9, Loyalty = 0 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("Betrayed", entry!.EmotionalState);
        Assert.Contains("severed", entry.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_AnxiousState_SurfacesPayWageHint()
    {
        var comp = new CompanionComponent { CompanionEntityId = 9, Loyalty = 30 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("Anxious", entry!.EmotionalState);
        Assert.Contains("wage", entry.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_CarriesCompanionId()
    {
        // Per the M03 convention (C7), `CompanionComponent.Id` IS the companion's
        // own identity (the C2 CompanionId / BetrayerEntityId); `CompanionEntityId`
        // names the partner it is bonded to. The book reads the companion's state,
        // so the entry's CompanionId comes from `Id`.
        var comp = new CompanionComponent { Id = 12345, CompanionEntityId = 99, Loyalty = 60 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("12345", entry!.CompanionId);
    }
}

public class EmpathyBookRouteTests
{
    // --- RouteResolution: the Forgive-vs-PermanentBreak branch --------------

    [Fact]
    public void Route_Forgives_WhenEmpathyHighEnough()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 60 }; // Neutral
        var entry = EmpathyBook.Query(comp);
        Assert.Equal(Resolution.Forgive, EmpathyBook.RouteResolution(entry, 0.8f));
    }

    [Fact]
    public void Route_PermanentBreak_WhenEmpathyTooLow()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 60 }; // Neutral
        var entry = EmpathyBook.Query(comp);
        Assert.Equal(Resolution.PermanentBreak, EmpathyBook.RouteResolution(entry, 0.2f));
    }

    [Fact]
    public void Route_ForgiveBoundary_AtThreshold_Half()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 60 };
        var entry = EmpathyBook.Query(comp);
        // empathy == ForgiveThreshold (0.5) must route to Forgive (>=).
        Assert.Equal(Resolution.Forgive, EmpathyBook.RouteResolution(entry, 0.5f));
    }

    [Fact]
    public void Route_JustBelowThreshold_PermanentBreak()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 60 };
        var entry = EmpathyBook.Query(comp);
        Assert.Equal(Resolution.PermanentBreak, EmpathyBook.RouteResolution(entry, 0.49f));
    }

    // --- RouteResolution: the Betrayed-hidden-state override ---------------

    [Fact]
    public void Route_BetrayedState_IsPermanentBreak_EvenWithHighEmpathy()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 0 }; // Betrayed
        var entry = EmpathyBook.Query(comp);
        Assert.Equal("Betrayed", entry!.EmotionalState);
        // Pillar "forge betrayed": even high empathy cannot mend a severed bond.
        Assert.Equal(Resolution.PermanentBreak, EmpathyBook.RouteResolution(entry, 1.0f));
    }

    [Fact]
    public void Route_AnxiousState_WithHighEmpathy_Forgives()
    {
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 30 }; // Anxious
        var entry = EmpathyBook.Query(comp);
        Assert.Equal(Resolution.Forgive, EmpathyBook.RouteResolution(entry, 1.0f));
    }

    // --- RouteResolution: null guard ---------------------------------------

    [Fact]
    public void Route_NullEntry_IsPermanentBreak()
    {
        Assert.Equal(Resolution.PermanentBreak, EmpathyBook.RouteResolution(null, 1.0f));
    }
}
