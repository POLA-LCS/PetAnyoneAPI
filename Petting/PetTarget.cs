using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Terraria;

namespace PetAnyone;

/// <summary>
/// A lightweight handle to anything the petting API can target. It points at a player or an NPC
/// by index, so it stays valid to copy, store in timers and compare without keeping an entity
/// reference alive.
/// </summary>
public readonly struct PetTarget : IEquatable<PetTarget>
{
    /// <summary>Empty target, used as the "nothing" value.</summary>
    public static readonly PetTarget None = new(PetTargetKind.Player, -1);

    public PetTarget(PetTargetKind kind, int index)
    {
        Kind = kind;
        Index = index;
    }

    /// <summary>Whether this handle points at a player or an NPC.</summary>
    public PetTargetKind Kind { get; }

    /// <summary>Player index (<c>whoAmI</c>) or NPC index.</summary>
    public int Index { get; }

    public bool IsPlayer => Kind == PetTargetKind.Player;

    public bool IsNpc => Kind == PetTargetKind.Npc;

    /// <summary>True when the index resolves to an entity in its array (entity may be inactive).</summary>
    public bool IsValid => TryGetEntity(out _);

    /// <summary>True when the target exists and is active in the world.</summary>
    public bool IsActive => TryGetEntity(out Entity? entity) && entity.active;

    /// <summary>True for an active, living target: players are not dead, NPCs still have life.</summary>
    public bool IsAlive => Kind == PetTargetKind.Player
        ? TryGetPlayer(out Player? player) && player.active && !player.dead
        : TryGetNpc(out NPC? npc) && npc.active && npc.life > 0;

    /// <summary>The resolved entity, or null when the handle is stale.</summary>
    public Entity? AsEntity => TryGetEntity(out Entity? entity) ? entity : null;

    public Vector2 Center => AsEntity?.Center ?? Vector2.Zero;

    public Rectangle Hitbox => AsEntity?.Hitbox ?? Rectangle.Empty;

    /// <summary>Player name or NPC given/type name; empty when the target is stale.</summary>
    public string DisplayName => Kind == PetTargetKind.Player
        ? (TryGetPlayer(out Player? player) ? player.name : string.Empty)
        : (TryGetNpc(out NPC? npc) ? npc.GivenOrTypeName : string.Empty);

    public bool TryGetPlayer([NotNullWhen(true)] out Player? player)
    {
        if (Kind != PetTargetKind.Player)
        {
            player = null;
            return false;
        }

        player = (uint)Index < (uint)Main.player.Length ? Main.player[Index] : null;
        return player is not null;
    }

    public bool TryGetNpc([NotNullWhen(true)] out NPC? npc)
    {
        if (Kind != PetTargetKind.Npc)
        {
            npc = null;
            return false;
        }

        npc = (uint)Index < (uint)Main.npc.Length ? Main.npc[Index] : null;
        return npc is not null;
    }

    public bool TryGetEntity([NotNullWhen(true)] out Entity? entity)
    {
        if (Kind == PetTargetKind.Player)
        {
            if (TryGetPlayer(out Player? player))
            {
                entity = player;
                return true;
            }
        }
        else if (TryGetNpc(out NPC? npc))
        {
            entity = npc;
            return true;
        }

        entity = null;
        return false;
    }

    public static PetTarget FromPlayer(Player? player) => player is null ? None : new PetTarget(PetTargetKind.Player, player.whoAmI);

    public static PetTarget FromNpc(NPC? npc) => npc is null ? None : new PetTarget(PetTargetKind.Npc, npc.whoAmI);

    public bool Equals(PetTarget other) => Kind == other.Kind && Index == other.Index;

    public override bool Equals(object? obj) => obj is PetTarget other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Index);

    public static bool operator ==(PetTarget left, PetTarget right) => left.Equals(right);

    public static bool operator !=(PetTarget left, PetTarget right) => !left.Equals(right);

    public override string ToString() => Kind == PetTargetKind.Player ? $"Player[{Index}]" : $"Npc[{Index}]";
}
