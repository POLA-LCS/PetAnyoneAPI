using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>
/// Hooks other code (and consumer mods) can subscribe to. Every registration is tied to an owning
/// <see cref="Mod"/> and is pruned lazily once that mod is no longer loaded. <c>CanPet</c>
/// handlers run in registration order and any handler returning false cancels the pet.
/// </summary>
public static class PetEvents
{
    private static readonly List<OwnedHandler<Func<PetContext, bool>>> canPetHandlers = new();
    private static readonly List<OwnedHandler<Action<PetContext>>> petStartHandlers = new();
    private static readonly List<OwnedHandler<Action<PetContext>>> petHoldHandlers = new();
    private static readonly List<OwnedHandler<Action<PetContext>>> petEndHandlers = new();
    private static readonly List<OwnedHandler<Func<PetTarget, float?>>> reachAngleProviders = new();

    /// <summary>Adds a cancellable check; return false to prevent petting the target.</summary>
    public static void RegisterCanPet(Mod owner, Func<PetContext, bool> handler) => Add(canPetHandlers, owner, handler);

    /// <summary>Called once when a tap pet is applied.</summary>
    public static void RegisterOnPetStart(Mod owner, Action<PetContext> handler) => Add(petStartHandlers, owner, handler);

    /// <summary>Called for each hold refresh, which bypasses the tap cooldown.</summary>
    public static void RegisterOnPetHold(Mod owner, Action<PetContext> handler) => Add(petHoldHandlers, owner, handler);

    /// <summary>Called when the local player stops petting a target.</summary>
    public static void RegisterOnPetEnd(Mod owner, Action<PetContext> handler) => Add(petEndHandlers, owner, handler);

    /// <summary>Optional angle override for the reach arm; return null to defer to other providers.</summary>
    public static void RegisterReachAngle(Mod owner, Func<PetTarget, float?> provider) => Add(reachAngleProviders, owner, provider);

    /// <summary>Registers every non-null callback in a bundle.</summary>
    public static void RegisterCallbacks(Mod owner, PetEventCallbacks? callbacks)
    {
        if (callbacks is null)
            return;
        if (callbacks.CanPet is not null)
            RegisterCanPet(owner, callbacks.CanPet);
        if (callbacks.OnPetStart is not null)
            RegisterOnPetStart(owner, callbacks.OnPetStart);
        if (callbacks.OnPetHold is not null)
            RegisterOnPetHold(owner, callbacks.OnPetHold);
        if (callbacks.OnPetEnd is not null)
            RegisterOnPetEnd(owner, callbacks.OnPetEnd);
    }

    /// <summary>Runs every CanPet handler; false from any handler cancels the pet.</summary>
    public static bool CanPet(PetContext context)
    {
        for (int i = 0; i < canPetHandlers.Count; i++)
        {
            OwnedHandler<Func<PetContext, bool>> entry = canPetHandlers[i];
            if (!PetRegistry.IsOwnerLoaded(entry.Owner))
            {
                canPetHandlers.RemoveAt(i);
                i--;
                continue;
            }
            if (!entry.Handler(context))
                return false;
        }
        return true;
    }

    public static void RaisePetStart(PetContext context) => Raise(petStartHandlers, context);

    public static void RaisePetHold(PetContext context) => Raise(petHoldHandlers, context);

    public static void RaisePetEnd(PetContext context) => Raise(petEndHandlers, context);

    /// <summary>The first live provider that returns an angle wins.</summary>
    public static bool TryGetReachAngle(PetTarget target, out float angle)
    {
        angle = 0f;
        for (int i = 0; i < reachAngleProviders.Count; i++)
        {
            OwnedHandler<Func<PetTarget, float?>> entry = reachAngleProviders[i];
            if (!PetRegistry.IsOwnerLoaded(entry.Owner))
            {
                reachAngleProviders.RemoveAt(i);
                i--;
                continue;
            }
            float? result = entry.Handler(target);
            if (result.HasValue)
            {
                angle = result.Value;
                return true;
            }
        }
        return false;
    }

    /// <summary>Drops every handler; call this when the consuming mod unloads.</summary>
    public static void Clear()
    {
        canPetHandlers.Clear();
        petStartHandlers.Clear();
        petHoldHandlers.Clear();
        petEndHandlers.Clear();
        reachAngleProviders.Clear();
    }

    private static void Add<T>(List<OwnedHandler<T>> list, Mod owner, T handler)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        list.Add(new OwnedHandler<T>(owner, handler));
    }

    private static void Raise(List<OwnedHandler<Action<PetContext>>> list, PetContext context)
    {
        for (int i = 0; i < list.Count; i++)
        {
            OwnedHandler<Action<PetContext>> entry = list[i];
            if (!PetRegistry.IsOwnerLoaded(entry.Owner))
            {
                list.RemoveAt(i);
                i--;
                continue;
            }
            entry.Handler(context);
        }
    }

    private readonly record struct OwnedHandler<T>(Mod Owner, T Handler);
}
