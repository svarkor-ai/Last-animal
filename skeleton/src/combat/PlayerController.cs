using System;

// Last Animal — M08 combat-3d PlayerController (MC 890.13, artemis, 2026-09-06).
//
// C10 (PHASE0.md line 379): `PlayerController` exposes `Move(Vec3)`/`State`.
// Engine-free (I3): this is the pure-logic player controller model, testable
// under `dotnet test` without a window. The Godot scene (M08) maps this to a
// CharacterBody3D + isometric Camera3D at the seam (I4 — engine types do not
// leak into the pure module).
//
// `Move(Vec3)` drives the controller one frame: it updates position from the
// requested movement direction and the controller's speed, then derives the
// player state (Idle when not moving, Run when moving). Health/mana simulate
// the HP/damage surface the HUD (M10) will bind.
namespace LastAnimal.Combat;

/// <summary>
/// Pure-logic player controller (C10). Exposes Move(Vec3) and a State enum.
/// </summary>
public class PlayerController
{
    public enum State
    {
        Idle,
        Run,
        Attack,
        Dead
    }

    public CombatVec3 Position { get; private set; }
    public State CurrentState { get; private set; } = State.Idle;
    public int Health { get; private set; } = 100;
    public int MaxHealth { get; } = 100;
    public float Speed { get; } = 5.0f;
    public float AttackRange { get; } = 1.5f;
    public int MeleeDamage { get; } = 20;

    public bool IsDead => Health <= 0;

    /// <summary>Set of enemies currently within melee reach of the player.</summary>
    private readonly System.Collections.Generic.HashSet<int> _engaged = new();

    public PlayerController(CombatVec3 start, float speed = 5.0f)
    {
        Position = start;
        Speed = speed;
    }

    /// <summary>
    /// Move one frame by a movement direction (length-agnostic; speed applied
    /// internally). Returns the new position. Dead players cannot move.
    /// </summary>
    public CombatVec3 Move(CombatVec3 direction, float frameTime)
    {
        if (IsDead) return Position;

        float len = direction.Length();
        if (len <= 1e-6f)
        {
            CurrentState = State.Idle;
            return Position;
        }

        Position += direction * (1f / len) * (Speed * frameTime);
        CurrentState = State.Run;
        return Position;
    }

    /// <summary>Apply incoming damage (e.g. from an enemy attack).</summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        Health = Math.Max(0, Health - amount);
        if (Health == 0) CurrentState = State.Dead;
    }

    /// <summary>
    /// Restore health (MC 1348 N1 save/load recovery): the F9 load path uses
    /// this to rescue a dead player — a restored live HP value clears IsDead
    /// so Move works again. Clamped to MaxHealth; a live player keeps its
    /// current state.
    /// </summary>
    public void RestoreHealth(int health)
    {
        Health = Math.Min(MaxHealth, Math.Max(0, health));
        if (!IsDead && CurrentState == State.Dead) CurrentState = State.Idle;
    }

    /// <summary>Track that an enemy is within melee reach (engaged).</summary>
    public void EngageEnemy(int entityId) => _engaged.Add(entityId);

    /// <summary>Number of enemies currently engaged in melee reach.</summary>
    public int EngagedCount => _engaged.Count;
}
