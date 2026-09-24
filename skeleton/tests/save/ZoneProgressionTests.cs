using Xunit;
using LastAnimal.Save;

// Last Animal — MC 1348 N2 regression tests: progression counts the FIRST
// entry into each DISTINCT zone, not zone changes. The back-and-forth travel
// the T key enables (meadow->canyon->meadow->canyon) is 2 chapters, not 4.
// ZoneProgression is the pure counter SaveLoadController delegates to, so
// these run headless (I3).
namespace LastAnimal.Tests.Save;

public class ZoneProgressionTests
{
    [Fact]
    public void BackAndForthTravel_CountsDistinctZonesOnly()
    {
        // The audit's N2 scenario with two non-boot zones: 4 zone CHANGES
        // must be 2 chapters (canyon + ruins), not 4.
        var p = new ZoneProgression("meadow");
        p.OnZoneEntered("canyon");
        p.OnZoneEntered("ruins");
        p.OnZoneEntered("canyon");
        p.OnZoneEntered("ruins");
        Assert.Equal(2, p.Chapters);
    }

    [Fact]
    public void BackAndForthWithBootZone_BootZoneNeverCounts()
    {
        // The boot zone is seeded as visited (documented contract): its boot
        // entry is not progress, and meadow->canyon->meadow->canyon is one
        // chapter (canyon), not the 4 zone changes.
        var p = new ZoneProgression("meadow");
        p.OnZoneEntered("meadow");
        p.OnZoneEntered("canyon");
        p.OnZoneEntered("meadow");
        p.OnZoneEntered("canyon");
        Assert.Equal(1, p.Chapters);
    }

    [Fact]
    public void ReEnteringSameZone_DoesNotCount()
    {
        var p = new ZoneProgression("meadow");
        p.OnZoneEntered("canyon");
        p.OnZoneEntered("canyon");
        p.OnZoneEntered("canyon");
        Assert.Equal(1, p.Chapters);
    }

    [Fact]
    public void SeededDefaultZone_BootEntryIsNotProgress()
    {
        var p = new ZoneProgression("meadow");
        p.OnZoneEntered("meadow");
        Assert.Equal(0, p.Chapters);
    }

    [Fact]
    public void LoadSeeding_MarksSavedZoneVisited_AndRestoresCount()
    {
        var p = new ZoneProgression("meadow");
        p.OnZoneEntered("canyon");
        p.SeedForLoad("ruins", 3);
        Assert.Equal(3, p.Chapters);
        p.OnZoneEntered("ruins");    // the load's re-entry must not count
        Assert.Equal(3, p.Chapters);
        p.OnZoneEntered("canyon");
        Assert.Equal(4, p.Chapters);
    }
}
