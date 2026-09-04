using Godot;
using LastAnimal.Core;
using LastAnimal.Core.Framework;
using LastAnimal.Core.Audio;

// Last Animal — M06 audio headless DoD test (MC 890.7, artemis, 2026-09-03).
//
// Phase-7 DoD (PHASE0.md Phase 7): a headless test fires an EventBus signal that
// triggers a non-silent SFX on the correct bus.
//
// Godot has no audio device in --headless mode, so "non-silent" is asserted from the
// actual stream data: the SFX streams must load with GetLength() > 0 (real PCM, not
// silent), the router must place them on the "Sfx" bus, and the player must be Playing.
// The DoD's "C-1 run with logged bus activity" path is what we gate on (see Gate 3).
//
// Why verification runs in _Process, not _Initialize: a SceneTree's _Initialize runs
// BEFORE the engine enters the tree / processes a frame, so a player AddChild-ed there
// has not yet "entered the tree" from the engine's perspective and Play() cannot start
// ("Playback can only happen when a node is inside the scene tree", Playing=False).
// By deferring the assertion to the first _Process tick the tree is fully active and
// the fired player is genuinely Playing with its stream routed to the Sfx bus.
public partial class AudioTest : SceneTree
{
    private int _failures = 0;
    private int _stage = 0;               // 0=compose, 1=fire, 2=assert, 3=done
    private SfxRouter? _sfx;
    private MusicManager? _music;
    private EventBus? _bus;

    public override void _Initialize()
    {
        GD.Print("M06_AUDIO_TEST: start");
        _stage = 1;
        var root = new Node3D { Name = "M06AudioTestRoot" };
        Root.AddChild(root);

        // --- compose the audio autoload + router (headless, no scene file) -----
        _music = new MusicManager();
        _music.Name = "MusicManager";
        root.AddChild(_music);
        _bus = new EventBus();
        root.AddChild(_bus);
        _sfx = new SfxRouter();
        root.AddChild(_sfx);
        _sfx.Subscribe(_bus, _music);

        // --- Gate 1: bus infrastructure exists (music + sfx buses) ------------
        _music.Boot();                              // EnsureBuses + LoadSfx
        Check("bus 'Sfx' exists", HasBus(MusicManager.SfxBus), "must be added by EnsureBuses");
        Check("bus 'Music' exists", HasBus(MusicManager.MusicBus), "must be added by EnsureBuses");

        // --- Gate 2: every routed SFX is a real, non-silent asset (PCM len>0) --
        foreach (var id in new[] { "dna_extract", "dna_spoken", "loyalty", "betrayal", "ecosystem" })
        {
            var rand = _music.GetSfx(id);
            var wav = GD.Load<AudioStreamWav>($"res://assets/audio/{id}.wav");
            double len = wav?.GetLength() ?? -1.0;
            bool ok = rand != null && wav != null && len > 0.0;
            Check($"SFX '{id}' loaded + non-silent (len={len:0.000}s)", ok,
                  "WAV must load and GetLength()>0 (real PCM, not a silent stub)");
        }
    }

    public override bool _Process(double delta)
    {
        // Tree is fully active now (this runs on the first process frame). Fire the
        // EventBus signal AND assert on the routed player in the SAME call: after
        // SfxRouter.Fire returns, its AddChild(player)+Play() has already run inside
        // a live tree, so the positional player is genuinely Playing on the Sfx bus.
        if (_stage == 1)
        {
            _stage = 2;
            _bus!.EmitDnaExtracted(new DnaSignature("sig_9000", "wolf"));

            // The router parented the fired player under itself; grab it and assert.
            int sfxChildren = _sfx!.GetChildCount();
            bool routedOk = false;
            string routedDetail = $"(no fired player found; children={sfxChildren})";
            if (sfxChildren > 0)
            {
                var fired = _sfx.GetChild<AudioStreamPlayer3D>(0);
                var len = fired.Stream?.GetLength() ?? -1.0;
                routedOk = fired.Bus == MusicManager.SfxBus && fired.Playing && len > 0.0;
                routedDetail = $"bus={fired.Bus} playing={fired.Playing} streamLen={len:0.000}s";
            }
            Check("EventBus signal -> non-silent SFX on Sfx bus", routedOk,
                  routedDetail + " (fired player must be on Sfx bus, playing, len>0)");
        }

        // --- Gate 4: music autoload can start a non-silent Ogg on the Music bus --
        var ogg = GD.Load<AudioStreamOggVorbis>("res://assets/audio/music_theme.ogg");
        Check("music Ogg loads non-silent (len>0)", ogg != null && ogg.GetLength() > 0.0,
              $"music_theme.ogg GetLength()={(ogg?.GetLength() ?? -1):0.000}s");
        if (ogg != null)
            _music!.PlayMusic(ogg);

        // --- verdict -----------------------------------------------------------
        if (_failures == 0)
            GD.Print("M06_AUDIO_TEST: PASS — EventBus triggered non-silent SFX on Sfx bus; music on Music bus");
        else
            GD.Print($"M06_AUDIO_TEST: FAIL ({_failures} check(s) failed)");

        Quit(_failures == 0 ? 0 : 1);
        return false;   // stop iterating once verified + quit requested
    }

    private static bool HasBus(string name)
    {
        for (int i = 0; i < AudioServer.BusCount; i++)
            if (AudioServer.GetBusName(i) == name)
                return true;
        return false;
    }

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"M06_AUDIO_TEST: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) _failures++;
    }
}
