using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>Post event: an open session received another hold refresh.</summary>
public sealed class PetHoldEvent : PetEvent
{
    /// <summary>Creates the payload.</summary>
    public PetHoldEvent(PetContext context, PetEventSource source, Mod? issuer = null)
        : base(context, source, issuer)
    {
    }
}
