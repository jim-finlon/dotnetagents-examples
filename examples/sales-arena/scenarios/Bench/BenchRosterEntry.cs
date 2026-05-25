namespace SalesArena.Bench;

/// <summary>
/// Where a persona sits relative to the active sales floor.
/// </summary>
public enum BenchSlot
{
    /// <summary>On the active sales floor for the current contest.</summary>
    Active = 1,
    /// <summary>In reserve, waiting for a call-up.</summary>
    Reserve = 2,
}

/// <summary>
/// A persona's roster entry. The bench manager owns these in slot-specific
/// FIFO order (oldest reserve persona evicts first).
/// </summary>
public sealed record BenchRosterEntry(
    string Persona,
    BenchSlot Slot,
    DateTimeOffset JoinedSlotAtUtc,
    int ConsecutiveContestsBelowThreshold);
