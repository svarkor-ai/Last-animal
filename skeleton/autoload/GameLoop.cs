using Godot;
using LastAnimal.Core.Audio;
using LastAnimal.Core.Framework;

// Last Animal — M01 bridge CARD 2 (MC 1123.3, artemis, 2026-09-07).
// T3b ownership refactor (MC 1256.9, artemis, 2026-09-21): BOOT-ONLY autoload.
//
// GameLoop: the boot autoload. Per the approved design (1256.2 §1.1/§1.3
// option A-lite) WorldDirector — the scene root on main.tscn — is the ONE
// composition root and runtime owner of every gameplay system. This autoload
// keeps ONLY its boot duties:
//   - resolve the EventBus + GameBootstrap autoloads,
//   - boot music/SFX (M06) and route the bus signals,
//   - run GameBootstrap.Boot() with the six BootAdapters (RG4 boot-order
//     markers the gates assert),
//   - print the "GameLoop: ready" marker ci/smoke.sh:60 greps for.
//
// Its former _Process tick, the ten private system fields and the private
// DnaExtracted handler are DELETED: they ticked phantom sidecar objects no
// scene node could ever see (the duplicate-composition-root bug this card
// cures). The Combat BootAdapter is now a no-op — the director owns the
// CombatSystem and subscribes its own DnaExtracted handler.
namespace LastAnimal.Core;

public partial class GameLoop : Node
{
    private EventBus _bus = null!;
    private GameBootstrap _bootstrap = null!;

    public override void _Ready()
    {
        _bus = GetNode<EventBus>("/root/EventBus");
        _bootstrap = GetNode<GameBootstrap>("/root/GameBootstrap");

        // --- M06: route SFX off the bus + start the music theme. ------------
        var music = GetNode<MusicManager>("/root/MusicManager");
        var sfx = GetNode<SfxRouter>("/root/SfxRouter");
        music.Boot();                                   // ensure buses + load SFX streams
        sfx.Subscribe(_bus, music);                     // route the 5 C2 signals to SFX
        var theme = GD.Load<AudioStreamOggVorbis>("res://assets/audio/music_theme.ogg");
        if (theme != null) music.PlayMusic(theme);      // cross-scene music loop

        // --- RG4: bind every pure module behind an IGameModule BootAdapter ---
        // so GameBootstrap.Boot() logs each boot marker in registration order.
        // Gameplay systems are NOT constructed here (the director owns them);
        // the adapters only preserve the boot-order log contract.
        _bootstrap.Bind(new BootAdapter("Salary",      () => { }));
        _bootstrap.Bind(new BootAdapter("Betrayal",    () => { }));
        _bootstrap.Bind(new BootAdapter("Companion",   () => { }));
        _bootstrap.Bind(new BootAdapter("Combat",      () => { }));
        _bootstrap.Bind(new BootAdapter("Ecosystem",   () => { }));
        _bootstrap.Bind(new BootAdapter("DnaLanguage", () => { }));
        _bootstrap.Bind<EventBus>(_bus);
        _bootstrap.Boot();

        GD.Print("GameLoop: ready — boot-only autoload (composition root is WorldDirector)");
    }

    /// <summary>
    /// Tiny IGameModule wrapper: adapts a pure module's boot callback to the
    /// framework seam so GameBootstrap.Boot() walks them all in order (RG4).
    /// The adapter carries NO gameplay logic — it only names + triggers the
    /// module's actual boot work via the delegate (I1).
    /// </summary>
    private sealed class BootAdapter : IGameModule
    {
        private readonly System.Action _boot;
        public string Name { get; }
        public BootAdapter(string name, System.Action boot) { Name = name; _boot = boot; }
        public void Boot() => _boot();
    }
}
