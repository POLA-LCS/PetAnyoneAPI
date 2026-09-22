namespace PetAnyone;

/// <summary>Machine readable outcome code for TryApplyPet.</summary>
public enum PetApplyCode : byte
{
    /// <summary>The application succeeded.</summary>
    Applied,
    /// <summary>The target does not exist or is not active.</summary>
    RejectedTarget,
    /// <summary>No registry rule allows petting the target.</summary>
    RejectedRegistry,
    /// <summary>The target is out of reach.</summary>
    RejectedRange,
    /// <summary>A CanPet handler vetoed the application.</summary>
    RejectedCancelled,
    /// <summary>The per target cooldown absorbed the application.</summary>
    RejectedCooldown,
    /// <summary>The target kind is not supported by this path.</summary>
    RejectedUnsupported
}

/// <summary>Outcome of a pet application. Check Applied or IsApplied.</summary>
public readonly record struct PetApplyResult(bool Applied, PetApplyCode Code)
{
    /// <summary>Shared success value.</summary>
    public static readonly PetApplyResult Success = new(true, PetApplyCode.Applied);

    /// <summary>True when the pet was applied.</summary>
    public bool IsApplied => Applied;

    /// <summary>True when the pet was rejected.</summary>
    public bool IsRejected => !Applied;

    /// <summary>Creates a rejected result with the given code.</summary>
    public static PetApplyResult Reject(PetApplyCode code) => new(false, code);

    /// <summary>Human readable summary.</summary>
    public override string ToString() => Applied ? "Applied" : $"Rejected:{Code}";
}
