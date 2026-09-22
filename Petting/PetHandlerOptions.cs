namespace PetAnyone;

/// <summary>Subscription options for PetEvents.On* methods.</summary>
public sealed class PetHandlerOptions
{
    /// <summary>Shared defaults: Normal priority, no kind restriction, no filter.</summary>
    public static readonly PetHandlerOptions Default = new();

    /// <summary>Dispatch order. High runs before Normal before Low.</summary>
    public PetPriority Priority { get; init; } = PetPriority.Normal;

    /// <summary>When set, the handler only runs for this target kind.</summary>
    public PetTargetKind? TargetKind { get; init; }

    /// <summary>Optional per target filter. Runs before the handler. Exceptions log and skip the handler.</summary>
    public Func<PetTarget, bool>? Filter { get; init; }
}
