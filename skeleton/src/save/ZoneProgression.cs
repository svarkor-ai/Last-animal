// Last Animal — zone-chapter progression counter (MC 1348 N2).
//
// Pure logic (I3, engine-free): chapters cleared = the FIRST entry into each
// DISTINCT zone. Extracted from SaveLoadController so the counting rule is
// headless-testable; SaveLoadController delegates to it and persists the
// count in GameState.Progression. The seed zone (the boot zone) is marked
// visited at construction so the boot-time entry is not counted as progress —
// same contract SaveLoadController's old _lastZone seed documented.
using System.Collections.Generic;

namespace LastAnimal.Save;

/// <summary>
/// Counts progression as first entries into distinct zones (MC 1348 N2:
/// the previous last-zone-only check counted zone CHANGES, so
/// meadow->canyon->meadow->canyon inflated to 4 chapters).
/// </summary>
public sealed class ZoneProgression
{
    private readonly HashSet<string> _visited = new();
    private int _chapters;

    /// <param name="seedZone">The boot zone; entering it first does not count.</param>
    public ZoneProgression(string seedZone)
    {
        _visited.Add(seedZone);
    }

    /// <summary>Chapters cleared so far (distinct zones first-entered).</summary>
    public int Chapters => _chapters;

    /// <summary>Tick a zone entry; only the FIRST entry of a distinct zone counts.</summary>
    public void OnZoneEntered(string zoneId)
    {
        if (_visited.Add(zoneId)) _chapters++;
    }

    /// <summary>
    /// Restore state on load: the saved chapter count comes back and the
    /// saved zone is marked visited so the load's re-entry does not count
    /// as new progress.
    /// </summary>
    public void SeedForLoad(string zoneId, int chapters)
    {
        _visited.Clear();
        _visited.Add(zoneId);
        _chapters = chapters;
    }
}
