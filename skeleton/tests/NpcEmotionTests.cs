using System;
using LastAnimal.Npc;
using Xunit;

// Last Animal — M03 npc-emotion test suite (MC 890.2, dobbie, 2026-09-03).
//
// C6 port tests: SalarySystem.CalculateSalary/PaySalary/SkipSalary semantics
// must match the prior art (/srv/workspace/animal/src/Animal.Gameplay/SalarySystem.cs).
// C7 port tests: BetrayalSystem.CheckBetrayal/ExecuteBetrayal semantics must
// match the prior art (/srv/workspace/animal/src/Animal.Gameplay/BetrayalSystem.cs).
// C8 extension tests: EmotionalDepth.ReadHiddenState must derive the correct
// hidden state from loyalty.
//
// The suite is enumerated (each test is a named [Fact]), not tallied — the
// DoD requires the suite count to be enumerated, not just a pass/fail count.
namespace LastAnimal.Tests;

public class SalarySystemTests
{
    // --- C6: CalculateSalary -----------------------------------------------

    [Fact]
    public void CalculateSalary_ReturnsBase_WhenLoyaltyZero()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 0 };

        int salary = sys.CalculateSalary(comp);

        Assert.Equal(1, salary); // BaseSalary = 1, 0/10 = 0
    }

    [Fact]
    public void CalculateSalary_ScalesWithLoyalty()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 50 };

        int salary = sys.CalculateSalary(comp);

        Assert.Equal(6, salary); // 1 + 50/10 = 1 + 5 = 6
    }

    [Fact]
    public void CalculateSalary_ReturnsZero_WhenNoCompanion()
    {
        var sys = new SalarySystem();

        int salary = sys.CalculateSalary(null);

        Assert.Equal(0, salary);
    }

    [Fact]
    public void CalculateSalary_UsesIntegerDivision()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 55 };

        int salary = sys.CalculateSalary(comp);

        Assert.Equal(6, salary); // 1 + 55/10 = 1 + 5 = 6 (integer division)
    }

    // --- C6: PaySalary -----------------------------------------------------

    [Fact]
    public void PaySalary_IncreasesLoyalty_ByPayBonus()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 50 };

        bool ok = sys.PaySalary(comp);

        Assert.True(ok);
        Assert.Equal(55, comp.Loyalty); // 50 + 5 = 55
    }

    [Fact]
    public void PaySalary_ClampsLoyalty_At100()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 98 };

        bool ok = sys.PaySalary(comp);

        Assert.True(ok);
        Assert.Equal(100, comp.Loyalty); // 98 + 5 = 103, clamped to 100
    }

    [Fact]
    public void PaySalary_ReturnsFalse_WhenNoCompanion()
    {
        var sys = new SalarySystem();

        bool ok = sys.PaySalary(null);

        Assert.False(ok);
    }

    // --- C6: SkipSalary ----------------------------------------------------

    [Fact]
    public void SkipSalary_DecreasesLoyalty_BySkipPenalty()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 50 };

        bool ok = sys.SkipSalary(comp);

        Assert.True(ok);
        Assert.Equal(47, comp.Loyalty); // 50 - 3 = 47
    }

    [Fact]
    public void SkipSalary_ClampsLoyalty_AtZero()
    {
        var sys = new SalarySystem();
        var comp = new CompanionComponent { Loyalty = 2 };

        bool ok = sys.SkipSalary(comp);

        Assert.True(ok);
        Assert.Equal(0, comp.Loyalty); // 2 - 3 = -1, clamped to 0
    }

    [Fact]
    public void SkipSalary_ReturnsFalse_WhenNoCompanion()
    {
        var sys = new SalarySystem();

        bool ok = sys.SkipSalary(null);

        Assert.False(ok);
    }
}

public class BetrayalSystemTests
{
    // --- C7: CheckBetrayal -------------------------------------------------

    [Fact]
    public void CheckBetrayal_ReturnsTrue_WhenLoyaltyZeroAndHasCompanion()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = 5, Loyalty = 0 };

        bool betrayal = sys.CheckBetrayal(comp);

        Assert.True(betrayal);
    }

    [Fact]
    public void CheckBetrayal_ReturnsFalse_WhenLoyaltyPositive()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = 5, Loyalty = 10 };

        bool betrayal = sys.CheckBetrayal(comp);

        Assert.False(betrayal);
    }

    [Fact]
    public void CheckBetrayal_ReturnsFalse_WhenNoCompanion()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = -1, Loyalty = 0 };

        bool betrayal = sys.CheckBetrayal(comp);

        Assert.False(betrayal);
    }

    [Fact]
    public void CheckBetrayal_ReturnsFalse_WhenNoComponent()
    {
        var sys = new BetrayalSystem();

        bool betrayal = sys.CheckBetrayal(null);

        Assert.False(betrayal);
    }

    // --- C7: ExecuteBetrayal -----------------------------------------------

    [Fact]
    public void ExecuteBetrayal_BreaksCompanionBond()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = 5, Loyalty = 0, Id = 1 };

        var result = sys.ExecuteBetrayal(comp);

        Assert.NotNull(result);
        Assert.Equal(-1, comp.CompanionEntityId); // bond broken
        Assert.Equal(0, comp.Loyalty); // loyalty zeroed
    }

    [Fact]
    public void ExecuteBetrayal_ReturnsCorrectDamage()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = 5, Loyalty = 0, Id = 1, ProximityRadius = 5.0f };

        var result = sys.ExecuteBetrayal(comp);

        Assert.NotNull(result);
        Assert.Equal(15, result!.DamageDealt); // 10 + (int)5.0 = 15
    }

    [Fact]
    public void ExecuteBetrayal_ReturnsCorrectBetrayerAndTarget()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = 7, Loyalty = 0, Id = 3 };

        var result = sys.ExecuteBetrayal(comp);

        Assert.NotNull(result);
        Assert.Equal(3, result!.BetrayerEntityId); // the companion's entity id
        Assert.Equal(7, result.BetrayedTargetId); // the owner's entity id
    }

    [Fact]
    public void ExecuteBetrayal_ReturnsNull_WhenNoCompanion()
    {
        var sys = new BetrayalSystem();
        var comp = new CompanionComponent { CompanionEntityId = -1, Loyalty = 0 };

        var result = sys.ExecuteBetrayal(comp);

        Assert.Null(result);
    }

    [Fact]
    public void ExecuteBetrayal_ReturnsNull_WhenNoComponent()
    {
        var sys = new BetrayalSystem();

        var result = sys.ExecuteBetrayal(null);

        Assert.Null(result);
    }
}

public class EmotionalDepthTests
{
    // --- C8: ReadHiddenState -----------------------------------------------

    [Fact]
    public void ReadHiddenState_ReturnsContent_WhenLoyaltyHigh()
    {
        var comp = new CompanionComponent { Loyalty = 80 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Content", state!.State);
        Assert.Equal(1.0f, state.Depth, 3);
        Assert.Equal(80, state.Loyalty);
    }

    [Fact]
    public void ReadHiddenState_ReturnsNeutral_WhenLoyaltyMid()
    {
        var comp = new CompanionComponent { Loyalty = 50 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Neutral", state!.State);
        Assert.Equal(0.5f, state.Depth, 3);
    }

    [Fact]
    public void ReadHiddenState_ReturnsAnxious_WhenLoyaltyLow()
    {
        var comp = new CompanionComponent { Loyalty = 30 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Anxious", state!.State);
        Assert.Equal(0.3f, state.Depth, 3);
    }

    [Fact]
    public void ReadHiddenState_ReturnsBetrayed_WhenLoyaltyVeryLow()
    {
        var comp = new CompanionComponent { Loyalty = 10 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Betrayed", state!.State);
        Assert.Equal(0.0f, state.Depth, 3);
    }

    [Fact]
    public void ReadHiddenState_ReturnsNull_WhenNoCompanion()
    {
        var state = EmotionalDepth.ReadHiddenState(null);

        Assert.Null(state);
    }

    [Fact]
    public void ReadHiddenState_BoundaryAt70_ReturnsContent()
    {
        var comp = new CompanionComponent { Loyalty = 70 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Content", state!.State);
    }

    [Fact]
    public void ReadHiddenState_BoundaryAt40_ReturnsNeutral()
    {
        var comp = new CompanionComponent { Loyalty = 40 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Neutral", state!.State);
    }

    [Fact]
    public void ReadHiddenState_BoundaryAt20_ReturnsAnxious()
    {
        var comp = new CompanionComponent { Loyalty = 20 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Anxious", state!.State);
    }

    [Fact]
    public void ReadHiddenState_BoundaryAt19_ReturnsBetrayed()
    {
        var comp = new CompanionComponent { Loyalty = 19 };

        var state = EmotionalDepth.ReadHiddenState(comp);

        Assert.NotNull(state);
        Assert.Equal("Betrayed", state!.State);
    }
}
