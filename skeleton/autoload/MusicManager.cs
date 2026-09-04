using Godot;
using System;
using System.Collections.Generic;

// Last Animal — M06 audio (MC 890.7, artemis, 2026-09-03).
//
// MusicManager: the audio autoload (PHASE0.md Phase 7 / line 300). It owns the
// two audio buses M06 introduces — "Music" (music playback) and "Sfx" (positional
// 3D sound effects) — and hands out positional AudioStreamPlayer3D nodes on the
// Sfx bus, so SFX can be routed from gameplay code (SfxRouter, a separate file)
// without coupling to the audio internals (I4: modules never reference each other
// directly; they go through this autoload or the EventBus).
//
// Deliberately thin (C3 orchestration discipline): it does NOT decide WHAT plays —
// SfxRouter maps EventBus signals to streams. MusicManager only provides the
// bus + player infrastructure and the music entry point. Together they satisfy the
// Phase-7 gate: WAV SFX + Ogg music imported, positional AudioStreamPlayer3D on the
// Sfx bus, AudioStreamRandomizer variation, and a Music bus for cross-scene music.
//
// Headless note (test-friendly by construction): buses are ensured at runtime
// (AudioServer.AddBus) so a headless --script run with no default_bus_layout.tres
// still gets a named "Sfx"/"Music" bus to assert on — this is what lets
// tests/AudioTest.cs check "non-silent SFX on the correct bus" without a device.
namespace LastAnimal.Core.Audio;

public partial class MusicManager : Node
{
    public const string SfxBus = "Sfx";
    public const string MusicBus = "Music";

    // Every SFX-backed EventBus signal we route. The Id key matches the SfxRouter
    // routing table; one randomizer per signal gives variation (AudioStreamRandomizer).
    private readonly Dictionary<string, AudioStreamRandomizer> _sfx = new();

    // Music bus + cross-scene music entry point (pure infrastructure, no logic).
    public void Boot()
    {
        EnsureBuses();
        LoadSfx();
        GD.Print("MusicManager: booted (buses: ", SfxBus, "/", MusicBus, ")");
    }

    /// <summary>Create the "Music" and "Sfx" buses if they do not already exist.</summary>
    public void EnsureBuses()
    {
        AddBusIfMissing(SfxBus);
        AddBusIfMissing(MusicBus);
    }

    private static void AddBusIfMissing(string name)
    {
        for (int i = 0; i < AudioServer.BusCount; i++)
            if (AudioServer.GetBusName(i) == name)
                return;
        AudioServer.AddBus();
        AudioServer.SetBusName(AudioServer.BusCount - 1, name);
        GD.Print("MusicManager: ensured bus \"", name, "\"");
    }

    /// <summary>Load the five CC0 SFX streams into per-signal AudioStreamRandomizers.</summary>
    private void LoadSfx()
    {
        RegisterSfx("dna_extract", "res://assets/audio/dna_extract.wav");
        RegisterSfx("dna_spoken", "res://assets/audio/dna_spoken.wav");
        RegisterSfx("loyalty", "res://assets/audio/loyalty.wav");
        RegisterSfx("betrayal", "res://assets/audio/betrayal.wav");
        RegisterSfx("ecosystem", "res://assets/audio/ecosystem.wav");
    }

    private void RegisterSfx(string id, string path)
    {
        // AudioStreamWav: PCM WAV -> stream with a real, >0 sample length (non-silent).
        // The randomizer wraps ALL the variants for one signal so repeated firings
        // vary (AudioStreamRandomizer -> picks randomly), which is the Phase-7 gate.
        var wav = GD.Load<AudioStreamWav>(path);
        if (wav is null)
        {
            GD.PushWarning($"MusicManager: failed to load SFX {id} at {path}");
            return;
        }
        var rand = new AudioStreamRandomizer();
        rand.AddStream(0, wav, 1.0f);
        _sfx[id] = rand;
        GD.Print($"MusicManager: loaded SFX \"{id}\" ({path}, length={wav.GetLength():0.000}s)");
    }

    /// <summary>Return the variation randomizer for an SFX id (null if unknown).</summary>
    public AudioStreamRandomizer? GetSfx(string id)
        => _sfx.TryGetValue(id, out var s) ? s : null;

    /// <summary>Create a positional AudioStreamPlayer3D on the Sfx bus that frees itself on finish.</summary>
    public AudioStreamPlayer3D SpawnSfxPlayer()
    {
        var p = new AudioStreamPlayer3D { Bus = SfxBus };
        p.Finished += () => p.QueueFree();   // short-lived; frees after play to avoid buildup
        return p;
    }

    /// <summary>Play cross-scene music on the Music bus (single looping player).</summary>
    public void PlayMusic(AudioStream stream)
    {
        var player = GetNodeOrNull<AudioStreamPlayer>("%MusicPlayer");
        if (player is null)
        {
            player = new AudioStreamPlayer { Name = "MusicPlayer", Bus = MusicBus };
            AddChild(player);
            player.SetMeta("MusicPlayer", true);
        }
        player.Stream = stream;
        player.Autoplay = true;
        player.Finished += () => player.Play(); // seamless loop (OggVorbis loops via its stream too)
        GD.Print($"MusicManager: playing music on bus \"{MusicBus}\"");
    }
}
