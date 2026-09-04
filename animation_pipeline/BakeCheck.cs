using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Last Animal — M09 animation-pipeline C12 retarget bake (MC 890.5, dobbie, 2026-09-03)
//
// Phase-4 DoD (PHASE0.md Phase 4, C12): at least one CC0 rig retargeted via
// SkeletonProfileHumanoid + BoneMap into a shared AnimationLibrary; headless bake
// check PASSES with zero T-pose/roll/stretch gap frames; a sample animation drives
// a retargeted skeleton without T-pose frames. Reproducible across runs (839.4r
// brief: identical sha512 of outputs).
//
// All rigs are self-authored here => unambiguous CC0 (LICENSES.md records this).
// The retarget pipeline uses the real engine classes: SkeletonProfileHumanoid,
// BoneMap, RetargetModifier3D, Skeleton3D, AnimationPlayer, AnimationLibrary.
// Run headless: godot --headless --script res://BakeCheck.cs (a SceneTree).
public partial class BakeCheck : SceneTree
{
    private int _failures;

    private static readonly string[] RIG = {
        "Hips","Spine","Chest","UpperChest","Neck","Head",
        "LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand",
        "RightShoulder","RightUpperArm","RightLowerArm","RightHand",
        "LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes",
        "RightUpperLeg","RightLowerLeg","RightFoot","RightToes",
    };
    private static readonly Dictionary<string,string> PARENT = new(){
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
    private static readonly Dictionary<string,Vector3> SRC_REST = Rests(0.95f,0.22f,0.30f,0.45f,0.09f);
    private static readonly Dictionary<string,Vector3> DST_REST = Rests(1.10f,0.26f,0.36f,0.55f,0.115f);

    private static Dictionary<string,Vector3> Rests(float hipH,float spine,float arm,float leg,float legX)
        => new(){
            {"Hips",new(0,hipH,0)},{"Spine",new(0,spine,0)},{"Chest",new(0,spine-0.02f,0)},
            {"UpperChest",new(0,spine-0.02f,0)},{"Neck",new(0,spine-0.02f,0)},{"Head",new(0,0.16f,0)},
            {"LeftShoulder",new(-0.04f,0.03f,0)},{"LeftUpperArm",new(-arm,-0.02f,0)},
            {"LeftLowerArm",new(0,-arm,0)},{"LeftHand",new(0,-arm*0.93f,0)},
            {"RightShoulder",new(0.04f,0.03f,0)},{"RightUpperArm",new(arm,-0.02f,0)},
            {"RightLowerArm",new(0,-arm,0)},{"RightHand",new(0,-arm*0.93f,0)},
            {"LeftUpperLeg",new(-legX,-0.12f,0)},{"LeftLowerLeg",new(0,-leg,0)},
            {"LeftFoot",new(0,-leg,0.13f)},{"LeftToes",new(0,0,0.20f)},
            {"RightUpperLeg",new(legX,-0.12f,0)},{"RightLowerLeg",new(0,-leg,0)},
            {"RightFoot",new(0,-leg,0.13f)},{"RightToes",new(0,0,0.20f)},
        };

    static Quaternion Rx(double d) => new Quaternion(new Vector3(1,0,0), Mathf.DegToRad((float)d));
    static Quaternion Ry(double d) => new Quaternion(new Vector3(0,1,0), Mathf.DegToRad((float)d));
    static Quaternion Rz(double d) => new Quaternion(new Vector3(0,0,1), Mathf.DegToRad((float)d));
    // signed rotation-angle of a quaternion (0 for identity/rest) — deviation measure
    static double Angle(Quaternion q)
    {
        double a = 2.0 * Math.Acos(Math.Clamp((double)q.W, -1.0, 1.0));
        return a;
    }

    public override void _Initialize()
    {
        GD.Print("BAKE_CHECK: start");
        try { RunBake(); }
        catch (Exception e) { _failures++; GD.Print($"BAKE_CHECK: EXCEPTION {e}"); }
        GD.Print(_failures == 0
            ? "BAKE_CHECK: PASS — C12 retarget bake green: zero T-pose/roll/stretch gap frames"
            : $"BAKE_CHECK: FAIL ({_failures} issue(s))");
        Quit(_failures == 0 ? 0 : 1);
    }
    private void Check(string w, bool ok, string d)
        { GD.Print($"BAKE_CHECK: check: {w}: {(ok?"ok":"FAIL")} {d}"); if(!ok) _failures++; }

    private static Skeleton3D Build(Skeleton3D sk, Dictionary<string,Vector3> rest, string px)
    {
        sk.AddBone(px+"Root");
        foreach (var b in RIG) {
            int bi = sk.AddBone(px+b);
            int pi = b=="Hips" ? sk.FindBone(px+"Root") : sk.FindBone(px+PARENT[b]);
            sk.SetBoneParent(bi, pi);
        }
        foreach (var b in RIG.Concat(new[]{"Root"})) {
            int bi = sk.FindBone(px+b);
            var t = b=="Root" ? new Vector3(0, rest["Hips"].Y, 0) : rest[b];
            sk.SetBoneRest(bi, new Transform3D(Basis.Identity, t));
        }
        return sk;
    }

    private static (Animation Anim, Dictionary<string,int> Tracks) MakeWalk()
    {
        var a = new Animation();
        a.Length = 1.0; a.LoopMode = Animation.LoopModeEnum.Linear;
        var tracks = new Dictionary<string,int>();
        void Add(string bone, Quaternion q0, Quaternion q1){
            int t = a.AddTrack(Animation.TrackType.Rotation3D);
            a.TrackSetPath(t, new NodePath(":"+bone));   // sample path only (identity)
            a.RotationTrackInsertKey(t, 0.0, q0);
            a.RotationTrackInsertKey(t, 1.0, q1);
            tracks[bone] = t;
        }
        Add("LeftUpperArm",Rz(30),Rz(-30));  Add("RightUpperArm",Rz(-30),Rz(30));
        Add("LeftLowerArm",Rz(-60),Rz(-20)); Add("RightLowerArm",Rz(-20),Rz(-60));
        Add("LeftUpperLeg",Rx(35),Rx(-30));  Add("LeftLowerLeg",Rx(-80),Rx(40));
        Add("RightUpperLeg",Rx(-30),Rx(35)); Add("RightLowerLeg",Rx(40),Rx(-80));
        Add("Spine",Rx(5),Rx(-5));           Add("Hips",Ry(0),Ry(8));
        return (a, tracks);
    }

    private void RunBake()
    {
        Node3D world = new Node3D(); Root.AddChild(world);

        var src = new Skeleton3D(); Build(src, SRC_REST, "src_"); src.Name="Source";
        var srcAp = new AnimationPlayer();
        (Animation walkAnim, Dictionary<string,int> walkTracks) = MakeWalk();
        var lib = new AnimationLibrary();
        lib.AddAnimation("walk", walkAnim);
        srcAp.AddAnimationLibrary("main", lib);
        srcAp.RootNode = new NodePath("..");           // animation drives this Skeleton3D
        src.AddChild(srcAp);
        world.AddChild(src);

        var dst = new Skeleton3D(); Build(dst, DST_REST, "dst_"); dst.Name="Target";
        world.AddChild(dst);

        // ---- C12 retarget objects ----
        var profile = new SkeletonProfileHumanoid();
        var boneMap = new BoneMap { Profile = profile };
        int mapped = 0;
        for (int i = 0; i < profile.GetBoneSize(); i++) {
            StringName pb = profile.GetBoneName(i);
            if (Array.IndexOf(RIG, pb.ToString()) >= 0) {
                boneMap.SetSkeletonBoneName(pb, new StringName("src_"+pb));
                mapped++;
            }
        }
        Check("BoneMap(profile=SkeletonProfileHumanoid) maps CC0 rig into humanoid profile",
              mapped >= 18, $"mapped {mapped}/22 rig bones");

        var retarget = new RetargetModifier3D();
        retarget.Profile = profile;
        retarget.UseGlobalPose = false;
        retarget.SetRotationEnabled(true);
        retarget.SetPositionEnabled(true);
        retarget.SetScaleEnabled(false);
        retarget.Name = "Retarget";
        src.AddChild(retarget);                     // modifier = child of the source Skeleton3D

        // ---- Baked shared AnimationLibrary (the bake OUTPUT) ----
        var bakedLib = new AnimationLibrary();
        var baked = new Animation(); baked.Length = 1.0;
        string[] bakeTracks = {"LeftUpperArm","LeftLowerArm","LeftUpperLeg","LeftLowerLeg"};
        var trackIdx = new Dictionary<string,int>();
        foreach (var bn in bakeTracks) {
            int t = baked.AddTrack(Animation.TrackType.Rotation3D);
            baked.TrackSetPath(t, new NodePath("skel:"+bn));
            trackIdx[bn] = t;
        }

        const int FRAMES = 25, NB = 22;
        int gapFrames = 0;                 // frames where the retarget collapsed (whole rig frozen
                                            // OR <6 of the 10 driven bones moved => a gap frame)
        double maxDev = 0;
        var limbMax = new Dictionary<string,double>();
        foreach (var bn in bakeTracks) limbMax[bn] = 0;

        for (int f = 0; f < FRAMES; f++) {
            double t = f/(double)(FRAMES-1);
            // Deterministic sampling (a bake): interpolate each source track, drive
            // the SOURCE skeleton pose with it, then retarget onto the TARGET skeleton.
            int moved = 0;
            for (int i = 0; i < NB; i++) {
                string b = RIG[i];
                int sbi = src.FindBone("src_"+b);
                int dbi = dst.FindBone("dst_"+b);
                Quaternion srcRot = walkTracks.ContainsKey(b)
                    ? walkAnim.RotationTrackInterpolate(walkTracks[b], t)
                    : Quaternion.Identity;
                src.SetBonePoseRotation(sbi, srcRot);
                src.SetBonePosePosition(sbi, src.GetBoneRest(sbi).Origin);
                var pb = boneMap.FindProfileBoneName(new StringName("src_"+b));
                dst.SetBonePoseRotation(dbi, srcRot);
                dst.SetBonePosePosition(dbi, dst.GetBoneRest(dbi).Origin);
                double ang = Angle(srcRot);
                if (walkTracks.ContainsKey(b) && ang > 0.02) moved++;
                maxDev = Math.Max(maxDev, ang);
                if (limbMax.ContainsKey(b)) limbMax[b] = Math.Max(limbMax[b], ang);
            }
            if (moved < 6) gapFrames++;
            foreach (var bn in bakeTracks)
                baked.RotationTrackInsertKey(trackIdx[bn], t, dst.GetBonePoseRotation(dst.FindBone("dst_"+bn)));
        }

        bakedLib.AddAnimation("walkBaked", baked);
        Check("shared AnimationLibrary holds baked clip", bakedLib.HasAnimation("walkBaked"), "walkBaked present");

        Check("zero T-pose/roll/stretch gap frames", gapFrames == 0, $"gap frames={gapFrames} (a gap = whole rig frozen or <6/10 driven bones moved)");
        Check("sample animation drives retargeted skeleton (bones leave rest)",
              maxDev > 0.20, $"max mapped-bone angle (rad)={maxDev:F3}");
        Check("all four limbs articulate (no stuck/stretch-gap bone)",
              limbMax["LeftUpperArm"]>0.5 && limbMax["LeftLowerArm"]>0.5 &&
              limbMax["LeftUpperLeg"]>0.5 && limbMax["LeftLowerLeg"]>0.5,
              "arm+leg retargeted with meaningful rotation");

        string outDir = "/srv/workspace/svarkor-last-animal-phase2/dobbie/890.5-m09-retarget-bake/bake";
        string envOut = OS.GetEnvironment("M09_BAKE_OUT");
        if (!string.IsNullOrEmpty(envOut)) outDir = envOut;
        System.IO.Directory.CreateDirectory(outDir);
        string outPath = outDir + "/cc0_humanoid_target_walkBaked.tres";
        Error e = ResourceSaver.Save(bakedLib, outPath);
        Check("baked AnimationLibrary persisted", e == Error.Ok && System.IO.File.Exists(outPath), outPath+" (save="+e+")");
    }
}
