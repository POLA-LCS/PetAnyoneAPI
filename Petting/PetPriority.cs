#nullable enable

namespace PetAnyone;

/// <summary>Ordering weight shared by event handlers and registry entries. Lower runs first.</summary>
public enum PetPriority : byte
{
    /// <summary>Runs before Normal.</summary>
    High = 0,
    /// <summary>Default ordering.</summary>
    Normal = 100,
    /// <summary>Runs after Normal.</summary>
    Low = 200
}
