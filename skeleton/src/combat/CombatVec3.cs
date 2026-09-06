using System;

// Last Animal — M08 combat-3d (MC 890.13, artemis, 2026-09-06).
//
// M08 is the keystone integration phase (PHASE0.md Phase 10, C10+C11).
// Per invariant I3 ("domain pillars ... are PURE logic, separable from 3D
// presentation — testable without a window"), the combat module must be
// engine-free so `dotnet test` runs it headless. Godot's `Vector3` is not
// available to a standalone xunit project without pulling in GodotSharp,
// so the module uses its own plain 3D vector value type here.
//
// The C10 contract says `PlayerController.Move(Vec3)` — this is that Vec3:
// a minimal X/Y/Z float tuple with the operations the AI/controller need.
// The Godot layer (M08 scene) maps this to/from Godot.Vector3 at the seam,
// keeping I4 acyclic (engine types never leak into the pure domain).
namespace LastAnimal.Combat;

/// <summary>
/// Minimal engine-free 3D vector used by the combat pure-logic module.
/// </summary>
public struct CombatVec3 : IEquatable<CombatVec3>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public CombatVec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>Distance on the XZ plane (the ground plane for a 3D action game).</summary>
    public float DistanceXZ(CombatVec3 other)
    {
        float dx = other.X - X;
        float dz = other.Z - Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public static CombatVec3 operator +(CombatVec3 a, CombatVec3 b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static CombatVec3 operator -(CombatVec3 a, CombatVec3 b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static CombatVec3 operator *(CombatVec3 a, float s)
        => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>Direction vector towards another point on the XZ plane (normalized).</summary>
    public CombatVec3 DirectionOnXZ(CombatVec3 other)
    {
        var diff = other - this;
        diff.Y = 0f;
        float len = diff.Length();
        if (len < 1e-6f) return new CombatVec3(0f, 0f, 0f);
        return diff * (1f / len);
    }

    public bool Equals(CombatVec3 other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) =>
        obj is CombatVec3 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
