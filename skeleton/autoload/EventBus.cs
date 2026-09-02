using Godot;
using LastAnimal.Core.Framework;

// Last Animal — M01 core-framework (MC 839.3, teddy, 2026-09-01).
//
// EventBus: the single global signal bus (C2). Autoload (I1: orchestration only).
// Modules NEVER reference each other directly (I4); they publish/subscribe here.
// One signal per contract line — the framework carries the carriers, the
// gameplay modules interpret them.
//
// M09 repair (MC 839.4, henrik, 2026-09-01) — C# signal idiom:
// Godot 4.7.2's C# code generator rejects [Signal] parameters that are user
// types (GD0202: "parameter ... is not supported" — only built-in value types,
// strings, Node references, Variant and Array<T> are allowed). It ALSO
// generates a nested static class `SignalName` on any node that declares
// [Signal]s, which collided with the hand-written `public static class
// SignalName` below (CS0102 + CS0713).
//
// Repair, keeping the C2 contract surface (the same five events, same
// argument order, same stable string names — C2 is unchanged; this is a C#
// idiom fix, not a contract change):
//   1. Each record carrier is passed as a `string` Id in the C# signal
//      signature. The record stays the canonical carrier in C# code: the
//      emit helpers below keep taking the record and extract its Id for the
//      signal, and subscribers can wrap the Id back into a record (Empty
//      factory included) — the event's payload is the same data, only its
//      wire form on the C# signal is the identifier string Godot requires.
//   2. The hand-written `SignalName` nested class was REMOVED: the source
//      generator now provides it. All usages were updated to the generated
//      members. (Verified by a compile of the whole project after the change.)
namespace LastAnimal.Core;

public partial class EventBus : Node
{
    // --- C2: global signals (single pub/sub) --------------------------------
    // [signal] DnaExtracted(String)          — a DNA signature was extracted (M02).
    // [signal] DnaSpoken(String)             — a learned signature was spoken (M02).
    // [signal] LoyaltyChanged(String, int)   — companion loyalty moved (M03).
    // [signal] Betrayal(String, String)      — a companion turned (M03).
    // [signal] EcosystemAdapted(String)      — ecosystem adapted to the player (C5).
    // EmpathyBookOpened() arrives with M04/M10 (not in the M01 C2 set).
    // (String = the carrier record's Id, see file header.)

    [Signal] public delegate void DnaExtractedEventHandler(string signature);
    [Signal] public delegate void DnaSpokenEventHandler(string signature);
    [Signal] public delegate void LoyaltyChangedEventHandler(string companion, int loyalty);
    [Signal] public delegate void BetrayalEventHandler(string companion, string target);
    [Signal] public delegate void EcosystemAdaptedEventHandler(string mutation);

    // --- C2: publish surface (thin, no logic) --------------------------------

    public void EmitDnaExtracted(DnaSignature signature)
        => EmitSignal(SignalName.DnaExtracted, signature.Id);

    public void EmitDnaSpoken(DnaSignature signature)
        => EmitSignal(SignalName.DnaSpoken, signature.Id);

    public void EmitLoyaltyChanged(CompanionId companion, int loyalty)
        => EmitSignal(SignalName.LoyaltyChanged, companion.Id, loyalty);

    public void EmitBetrayal(CompanionId companion, TargetId target)
        => EmitSignal(SignalName.Betrayal, companion.Id, target.Id);

    public void EmitEcosystemAdapted(MutationId mutation)
        => EmitSignal(SignalName.EcosystemAdapted, mutation.Id);
}
