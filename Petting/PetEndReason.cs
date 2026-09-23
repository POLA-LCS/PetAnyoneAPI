#nullable enable

namespace PetAnyone;

/// <summary>Why an active pet session ended.</summary>
public enum PetEndReason : byte
{
    /// <summary>The patter released the target.</summary>
    Released,
    /// <summary>No hold refresh arrived before the timeout.</summary>
    Timeout,
    /// <summary>The target became invalid or inactive.</summary>
    TargetLost,
    /// <summary>A new session replaced the previous session.</summary>
    Replaced,
    /// <summary>The world unloaded and all sessions were closed.</summary>
    WorldUnload,
    /// <summary>A consumer ended the session through the API.</summary>
    Manual
}
