#if PETANYONE_MOD_BUILD
using PetAnyone;
using Terraria.ModLoader;

namespace PetAnyoneAPI;

/// <summary>
/// Library mod entry point, present only in the dependency mod build. The namespace intentionally
/// starts with the mod internal name so the tModLoader namespace and folder name check passes
/// while the API types stay in the <c>PetAnyone</c> namespace for consumers.
/// </summary>
public sealed class PetAnyoneMod : Mod
{
    /// <summary>The live library instance, null when running from a frozen DLL.</summary>
    public static PetAnyoneMod? Instance { get; private set; }

    /// <summary>Captures the instance.</summary>
    public override void Load()
    {
        Instance = this;
    }

    /// <summary>Clears every static registry, handler, and state table, then releases the instance.</summary>
    public override void Unload()
    {
        PetEvents.Clear();
        PetRegistry.Clear();
        PetService.Clear();
        Instance = null;
    }

    /// <summary>Forwards Mod.Call to the typed API dispatcher.</summary>
    public override object Call(params object[] args)
    {
        return PettingApi.Call(args)!;
    }
}
#endif
