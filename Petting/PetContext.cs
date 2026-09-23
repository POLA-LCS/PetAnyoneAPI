using Terraria;

#nullable enable

namespace PetAnyone;

/// <summary>
/// What a pet event is about: the player doing the petting and the target being petted. The
/// patter can be null for a synced application that has no local player behind it.
/// </summary>
public readonly struct PetContext(Player? patter, PetTarget target)
{
    /// <summary>The player performing the pet, or null when unavailable.</summary>
    public Player? Patter { get; } = patter;

    /// <summary>The player or NPC being petted.</summary>
    public PetTarget Target { get; } = target;

    /// <summary>True while the target still exists and is active.</summary>
    public bool IsValid => Target.IsActive;
}
