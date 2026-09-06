using System;

// Last Animal — M05 companion-system (MC 890.12, bernie, 2026-09-06).
//
// The M09 animation hook: a pure mapping from companion state + needs to a
// clip name that an M09-retargeted rig's AnimationPlayer can play. It does
// not touch the Godot AnimationPlayer / AnimationLibrary / Skeleton3D here —
// it returns the *clip name* the presentation layer feeds the rig, so the
// logic layer stays headless-testable. (The Phase-9 DoD's 'animates via the
// retargeted rig' arm is proven by wiring this hook to M09's baked library
// in a headless render; the mapping itself is unit-tested below.)
namespace LastAnimal.Companion;

/// <summary>
/// A per-companion animation hook the M09 retargeted rig consumes. Maps the
/// companion's current behavioral state to a clip in the shared
/// AnimationLibrary that M09 produced (e.g. `walkBaked`).
/// </summary>
public class CompanionAnimationHook
{
    /// <summary>Fallback clip name if the rig lacks the state's preferred clip.</summary>
    public string FallbackClip { get; set; }

    /// <summary>Clip name for the Following state (trailing the player).</summary>
    public string FollowClip { get; set; }

    /// <summary>Clip name for the Needing state (the -unpaid- wait).</summary>
    public string NeedClip { get; set; }

    /// <summary>Clip name for the Betrayed state.</summary>
    public string BetrayedClip { get; set; }

    public CompanionAnimationHook(
        string followClip,
        string needClip,
        string betrayedClip,
        string fallbackClip = "RESET")
    {
        FollowClip = followClip;
        NeedClip = needClip;
        BetrayedClip = betrayedClip;
        FallbackClip = fallbackClip;
    }

    /// <summary>Resolve the clip for a given companion state (pure mapping).</summary>
    public string Resolve(CompanionState state)
    {
        return state switch
        {
            CompanionState.Following => FollowClip,
            CompanionState.Needing => NeedClip,
            CompanionState.Betrayed => BetrayedClip,
            _ => FallbackClip,
        };
    }

    /// <summary>Convenience: resolve from a state machine (reads its State).</summary>
    public string Resolve(CompanionStateMachine machine) => Resolve(machine.State);

    public override string ToString() =>
        $"CompanionAnimationHook(follow={FollowClip}, need={NeedClip}, betrayed={BetrayedClip})";
}
