using LastAnimal.Ecosystem;
using Xunit;

// Last Animal — M12 boss-ecosystem-adaptation harness self-test
// (MC 890.16, artemis, 2026-09-06).
//
// The two-sided calibration pattern (same as combat_test.sh / Phase 5/8):
// this ONE deliberately-broken test asserts a KNOWN-WRONG value — it must go
// RED. The gate script runs the suite twice:
//   1. With this file present -> exactly 1 failure (this test), all others green.
//   2. With this file excluded -> 0 failures.
// If the harness cannot fail, the gate is broken.
namespace LastAnimal.Tests;

public class EcosystemHarnessSelfTest
{
    [Fact]
    public void DeliberatelyBroken_UnknownZone_SpawnsBoss()
    {
        // WRONG: claims the meadow (safest, bossTier=0) fallback spawns a boss
        // when fully adapted. A shallow zone NEVER spawns a boss. This is
        // DELIBERATELY BROKEN — it must go red.
        var spawner = new EcosystemSpawner(seed: 1);
        var set = spawner.OnZoneEnter("unknown-zone", new Dna.CounterProfile(new int[0], new int[0], 20));
        Assert.True(set.HasBoss);
    }
}
