using Godot;

// Last Animal — M05 companion-system (MC 890.12, bernie, 2026-09-06).
//
// CompanionEntity: the Godot presentation Node that makes a companion a
// visible, ANIMATED entity in the world. It owns the retargeted humanoid
// skeleton (M09's shared AnimationLibrary, resources/animation/walkBaked.tres)
// and drives it from the M05 behavior: each Advance() reads the companion's
// CompanionStateMachine.State and plays the clip the CompanionAnimationHook
// resolves (the animation-hook consumption of M09 the Phase-9 gate names).
//
// The M05 PURE-LOGIC CORE (CompanionStateMachine/CompanionNeeds) stays in
// LastAnimal.Companion, engine-free and headless-testable. This file is the
// thin Godot seam (I1 orchestration): it only wires the hook to the rig, it
// never mutates loyalty (that is M03's) nor decides behavior (that is the
// machine's).
namespace LastAnimal.Companion;

/// <summary>
/// A minimal retargeted humanoid Skeleton3D, built the same way M09's bake
/// does (RIG bone names + rest pose), ready to consume a shared AnimationLibrary.
/// </summary>
public partial class CompanionRig : Skeleton3D
{
    public const string BakedLibraryPath =
        "res://resources/animation/walkBaked.tres";

    private static readonly string[] RIG = {
        "Hips","Spine","Chest","UpperChest","Neck","Head",
        "LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand",
        "RightShoulder","RightUpperArm","RightLowerArm","RightHand",
        "LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes",
        "RightUpperLeg","RightLowerLeg","RightFoot","RightToes",
    };
    private static readonly System.Collections.Generic.Dictionary<string,string> PARENT = new(){
        {"Spine","Hips"},{"Chest","Spine"},{"UpperChest","Chest"},
        {"Neck","UpperChest"},{"Head","Neck"},
        {"LeftShoulder","UpperChest"},{"LeftUpperArm","LeftShoulder"},
        {"LeftLowerArm","LeftUpperArm"},{"LeftHand","LeftLowerArm"},
        {"RightShoulder","UpperChest"},{"RightUpperArm","RightShoulder"},
        {"RightLowerArm","RightUpperArm"},{"RightHand","RightLowerArm"},
        {"LeftUpperLeg","Hips"},{"LeftLowerLeg","LeftUpperLeg"},
        {"LeftFoot","LeftLowerLeg"},{"LeftToes","LeftFoot"},
        {"RightUpperLeg","Hips"},{"RightLowerLeg","RightUpperLeg"},
        {"RightFoot","RightLowerLeg"},{"RightToes","RightFoot"},
    };

    public void BuildHumanoid()
    {
        AddBone("Root");
        foreach (var b in RIG)
        {
            int bi = AddBone(b);
            int pi = b == "Hips" ? FindBone("Root") : FindBone(PARENT[b]);
            SetBoneParent(bi, pi);
        }
        SetBoneRest(FindBone("Root"), new Transform3D(Basis.Identity, new Vector3(0, 1.0f, 0)));
        foreach (var b in RIG)
            SetBoneRest(FindBone(b), new Transform3D(Basis.Identity, Vector3.Zero));
    }
}

/// <summary>
/// The companion entity in the world: a rig + an AnimationPlayer driven by the
/// M05 animation hook. Advance() plays the clip for the machine's current state.
/// RootNode + the M09 shared library are bound in _Ready() (once the entity is
/// inside the tree), so the player can resolve paths and play clips.
/// </summary>
public partial class CompanionEntity : Node3D
{
    public CompanionStateMachine Machine { get; }
    public CompanionAnimationHook Hook { get; }
    public CompanionRig Rig { get; }
    public AnimationPlayer Player { get; }

    private bool _libraryBound;
    private readonly string _libraryName = "main"; // M09 BakeCheck contract (AddAnimationLibrary "main")

    public CompanionEntity(CompanionStateMachine machine, CompanionAnimationHook hook)
    {
        Machine = machine;
        Hook = hook;

        Rig = new CompanionRig { Name = "skel" };
        Rig.BuildHumanoid();
        AddChild(Rig);

        Player = new AnimationPlayer { Name = "anim" };
        AddChild(Player);
    }

    public override void _Ready()
    {
        // Now inside the tree: point the animation at a child named "skel",
        // relative to THIS node (the baked clip tracks are "skel:<bone>").
        Player.RootNode = Player.GetPathTo(this);

        // Bind M09's shared baked AnimationLibrary once.
        if (GD.Load<AnimationLibrary>(CompanionRig.BakedLibraryPath) is { } lib)
        {
            Player.AddAnimationLibrary(_libraryName, lib);
            _libraryBound = true;
        }
    }

    /// <summary>Tick the machine, then sync the rig to the hook's resolved clip.</summary>
    public void Advance()
    {
        Machine.Tick();
        if (!_libraryBound) return;
        // The M09 shared baked AnimationLibrary is bound under _libraryName, so
        // the clip the hook resolves ("walkBaked") is addressable on the player
        // as "<libraryName>/walkBaked" — Play()/CurrentAnimation need that form.
        string clip = Hook.Resolve(Machine);
        string qualified = _libraryName + "/" + clip;
        if (Player.CurrentAnimation != qualified)
        {
            Player.CurrentAnimation = qualified;
        }
        if (!Player.IsPlaying())
        {
            Player.Play(qualified);
        }
    }
}
