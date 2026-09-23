using Terraria.ModLoader;

#nullable enable

namespace PetAnyone;

/// <summary>Post event: an active session closed. Every start is paired with exactly one end.</summary>
public sealed class PetEndEvent : PetEvent
{
    /// <summary>Creates the payload.</summary>
    public PetEndEvent(PetContext context, PetEventSource source, PetEndReason reason, Mod? issuer = null)
        : base(context, source, issuer)
    {
        Reason = reason;
    }

    /// <summary>Why the session ended.</summary>
    public PetEndReason Reason { get; }
}
