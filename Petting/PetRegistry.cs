using System.Diagnostics.CodeAnalysis;
using Terraria;
using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>
/// Central registry of everything that may be petted. Players are pettable by default; NPCs must
/// be registered here, either per NPC type or through a predicate rule. Consumer mods register
/// their own entries and their own "held item allows petting" rules. Every registration remembers
/// its owning <see cref="Mod"/> and is pruned lazily once that mod is no longer loaded, so
/// unloading a mod cannot leave dangling handlers behind.
/// </summary>
public static class PetRegistry
{
    private static readonly Dictionary<int, OwnedNpcDefinition> npcDefinitions = new();
    private static readonly List<OwnedNpcRule> npcRules = new();
    private static readonly List<OwnedPlayerRule> playerRules = new();
    private static readonly List<OwnedPlayerRule> playerRequirements = new();
    private static readonly List<OwnedItemRule> petHandItems = new();

    /// <summary>Registers a pettable NPC type. Registration alone makes the NPC pettable.</summary>
    public static void RegisterNpc(Mod owner, int npcType, PetNpcDefinition? definition = null)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        npcDefinitions[npcType] = new OwnedNpcDefinition(owner, definition ?? PetNpcDefinition.Default);
    }

    /// <summary>Removes a per-type registration if it belongs to <paramref name="owner"/>.</summary>
    public static bool UnregisterNpc(Mod owner, int npcType)
    {
        return npcDefinitions.TryGetValue(npcType, out OwnedNpcDefinition existing)
            && existing.Owner == owner
            && npcDefinitions.Remove(npcType);
    }

    /// <summary>
    /// Registers a predicate rule that marks matching NPCs as pettable. Rules take priority over
    /// per-type registrations, and the most recently registered matching rule wins.
    /// </summary>
    public static void RegisterNpcRule(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition = null)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        npcRules.Add(new OwnedNpcRule(owner, predicate, definition ?? PetNpcDefinition.Default));
    }

    /// <summary>Removes a predicate rule belonging to <paramref name="owner"/>.</summary>
    public static bool UnregisterNpcRule(Mod owner, Func<NPC, bool> predicate)
    {
        for (int i = npcRules.Count - 1; i >= 0; i--)
        {
            OwnedNpcRule rule = npcRules[i];
            if (rule.Owner == owner && rule.Predicate == predicate)
            {
                npcRules.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether the NPC matches a registered rule or type; also outputs its definition.</summary>
    public static bool TryGetNpcDefinition(NPC npc, [NotNullWhen(true)] out PetNpcDefinition? definition)
    {
        definition = null;
        if (npc is null)
            return false;

        // Rules win over type registrations so per-instance conditions can override a default.
        for (int i = npcRules.Count - 1; i >= 0; i--)
        {
            OwnedNpcRule rule = npcRules[i];
            if (!IsOwnerLoaded(rule.Owner))
            {
                npcRules.RemoveAt(i);
                continue;
            }
            if (rule.Predicate(npc))
            {
                definition = rule.Definition;
                return true;
            }
        }

        if (!npcDefinitions.TryGetValue(npc.type, out OwnedNpcDefinition owned))
            return false;
        if (!IsOwnerLoaded(owned.Owner))
        {
            npcDefinitions.Remove(npc.type);
            return false;
        }

        definition = owned.Definition;
        return true;
    }

    /// <summary>Whether the NPC may be petted at all (registered).</summary>
    public static bool IsNpcPettable(NPC npc) => npc is not null && TryGetNpcDefinition(npc, out _);

    /// <summary>
    /// Registers a rule that can veto petting a player. Players are pettable by default, so a rule
    /// only matters when it returns false; use this to exclude special players.
    /// </summary>
    public static void RegisterPlayerRule(Mod owner, Func<Player, bool> isPettable)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (isPettable is null)
            throw new ArgumentNullException(nameof(isPettable));
        playerRules.Add(new OwnedPlayerRule(owner, isPettable));
    }

    /// <summary>Removes a player rule belonging to <paramref name="owner"/>.</summary>
    public static bool UnregisterPlayerRule(Mod owner, Func<Player, bool> isPettable)
    {
        for (int i = playerRules.Count - 1; i >= 0; i--)
        {
            OwnedPlayerRule rule = playerRules[i];
            if (rule.Owner == owner && rule.Predicate == isPettable)
            {
                playerRules.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Registers an allow-list requirement: a player is pettable only when every requirement
    /// returns true. With no requirements, all players are pettable by default. This is additive
    /// with the veto rules in <see cref="RegisterPlayerRule"/>; both must pass.
    /// </summary>
    public static void RegisterPlayerRequirement(Mod owner, Func<Player, bool> predicate)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        playerRequirements.Add(new OwnedPlayerRule(owner, predicate));
    }

    /// <summary>Removes a player requirement belonging to <paramref name="owner"/>.</summary>
    public static bool UnregisterPlayerRequirement(Mod owner, Func<Player, bool> predicate)
    {
        for (int i = playerRequirements.Count - 1; i >= 0; i--)
        {
            OwnedPlayerRule rule = playerRequirements[i];
            if (rule.Owner == owner && rule.Predicate == predicate)
            {
                playerRequirements.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>Players are pettable by default; any registered rule returning false vetoes and every requirement must pass.</summary>
    public static bool IsPlayerPettable(Player player)
    {
        if (player is null || !player.active || player.dead)
            return false;

        for (int i = 0; i < playerRequirements.Count; i++)
        {
            OwnedPlayerRule rule = playerRequirements[i];
            if (!IsOwnerLoaded(rule.Owner))
            {
                playerRequirements.RemoveAt(i);
                i--;
                continue;
            }
            if (!rule.Predicate(player))
                return false;
        }

        for (int i = 0; i < playerRules.Count; i++)
        {
            OwnedPlayerRule rule = playerRules[i];
            if (!IsOwnerLoaded(rule.Owner))
            {
                playerRules.RemoveAt(i);
                i--;
                continue;
            }
            if (!rule.Predicate(player))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Registers an "item allows petting" rule. The empty hand always allows petting; a held item
    /// only does when a rule says so (for example a clicker registered by a consumer mod).
    /// </summary>
    public static void RegisterPetHandItem(Mod owner, Func<Item, bool> allowsPetting)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (allowsPetting is null)
            throw new ArgumentNullException(nameof(allowsPetting));
        petHandItems.Add(new OwnedItemRule(owner, allowsPetting));
    }

    /// <summary>Removes a held-item rule belonging to <paramref name="owner"/>.</summary>
    public static bool UnregisterPetHandItem(Mod owner, Func<Item, bool> allowsPetting)
    {
        for (int i = petHandItems.Count - 1; i >= 0; i--)
        {
            OwnedItemRule rule = petHandItems[i];
            if (rule.Owner == owner && rule.Predicate == allowsPetting)
            {
                petHandItems.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether any registered rule allows petting with this item.</summary>
    public static bool AllowsPettingItem(Item item)
    {
        if (item is null || item.IsAir)
            return false;

        for (int i = 0; i < petHandItems.Count; i++)
        {
            OwnedItemRule rule = petHandItems[i];
            if (!IsOwnerLoaded(rule.Owner))
            {
                petHandItems.RemoveAt(i);
                i--;
                continue;
            }
            if (rule.Predicate(item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Lazy unload safety: a registration is stale when its owning Mod is no longer the loaded
    /// instance under that name.
    /// </summary>
    public static bool IsOwnerLoaded(Mod owner)
    {
        if (owner is null)
            return false;
        return ModLoader.TryGetMod(owner.Name, out Mod loaded) && ReferenceEquals(loaded, owner);
    }

    /// <summary>Drops every registration; call this when the consuming mod unloads.</summary>
    public static void Clear()
    {
        npcDefinitions.Clear();
        npcRules.Clear();
        playerRules.Clear();
        playerRequirements.Clear();
        petHandItems.Clear();
    }

    private readonly record struct OwnedNpcDefinition(Mod Owner, PetNpcDefinition Definition);

    private readonly record struct OwnedNpcRule(Mod Owner, Func<NPC, bool> Predicate, PetNpcDefinition Definition);

    private readonly record struct OwnedPlayerRule(Mod Owner, Func<Player, bool> Predicate);

    private readonly record struct OwnedItemRule(Mod Owner, Func<Item, bool> Predicate);
}
