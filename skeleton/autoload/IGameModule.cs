// Last Animal — M01 core-framework (MC 839.3, teddy, 2026-09-01).
//
// IM01Module: the seam every gameplay module implements so GameBootstrap can
// bind it and order its boot (C3). Deliberately tiny — the framework only
// needs an entry point + a name for logging/order. All logic stays in the
// module's own classes; this interface is orchestration-only (I1).
namespace LastAnimal.Core;

/// <summary>
/// A registered game module. Implementers keep all logic in their own module
/// classes and expose only this surface to the composition root.
/// </summary>
public interface IGameModule
{
    /// <summary>Stable display name (used in boot-order logs; must be unique).</summary>
    string Name { get; }

    /// <summary>Called once, in registration order, when the game boots.</summary>
    void Boot();
}
