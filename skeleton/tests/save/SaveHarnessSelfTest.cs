using Xunit;
using LastAnimal.Save;

// Last Animal — M11 save-progression harness self-test (MC 890.15, gunilla,
// 2026-09-06). Deliberately broken: claimed invalid semantics, so it MUST fail.
// The gate script drops it via /p:IncludeHarness=false for the real green run.
// This is the two-sided calibration — the harness must be able to go red.
namespace LastAnimal.Tests.Save;

public class SaveHarnessSelfTest
{
    // DELIBERATELY WRONG: a null store is claimed to still save. The real
    // SaveSystem refuses a null store (returns false). Must FAIL.
    [Fact]
    public void DeliberatelyBroken_NullStore_Saves()
    {
        var store = new TempDirSaveStore();
        // Claim: gracefully ignoring the store still writes the file.
        // (Incorrect — the harness must be able to fail.)
        Assert.True(store.Exists(store.SavePath));
    }
}
