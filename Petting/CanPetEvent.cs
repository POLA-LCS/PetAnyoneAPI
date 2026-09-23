using Terraria.ModLoader;

#nullable enable

namespace PetAnyone;

/// <summary>Pre event: raised before a local pet is accepted. Set Cancel to veto.</summary>
public sealed class CanPetEvent : PetEvent
{
    /// <summary>Creates the payload.</summary>
    public CanPetEvent(PetContext context, PetEventSource source = PetEventSource.Manual, Mod? issuer = null)
        : base(context, source, issuer)
    {
    }

    /// <summary>Set true to reject the pet. Sticky, and remaining handlers do not run after a cancel.</summary>
    public bool Cancel { get; set; }

    /// <summary>Optional diagnostic text attached to the cancellation.</summary>
    public string? RejectionReason { get; set; }
}
