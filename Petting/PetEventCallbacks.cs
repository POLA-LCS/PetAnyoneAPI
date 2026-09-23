using System;

#nullable enable

namespace PetAnyone;

/// <summary>
/// Optional callback bundle, convenient for passing several pet hooks at once through
/// <see cref="PettingApi.RegisterEvents"/> or <c>Mod.Call("RegisterEvents", mod, callbacks)</c>.
/// Null callbacks are simply not registered.
/// </summary>
public sealed class PetEventCallbacks
{
    /// <summary>Return false to cancel petting the target.</summary>
    public Func<PetContext, bool>? CanPet { get; init; }

    /// <summary>Called once when a tap pet is applied.</summary>
    public Action<PetContext>? OnPetStart { get; init; }

    /// <summary>Called for each hold refresh, which bypasses the tap cooldown.</summary>
    public Action<PetContext>? OnPetHold { get; init; }

    /// <summary>Called when the local player stops petting a target.</summary>
    public Action<PetContext>? OnPetEnd { get; init; }
}
