using Godot;

// Last Animal — M01 core-framework (MC 839.3, teddy, 2026-09-01).
//
// Signal carrier types for the C2 EventBus. They are the "small carrier types"
// the C2 contract names — defined as plain data so any module can use them
// without pulling in gameplay logic (I4: no cross-module refs).
//
// WHY Resource (not record): Godot [Signal] parameters must be Godot-REGISTERED
// types (GD0202 rejects plain C# value types / records). A Godot `Resource`
// subclass IS registered, so it can flow through a [Signal] delegate. These
// carry the identity M01 needs; the concrete DNA-language model (C4: the
// codon sequence) belongs to M02. CompanionId / TargetId are string-keyed
// because PHASE0.md names them by id (C2).
namespace LastAnimal.Core.Framework;

/// <summary>
/// A DNA signature as observed by the world (C4 carrier). M02 fills in the
/// real codon sequence; the framework only carries the identifying data.
/// </summary>
public partial class DnaSignature : Resource
{
    [Export] public string Id = "<empty>";
    [Export] public string Entity = "<none>";

    public DnaSignature() { }
    public DnaSignature(string id, string entity) { Id = id; Entity = entity; }

    public override string ToString() => $"DnaSignature({Id} of {Entity})";
}

/// <summary>A companion reference (C2: LoyaltyChanged / Betrayal). String-keyed id.</summary>
public partial class CompanionId : Resource
{
    [Export] public string Id = "<none>";

    public CompanionId() { }
    public CompanionId(string id) { Id = id; }

    public override string ToString() => $"CompanionId({Id})";
}

/// <summary>A generic target reference (C2: Betrayal(CompanionId, TargetId)).</summary>
public partial class TargetId : Resource
{
    [Export] public string Id = "<none>";

    public TargetId() { }
    public TargetId(string id) { Id = id; }

    public override string ToString() => $"TargetId({Id})";
}

/// <summary>An ecosystem mutation (C2: EcosystemAdapted(MutationId)); C5 feeds it.</summary>
public partial class MutationId : Resource
{
    [Export] public string Id = "<none>";

    public MutationId() { }
    public MutationId(string id) { Id = id; }

    public override string ToString() => $"MutationId({Id})";
}
