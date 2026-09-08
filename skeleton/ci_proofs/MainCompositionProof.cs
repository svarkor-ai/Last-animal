using Godot;

// Last Animal — M01 bridge CARD 3 (MC 1123.9, artemis, 2026-09-08).
//
// main_composition_proof.cs — proves the playable composition root (res://main.tscn)
// is actually playable, as CARD 3 of BRIDGE-MVP.md §2 STEP C requires ("+ proof"):
//   RG2 closed  : project.godot run/main_scene points at main.tscn (not preflight).
//   Scene loads : main.tscn instantiates the meadow zone + a Player CharacterBody3D +
//                 isometric Camera3D without error (the scene tree is real).
//   Player moves: simulated WASD ("move_right" on the [input] map from 1123.2/RG3)
//                 drives PlayerController.Move -> CharacterBody3D XZ displacement.
//   Cam follows : the CameraRig (isometric Camera3D) tracks the player's position.
//
// A SceneTree `--script` run does NOT instantiate engine autoloads, so we instance
// main.tscn ourselves and drive the Player node directly; main.tscn/Player.cs do not
// depend on autoload singletons in _Ready, so this runs clean. Any missing marker
// makes the script exit non-zero (green gate can fail).
//
// Run:  $GODOT --headless --path <proj> --script res://ci_proofs/main_composition_proof.cs
public partial class MainCompositionProof : SceneTree
{
    private Node3D? _main;
    private Player? _player;
    private CharacterBody3D? _body;
    private Node3D? _cameraRig;
    private Vector3 _start;
    private bool _asserted;

    public override void _Initialize()
    {
        GD.Print("MAIN_COMPOSITION_PROOF: start");

        // --- RG2: confirm the entry scene is main.tscn, not preflight ---------
        var cfg = new ConfigFile();
        var err = cfg.Load("res://project.godot");
        var entry = cfg.GetValue("application", "run/main_scene", "").AsString();
        if (err != Error.Ok || entry != "res://main.tscn")
        {
            GD.PrintErr($"MAIN_COMPOSITION_PROOF: FAIL — run/main_scene='{entry}' (err={err}); RG2 not closed");
            Quit(1);
            return;
        }
        GD.Print("MAIN_COMPOSITION_PROOF: RG2 OK — run/main_scene=res://main.tscn");

        // --- instance the composition root -----------------------------------
        var packed = GD.Load<PackedScene>("res://main.tscn");
        if (packed == null)
        {
            GD.PrintErr("MAIN_COMPOSITION_PROOF: FAIL — cannot load res://main.tscn");
            Quit(1);
            return;
        }
        _main = packed.Instantiate<Node3D>();
        Root.AddChild(_main);
        GD.Print("MAIN_COMPOSITION_PROOF: main.tscn instantiated");

        _body = _main.GetNodeOrNull<CharacterBody3D>("Player");
        _player = _body as Player;
        _cameraRig = _body?.GetNodeOrNull<Node3D>("CameraRig");
        if (_body == null || _player == null || _cameraRig == null)
        {
            GD.PrintErr("MAIN_COMPOSITION_PROOF: FAIL — Player/CameraRig node not found in main.tscn");
            Quit(1);
            return;
        }
        _start = _body.GlobalPosition;
        GD.Print($"MAIN_COMPOSITION_PROOF: player start=({_start.X:0.###},{_start.Y:0.###},{_start.Z:0.###})");

        // --- press walk-right on the [input] map (RG3) for a few frames --------
        Input.ActionPress("move_right");
    }

    private int _frames;

    public override bool _Process(double delta)
    {
        _frames++;
        var inp = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (_frames <= 8)
            GD.Print($"MAIN_COMPOSITION_PROOF: f{_frames} inp=({inp.X},{inp.Y}) gp=({_body!.GlobalPosition.X:0.####},{_body.GlobalPosition.Y:0.####},{_body.GlobalPosition.Z:0.####})");
        if (_frames < 10) return false;    // let physics settle + accumulate movement

        // --- player must have moved +X (move_right => +X on the input map) ---
        Vector3 now = _body!.GlobalPosition;
        float dx = now.X - _start.X;
        float dz = now.Z - _start.Z;
        GD.Print($"MAIN_COMPOSITION_PROOF: after {_frames} frames player dx={dx:0.####} dz={dz:0.####}");

        if (!(dx > 0.05f || dz > 0.05f))
        {
            GD.PrintErr("MAIN_COMPOSITION_PROOF: FAIL — player did not move on move_right (RG3/C10)");
            Quit(1);
            return true;
        }
        GD.Print("MAIN_COMPOSITION_PROOF: PLAYER_MOVED (input -> PlayerController -> CharacterBody3D)");

        // --- camera rig must have followed to near the player -----------------
        float camDx = Mathf.Abs(_cameraRig!.GlobalPosition.X - now.X);
        float camDz = Mathf.Abs(_cameraRig.GlobalPosition.Z - now.Z);
        if (camDx > 0.5f || camDz > 0.5f)
        {
            GD.PrintErr($"MAIN_COMPOSITION_PROOF: FAIL — camera off by dx={camDx:0.###} dz={camDz:0.###}");
            Quit(1);
            return true;
        }
        GD.Print("MAIN_COMPOSITION_PROOF: CAMERA_FOLLOWS (isometric Camera3D tracks player)");

        Input.ActionRelease("move_right");
        GD.Print("MAIN_COMPOSITION_PROOF: PASS — playable composition root verified (RG2 + RG3 + C10 + C17)");
        _asserted = true;
        Quit(0);
        return true;
    }

    public override void _Finalize()
    {
        if (!_asserted)
            GD.PrintErr("MAIN_COMPOSITION_PROOF: FAIL — finished without asserting movement");
    }
}
