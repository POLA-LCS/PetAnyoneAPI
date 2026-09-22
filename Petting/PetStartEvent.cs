using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>Post event: a pet session opened, either a tap or the first hold application.</summary>
public sealed class PetStartEvent : PetEvent
{
    /// <summary>Creates the payload.</summary>
    public PetStartEvent(PetContext context, PetEventSource source, PetApplyMode mode, Mod? issuer = null)
        : base(context, source, issuer)
    {
        Mode = mode;
    }

    /// <summary>Tap or Hold input that opened the session.</summary>
    public PetApplyMode Mode { get; }

    /// <summary>True when a hold opened the session.</summary>
    public bool IsHoldStart => Mode == PetApplyMode.Hold;
}
