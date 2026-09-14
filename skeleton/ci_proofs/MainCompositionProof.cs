using Godot;
using LastAnimal.World;

// Last Animal — M01 bridge CARD 3 (MC 1123.9, artemis; w3fix2 MC 1123.11, dobbie).
//
// main_composition_proof.cs — proves the playable composition root (res://main.tscn)
// is actually playable, as CARD 3 of BRIDGE-MVP.md §2 STEP C requires ("+ proof"):
//   RG2 closed  : project.godot run/main_scene points at main.tscn (not preflight).
//   Scene loads : main.tscn instantiates the meadow zone + a Player CharacterBody3D +
//                 the top-level FollowCamera rig without error (the scene tree is real).
//   Player moves: simulated WASD ("move_right" on the [input] map from 1123.2/RG3)
//                 drives PlayerController.Move -> CharacterBody3D XZ displacement.
//   Cam follows : the top-level "Camera" node (world/FollowCamera.cs) lerps toward
//                 Target+Offset, so it tracks the player's position.
//
// w3fix2 (MC 1123.11): the w3fix composition moved the camera from a CameraRig child
// of Player to a top-level "Camera" node. This proof asserts THAT composition, and
// presses the input BEFORE any early-return path so a failed assert can never leave
// _Process running with the input unpressed (the stale-proof failure mode).
//
// A SceneTree `--script` run does NOT instantiate engine autoloads, so we instance
// main.tscn ourselves and drive the Player node directly; main.tscn/Player.cs do not
// depend on autoload singletons in _Ready, so this runs clean. Any missing marker
// makes the script exit non-zero (green gate can fail).
//
// Run:  $GODOT --headless --path <proj> --script res://ci_proofs/MainCompositionProof.cs
public partial class MainCompositionProof : SceneTree
{
    private Node3D? _main;
    private Player? _player;
    private CharacterBody3D? _body;
    private FollowCamera? _camera;
    private Vector3 _start;
    private Vector3 _camStart;
    private bool _asserted;
    private bool _failed;

    public override void _Initialize()
    {
        GD.Print("MAIN_COMPOSITION_PROOF: start");

        // --- RG2: confirm the entry scene is main.tscn, not preflight ---------
        var cfg = new ConfigFile();
        var err = cfg.Load("res://project.godot");
        var entry = cfg.GetValue("application", "run/main_scene", "").AsString();
        if (err != Error.Ok || entry != "res://main.tscn")
        {
            Fail($"run/main_scene='{entry}' (err={err}); RG2 not closed");
            return;
        }
        GD.Print("MAIN_COMPOSITION_PROOF: RG2 OK — run/main_scene=res://main.tscn");

        // --- instance the composition root -----------------------------------
        var packed = GD.Load<PackedScene>("res://main.tscn");
        if (packed == null)
        {
            Fail("cannot load res://main.tscn");
            return;
        }
        _main = packed.Instantiate<Node3D>();
        Root.AddChild(_main);
        GD.Print("MAIN_COMPOSITION_PROOF: main.tscn instantiated");

        _body = _main.GetNodeOrNull<CharacterBody3D>("Player");
        _player = _body as Player;
        if (_body == null || _player == null)
        {
            Fail("Player node not found in main.tscn");
            return;
        }

        // Press walk-right BEFORE any remaining early return: from here on every
        // fail path releases the input, so _Process never runs it unpressed.
        Input.ActionPress("move_right");

        // --- w3fix composition: camera is the TOP-LEVEL "Camera" node ---------
        _camera = _main.GetNodeOrNull<FollowCamera>("Camera");
        if (_camera == null)
        {
            Fail("top-level Camera node (FollowCamera) not found in main.tscn");
            return;
        }
        _start = _body.GlobalPosition;
        _camStart = _camera.GlobalPosition;
        // NOTE: during _Initialize the just-instanced nodes are NOT yet inside the
        // tree (GlobalPosition errors + returns identity), so real baselines are
        // captured on the first _Process frame instead (see _Process).
        GD.Print("MAIN_COMPOSITION_PROOF: composition resolved (Player + top-level Camera/FollowCamera)");
    }

    private void Fail(string why)
    {
        // Every fail path: release the pressed input, flag, quit non-zero.
        Input.ActionRelease("move_right");
        _failed = true;
        GD.PrintErr($"MAIN_COMPOSITION_PROOF: FAIL — {why}");
        Quit(1);
    }

    private int _frames;

    public override bool _Process(double delta)
    {
        if (_failed || _body == null || _camera == null)
            return true;    // a failed assert already quit(1); do nothing further

        // First live frame: the nodes are inside the tree now — capture the real
        // spawn baselines (GlobalPosition is invalid during _Initialize).
        if (_frames == 0)
        {
            _start = _body.GlobalPosition;
            _camStart = _camera.GlobalPosition;
            GD.Print($"MAIN_COMPOSITION_PROOF: player start=({_start.X:0.###},{_start.Y:0.###},{_start.Z:0.###}) " +
                     $"camera=({_camStart.X:0.###},{_camStart.Y:0.###},{_camStart.Z:0.###}) offset={_camera.Offset}");
        }

        _frames++;
        var inp = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (_frames <= 8)
            GD.Print($"MAIN_COMPOSITION_PROOF: f{_frames} inp=({inp.X},{inp.Y}) gp=({_body.GlobalPosition.X:0.####},{_body.GlobalPosition.Y:0.####},{_body.GlobalPosition.Z:0.####})");
        if (_frames < 10) return false;    // let physics settle + accumulate movement

        // --- player must have moved +X (move_right => +X on the input map) ---
        Vector3 now = _body.GlobalPosition;
        float dx = now.X - _start.X;
        float dz = now.Z - _start.Z;
        GD.Print($"MAIN_COMPOSITION_PROOF: after {_frames} frames player dx={dx:0.####} dz={dz:0.####}");

        if (!(dx > 0.05f || dz > 0.05f))
        {
            Fail("player did not move on move_right (RG3/C10)");
            return true;
        }
        GD.Print("MAIN_COMPOSITION_PROOF: PLAYER_MOVED (input -> PlayerController -> CharacterBody3D)");

        // --- camera must track the player (FollowCamera lerps to Target+Offset)
        // Loose tolerance: the lerp has only had ~10 frames to converge.
        Vector3 ideal = now + _camera.Offset;
        float camDx = Mathf.Abs(_camera.GlobalPosition.X - ideal.X);
        float camDz = Mathf.Abs(_camera.GlobalPosition.Z - ideal.Z);
        if (camDx > 2.0f || camDz > 2.0f)
        {
            Fail($"camera off ideal(Target+Offset) by dx={camDx:0.###} dz={camDz:0.###}");
            return true;
        }
        // And it must have MOVED TOWARD the player, not sat still at its spawn pose.
        if (_camera.GlobalPosition.DistanceTo(_camStart) < 0.05f)
        {
            Fail("camera did not move toward the player (no follow lerp)");
            return true;
        }
        GD.Print("MAIN_COMPOSITION_PROOF: CAMERA_FOLLOWS (top-level FollowCamera tracks player)");

        Input.ActionRelease("move_right");
        GD.Print("MAIN_COMPOSITION_PROOF: PASS — playable composition root verified (RG2 + RG3 + C10 + C17)");
        _asserted = true;
        Quit(0);
        return true;
    }

    public override void _Finalize()
    {
        Input.ActionRelease("move_right");   // unconditional: never leak a pressed action
        if (!_asserted && !_failed)
            GD.PrintErr("MAIN_COMPOSITION_PROOF: FAIL — finished without asserting movement");
    }
}
