using Godot;

// Last Animal — M06 audio (MC 890.7, artemis, 2026-09-03).
//
// SfxRouter: the thin wiring that "routes SFX off EventBus signals" (PHASE0.md
// Phase 7 gate). It subscribes to the five C2 game signals on EventBus and, for
// each, spawns a positional AudioStreamPlayer3D on the Sfx bus whose stream comes
// from MusicManager (so a single sub plays through the shared audio autoload).
//
// Deliberately NOT an autoload itself and no gameplay logic: a scene/root just adds
// it next to EventBus and calls Subscribe(). The headless test does exactly that.
namespace LastAnimal.Core.Audio;

public partial class SfxRouter : Node
{
    private MusicManager? _music;

    /// <summary>Wire SFX to the given EventBus via the shared MusicManager autoload.</summary>
    public void Subscribe(EventBus bus, MusicManager music)
    {
        _music = music;
        bus.DnaExtracted += _ => Fire("dna_extract");
        bus.DnaSpoken += _ => Fire("dna_spoken");
        bus.LoyaltyChanged += (_, _) => Fire("loyalty");
        bus.Betrayal += (_, _) => Fire("betrayal");
        bus.EcosystemAdapted += _ => Fire("ecosystem");
    }

    private void Fire(string sfx)
    {
        var rand = _music?.GetSfx(sfx);
        if (rand is null)
        {
            GD.Print($"SFX_ROUTER: fire \"{sfx}\" SKIPPED (stream not loaded)");
            return;
        }
        var player = _music!.SpawnSfxPlayer();   // positional, on the Sfx bus
        player.Stream = rand;                    // randomizer -> variation on repeat
        // Playback requires the node be inside the scene tree; parent it under the
        // router so the engine can mix it (Positional 3D -> Sfx bus).
        AddChild(player);
        player.Play();
        GD.Print($"SFX_ROUTER: fired \"{sfx}\"  bus={player.Bus}  playing={player.Playing}" +
                 $"  streamLen={player.Stream?.GetLength():0.000}s");
    }
}
