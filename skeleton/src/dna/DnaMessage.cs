using System;
using System.Threading;

// Last Animal — M02 dna-language-combat (MC 890.2, dobbie, 2026-09-03).
//
// Port of /srv/workspace/animal/src/Animal.Gameplay/DnaMessage.cs. Pure data
// carrier: no Godot types, no window — runs headless under `dotnet test` (I3).
//
// Port notes (semantics preserved 1:1 from the prior art):
//   - MessageId is a monotonically increasing, lock-guarded counter (same as
//     the source's Interlocked.Increment pattern).
//   - Payload is a nucleotide array (0-3: A,C,G,T); null input becomes an
//     empty array (same guard as the source).
//   - CodonCount = (Length + 2) / 3 (ceil to codons of 3).
//   - CounterStrength is settable only by the DnaLanguage.Counter path
//     (internal setter, same as the source).
//   - PayloadAsString renders A/C/G/T, '?' for anything else.
namespace LastAnimal.Dna;

/// <summary>
/// A DNA language message produced by an entity speaking its language signature.
/// Contains a nucleotide payload and routing info (source/target entity IDs).
/// </summary>
public class DnaMessage
{
    private static long _nextId;
    private static readonly object _idLock = new();

    /// <summary>Unique message identifier (monotonically increasing).</summary>
    public long MessageId { get; }

    /// <summary>The entity that created this message.</summary>
    public int SourceEntityId { get; }

    /// <summary>Optional target entity (null = broadcast).</summary>
    public int? TargetEntityId { get; }

    /// <summary>The nucleotide payload (values 0-3: A,C,G,T).</summary>
    public int[] Payload { get; }

    /// <summary>Number of codons (groups of 3 nucleotides) in the payload.</summary>
    public int CodonCount => (Payload.Length + 2) / 3;

    /// <summary>Counter strength (0-1). Set when this message is a counter-response.</summary>
    public float CounterStrength { get; internal set; }

    /// <summary>When this message was created.</summary>
    public DateTime Timestamp { get; }

    public DnaMessage(int sourceEntityId, int[] payload, int? targetEntityId = null)
    {
        lock (_idLock)
        {
            MessageId = Interlocked.Increment(ref _nextId);
        }
        SourceEntityId = sourceEntityId;
        Payload = payload ?? Array.Empty<int>();
        TargetEntityId = targetEntityId;
        Timestamp = DateTime.UtcNow;
        CounterStrength = 0f;
    }

    /// <summary>Render the payload as base letters (A/C/G/T).</summary>
    public string PayloadAsString()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var n in Payload)
            sb.Append(n switch { 0 => 'A', 1 => 'C', 2 => 'G', 3 => 'T', _ => '?' });
        return sb.ToString();
    }

    public override string ToString() =>
        $"DnaMessage[{MessageId}] Source={SourceEntityId}" +
        (TargetEntityId.HasValue ? $" Target={TargetEntityId}" : "") +
        $" Payload={PayloadAsString()}";
}
