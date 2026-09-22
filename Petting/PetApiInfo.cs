namespace PetAnyone;

/// <summary>Immutable API identity returned by Call("GetApi") and PettingApi.Info.</summary>
public sealed class PetApiInfo(int major, int minor, int patch, string versionString, string modName)
{
    /// <summary>Major API version, negotiated through Call.</summary>
    public int Major { get; } = major;

    /// <summary>Minor API version, additive changes only.</summary>
    public int Minor { get; } = minor;

    /// <summary>Patch version, documentation and fixes only.</summary>
    public int Patch { get; } = patch;

    /// <summary>Version string, for example "2.0.0".</summary>
    public string VersionString { get; } = versionString;

    /// <summary>tModLoader internal name for ModLoader.GetMod, "PetAnyoneAPI".</summary>
    public string ModName { get; } = modName;
}
