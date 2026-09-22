using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>
/// Visual hook: a heart opportunity for one target. Raised on clients only, after the shared
/// per target throttle. Set Handled to suppress the built in heart and provide your own.
/// </summary>
public sealed class PetHeartEvent : PetEvent
{
    /// <summary>Creates the payload.</summary>
    public PetHeartEvent(PetContext context, PetEventSource source, Mod? issuer = null)
        : base(context, source, issuer)
    {
    }

    /// <summary>Set true to take over the visual and suppress the default heart. Sticky.</summary>
    public bool Handled { get; set; }

    /// <summary>True when the library spawned the built in heart for this opportunity.</summary>
    public bool DefaultSpawned { get; internal set; }
}
