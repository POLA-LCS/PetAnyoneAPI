#if !PETANYONE_DLL_BUILD
using PetAnyone;
using Terraria.ModLoader;

#nullable enable

namespace PetAnyoneAPI;

/// <summary>
/// Library lifecycle hooks, present only in the dependency mod build. Owns automatic cleanup on
/// unload, world state resets on world transitions, and the per tick session pump.
/// </summary>
public sealed class PetAnyoneSystem : ModSystem
{
    /// <summary>Clears all library state on unload as a second safety net after the mod unload.</summary>
    public override void Unload()
    {
        PetEvents.Clear();
        PetRegistry.Clear();
        PetService.Clear();
    }

    /// <summary>Resets stray session and tick state before a world becomes live.</summary>
    public override void OnWorldLoad()
    {
        PetService.ResetWorldState();
    }

    /// <summary>Raises end events for active sessions with the WorldUnload reason, then resets world state.</summary>
    public override void OnWorldUnload()
    {
        PetService.EndAllSessions(PetEndReason.WorldUnload);
        PetService.ResetWorldState();
    }

    /// <summary>Pumps the session expiry and stale state sweep once per game update.</summary>
    public override void PostUpdateEverything()
    {
        PetService.Tick();
    }
}
#endif
