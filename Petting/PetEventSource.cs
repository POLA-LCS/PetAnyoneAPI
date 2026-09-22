namespace PetAnyone;

/// <summary>Where a pet application came from.</summary>
public enum PetEventSource : byte
{
    /// <summary>A local player initiated the pet on this machine.</summary>
    Local,
    /// <summary>A synced replay of a remote application.</summary>
    Synced,
    /// <summary>A mod raised the application explicitly through the API.</summary>
    Manual
}
