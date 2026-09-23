using Terraria;
using Terraria.ModLoader;

#nullable enable

namespace PetAnyone;

/// <summary>
/// Base payload for every pet hook. Carries the pet context, where the application came from,
/// an optional issuing Mod for manual raises, and the game update tick of the raise.
/// </summary>
public abstract class PetEvent
{
    /// <summary>Creates the payload and stamps the current game update tick.</summary>
    protected PetEvent(PetContext context, PetEventSource source, Mod? issuer = null)
    {
        Context = context;
        Source = source;
        Issuer = issuer;
        Tick = (long)Main.GameUpdateCount;
    }

    /// <summary>The patter and target the event is about.</summary>
    public PetContext Context { get; }

    /// <summary>Local, Synced, or Manual.</summary>
    public PetEventSource Source { get; }

    /// <summary>The mod that raised the event manually, null when the library raised it.</summary>
    public Mod? Issuer { get; }

    /// <summary>Main.GameUpdateCount at the moment the event was created.</summary>
    public long Tick { get; }

    /// <summary>Convenience for Context.Patter. Can be null for a synced replay.</summary>
    public Player? Patter => Context.Patter;

    /// <summary>Convenience for Context.Target.</summary>
    public PetTarget Target => Context.Target;

    /// <summary>True when Source is Local.</summary>
    public bool IsLocal => Source == PetEventSource.Local;

    /// <summary>True when Source is Synced.</summary>
    public bool IsSynced => Source == PetEventSource.Synced;

    /// <summary>True when the target still exists and is active.</summary>
    public bool IsValid => Target.IsActive;
}
