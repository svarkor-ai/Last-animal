using Godot;
using LastAnimal.Companion;
using LastAnimal.Npc;

// Last Animal — M05 companion-system render DoD test (MC 890.12, bernie, 2026-09-06).
//
// Phase-9 DoD (PHASE0.md Phase 9): "a companion entity animates via the
// retargeted rig in a headless render."
//
// Runs as a NODE in a main scene (companion_render.tscn) under the
// graphical-test-helper on a live Xvfb display (NOT --headless), so the 3D
// scene renders to a real framebuffer and the helper asserts the capture is
// non-blank. Root-cause note 2026-09-06: a raw SceneTree `--script` only runs
// its `_process` ONE pass then quits, so a 20-frame animation can never play;
// a Node main scene iterates normally. Also fixed: M09 binds the baked clip
// under library "main", so the playable name is "main/walkBaked", not the bare
// "walkBaked" the hook returns.
//
// Phases: 0=compose,1=drive (articulate 20 frames),2=assert + hold so the
// helper can snapshot the LIVE articulated rig,3=quit.
//
// Run: graphical-test-helper.sh --cmd "GODOT --path <skeleton> res://tests/companion/companion_render.tscn" --wait 4
public partial class CompanionRenderTest : Node
{
    private int _failures;
    private int _frameCounter;
    private int _holdFrames;
    private CompanionEntity? _companion;
    private CompanionRig? _rig;
    private bool _armed;
    private int _stage;

    public override void _Ready()
    {
        GD.Print("M05_RENDER_TEST: start");
        _stage = 1;

        var world = new Node3D { Name = "World" };
        AddChild(world);

        // Camera looking at the companion so its articulation is visible.
        var cam = new Camera3D { Name = "Cam", Current = true, Fov = 60 };
        cam.Position = new Vector3(0, 2.2f, 5);
        cam.LookAtFromPosition(cam.Position, new Vector3(0, 1.0f, 0), Vector3.Up);
        world.AddChild(cam);

        var light = new DirectionalLight3D { Name = "Sun" };
        light.RotationDegrees = new Vector3(-45, 0, 0);
        world.AddChild(light);

        // A Following companion: loyalty healthy, no salary due yet.
        var comp = new CompanionComponent { Id = 1, CompanionEntityId = 2, Loyalty = 80 };
        var needs = new CompanionNeeds(graceTicks: 10_000); // stay Following for this render
        var machine = new CompanionStateMachine("garn", comp, needs);
        // The hook maps state -> CLIP NAME: a Following companion -> "walkBaked",
        // the clip in M09's shared baked AnimationLibrary (resources/animation/walkBaked.tres).
        var hook = new CompanionAnimationHook("walkBaked", "needIdle", "betrayedIdle");

        _companion = new CompanionEntity(machine, hook);
        world.AddChild(_companion);
        _rig = _companion.Rig;

        bool bnd = _companion.Player.GetAnimationLibraryList().Count != 0;
        bool hc = _companion.Player.HasAnimation("main/walkBaked");
        GD.Print("M05_RENDER_TEST: libraryBound=" + bnd +
                 " root=" + _companion.Player.RootNode +
                 " hasClip=" + hc);
        _armed = bnd && hc;

        // Stay alive (in frames) after articulating so graphical-test-helper
        // can snapshot the LIVE articulated rig; HOLD_MS is seconds-as-ms
        // (mirror M08 crowd_scene), default 3000ms.
        string hold = OS.GetEnvironment("HOLD_MS");
        double holdMs = string.IsNullOrEmpty(hold) ? 3000.0 : double.Parse(hold);
        _holdFrames = (int)System.Math.Ceiling(holdMs / 1000.0 * 60.0); // ~60fps
    }

    public override void _Process(double delta)
    {
        if (_stage == 1)
        {
            // Drive the companion across real frames so the AnimationPlayer
            // progresses the walk clip and articulates the retargeted skeleton.
            if (_companion is null || !_armed)
            {
                Fail("companion could not bind M09 baked clip (main/walkBaked)");
                _stage = 3; // still quit via gate below after hold
                _finish(true); // quit now — nothing to animate
                return;
            }
            _companion.Advance();
            if (_frameCounter++ < 20)
            {
                return;               // keep iterating; let animation play out
            }

            // Gate: the rig must be articulating — at least two limbs leave rest
            // pose (angle > threshold), proving M09's baked clip drives it.
            int moved = 0;
            foreach (var bone in new[] { "LeftUpperArm", "LeftLowerArm",
                                         "LeftUpperLeg", "LeftLowerLeg", "RightUpperLeg" })
            {
                int idx = _rig!.FindBone(bone);
                if (idx < 0) continue;
                var r = _rig.GetBonePoseRotation(idx);
                double ang = 2.0 * System.Math.Acos(System.Math.Clamp((double)r.W, -1.0, 1.0));
                if (ang > 0.20) moved++;
            }
            bool articulated = moved >= 2;
            Check("retargeted rig articulates (limbs leave rest pose) via shared baked clip",
                  articulated, $"limbs moved past 0.20 rad = {moved}");
            GD.Print(_failures == 0
                ? "M05_RENDER_TEST: PASS — companion Following -> main/walkBaked; rig articulated"
                : $"M05_RENDER_TEST: FAIL ({_failures} check(s))");
            _stage = 2;
            // fall through: begin hold
        }

        if (_stage == 2)
        {
            // Hold the LIVE articulated rig on screen so graphical-test-helper
            // can snapshot it, while the walk clip keeps playing.
            if (_holdFrames-- > 0) return;
            _stage = 3;
            GetTree().Quit(_failures == 0 ? 0 : 1);
            return;
        }

        if (_stage == 3)
        {
            GetTree().Quit(_failures == 0 ? 0 : 1);
            return;
        }
    }

    private void _finish(bool now)
    {
        GetTree().Quit(now && _failures == 0 ? 0 : 1);
    }

    private void Check(string what, bool ok, string detail)
    {
        GD.Print($"M05_RENDER_TEST: check: {what}: {(ok ? "ok" : "FAIL")} {detail}");
        if (!ok) _failures++;
    }

    private void Fail(string what)
    {
        _failures++;
        GD.Print($"M05_RENDER_TEST: check: {what}: FAIL");
    }
}
