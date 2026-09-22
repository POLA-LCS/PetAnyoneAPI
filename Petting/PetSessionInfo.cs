using Terraria;

namespace PetAnyone;

/// <summary>Immutable snapshot of one active pet session.</summary>
public readonly record struct PetSessionInfo(
    PetTarget Target,
    Player? Patter,
    PetEventSource Source,
    long StartedTick,
    long LastRefreshTick);
