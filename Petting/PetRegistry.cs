using System.Diagnostics.CodeAnalysis;
using Terraria;
using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>
/// Owner scoped registration table. NPC type registrations and NPC rules are layered per owner and
/// resolved by priority then recency, player veto rules and requirements stay ANDed, and held item
/// rules stay ORed. Every consumer predicate is exception isolated, and entries are pruned lazily
/// once their owning <see cref="Mod"/> is no longer loaded, so unloading a mod cannot leave
/// dangling handlers behind.
/// </summary>
public static class PetRegistry
{
    private static readonly Dictionary<int, List<OwnedNpcDefinition>> npcDefinitions = new();
    private static readonly List<OwnedNpcRule> npcRules = new();
    private static readonly List<OwnedPlayerRule> playerRules = new();
    private static readonly List<OwnedPlayerRule> playerRequirements = new();
    private static readonly List<OwnedItemRule> petHandItems = new();

    /// <summary>Monotonic registration counter used as the recency tiebreak within one priority.</summary>
    private static long sequence;

    /// <summary>Registers a pettable NPC type with <see cref="PetPriority.Normal"/>. Registration alone makes the NPC pettable.</summary>
    public static void RegisterNpc(Mod owner, int npcType, PetNpcDefinition? definition = null)
    {
        RegisterNpc(owner, npcType, definition, PetPriority.Normal);
    }

    /// <summary>
    /// Registers a pettable NPC type at the given <paramref name="priority"/>. The type is stored as
    /// an owner scoped layer, and the first live layer in priority then recency order wins. When the
    /// same owner registers the same type again, the definition is replaced in place while the
    /// existing priority and sequence are preserved, so changing priority requires
    /// <see cref="UnregisterNpc"/> followed by a new registration. Registering a type that a layer
    /// owned by a different mod already shadows at a strictly higher priority keeps the new layer
    /// and logs one informational conflict naming both owners.
    /// </summary>
    public static void RegisterNpc(Mod owner, int npcType, PetNpcDefinition? definition, PetPriority priority)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        PetNpcDefinition resolved = definition ?? PetNpcDefinition.Default;
        if (!npcDefinitions.TryGetValue(npcType, out List<OwnedNpcDefinition>? layers))
        {
            layers = new List<OwnedNpcDefinition>();
            npcDefinitions[npcType] = layers;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            OwnedNpcDefinition existing = layers[i];
            if (existing.Owner == owner)
            {
                layers[i] = existing with { Definition = resolved };
                return;
            }
        }

        // Shadowing is reported once per registration. Stale layers are skipped here and pruned by
        // the next lookup, so a registration never destroys another mod's entries.
        OwnedNpcDefinition? shadowedBy = null;
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            OwnedNpcDefinition existing = layers[i];
            if (!IsOwnerLoaded(existing.Owner))
                continue;
            if (existing.Owner != owner && existing.Priority < priority
                && (shadowedBy is null || existing.Priority < shadowedBy.Value.Priority))
            {
                shadowedBy = existing;
            }
        }

        if (shadowedBy is OwnedNpcDefinition shadow)
        {
            PetLog.Info(owner,
                $"PetRegistry: NPC type {npcType} from '{owner.Name}' is shadowed by '{shadow.Owner.Name}' "
                + $"at priority {shadow.Priority} ({(int)shadow.Priority}). Register at priority {shadow.Priority} "
                + "or a numerically lower value to win, and unregister before changing priority.");
        }

        int insertAt = 0;
        while (insertAt < layers.Count && layers[insertAt].Priority < priority)
            insertAt++;

        layers.Insert(insertAt, new OwnedNpcDefinition(owner, resolved, priority, ++sequence));
    }

    /// <summary>Removes every layer owned by <paramref name="owner"/> for this NPC type and returns whether anything was removed.</summary>
    public static bool UnregisterNpc(Mod owner, int npcType)
    {
        if (!npcDefinitions.TryGetValue(npcType, out List<OwnedNpcDefinition>? layers))
            return false;

        bool removed = false;
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (layers[i].Owner == owner)
            {
                layers.RemoveAt(i);
                removed = true;
            }
        }

        if (layers.Count == 0)
            npcDefinitions.Remove(npcType);

        return removed;
    }

    /// <summary>
    /// Registers a predicate rule that marks matching NPCs as pettable with
    /// <see cref="PetPriority.Normal"/>. Rules are consulted before per-type registrations, and the
    /// first live match in priority then recency order wins.
    /// </summary>
    public static void RegisterNpcRule(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition = null)
    {
        RegisterNpcRule(owner, predicate, definition, PetPriority.Normal);
    }

    /// <summary>
    /// Registers a predicate rule at the given <paramref name="priority"/>. Lower values are
    /// consulted first, and equal priorities go to the most recent registration.
    /// </summary>
    public static void RegisterNpcRule(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition, PetPriority priority)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        OwnedNpcRule rule = new(owner, predicate, definition ?? PetNpcDefinition.Default, priority, ++sequence);

        int index = 0;
        while (index < npcRules.Count && npcRules[index].Priority < priority)
            index++;

        npcRules.Insert(index, rule);
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

    /// <summary>
    /// Whether the NPC matches a registered rule or type. Also outputs its definition. Rules are
    /// resolved before type layers, and definitions never merge across entries.
    /// </summary>
    public static bool TryGetNpcDefinition(NPC npc, [NotNullWhen(true)] out PetNpcDefinition? definition)
    {
        definition = null;
        if (npc is null)
            return false;

        // Rules win over type registrations, ordered by priority ascending then recency descending.
        for (int i = 0; i < npcRules.Count; i++)
        {
            OwnedNpcRule rule = npcRules[i];
            if (!IsOwnerLoaded(rule.Owner))
            {
                npcRules.RemoveAt(i);
                i--;
                continue;
            }
            if (SafeEvaluate(rule.Owner, rule.Predicate, npc, "PetRegistry.NpcRule"))
            {
                definition = rule.Definition;
                return true;
            }
        }

        if (!npcDefinitions.TryGetValue(npc.type, out List<OwnedNpcDefinition>? layers))
            return false;

        for (int i = 0; i < layers.Count; i++)
        {
            OwnedNpcDefinition entry = layers[i];
            if (!IsOwnerLoaded(entry.Owner))
            {
                layers.RemoveAt(i);
                i--;
                continue;
            }
            definition = entry.Definition;
            return true;
        }

        if (layers.Count == 0)
            npcDefinitions.Remove(npc.type);
        return false;
    }

    /// <summary>Whether the NPC may be petted at all (registered).</summary>
    public static bool IsNpcPettable(NPC npc) => npc is not null && TryGetNpcDefinition(npc, out _);

    /// <summary>
    /// Registers a rule that can veto petting a player. Players are pettable by default, so a rule
    /// only matters when it returns false. Use this to exclude special players. Player lists keep
    /// v1 registration order, and a rule whose predicate throws vetoes.
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
    /// with the veto rules in <see cref="RegisterPlayerRule"/>. Both must pass. Requirements are
    /// checked first, a requirement whose predicate throws fails, and evaluation order stays v1
    /// registration order.
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

    /// <summary>Players are pettable by default. Any registered rule returning false vetoes and every requirement must pass.</summary>
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
            if (!SafeEvaluate(rule.Owner, rule.Predicate, player, "PetRegistry.PlayerRequirement"))
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
            if (!SafeEvaluate(rule.Owner, rule.Predicate, player, "PetRegistry.PlayerRule"))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Registers an "item allows petting" rule. The empty hand always allows petting. A held item
    /// only does when a rule says so (for example a clicker registered by a consumer mod). Item
    /// rules are ORed in v1 registration order, and a rule whose predicate throws does not allow.
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
            if (SafeEvaluate(rule.Owner, rule.Predicate, item, "PetRegistry.PetHandItem"))
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

    /// <summary>Removes every registration owned by <paramref name="owner"/> across every list and returns the number removed.</summary>
    public static int ClearOwner(Mod owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        int removed = 0;
        List<int>? emptyTypes = null;

        foreach (KeyValuePair<int, List<OwnedNpcDefinition>> pair in npcDefinitions)
        {
            List<OwnedNpcDefinition> layers = pair.Value;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].Owner == owner)
                {
                    layers.RemoveAt(i);
                    removed++;
                }
            }

            if (layers.Count == 0)
            {
                emptyTypes ??= new List<int>();
                emptyTypes.Add(pair.Key);
            }
        }

        if (emptyTypes is not null)
        {
            foreach (int npcType in emptyTypes)
                npcDefinitions.Remove(npcType);
        }

        removed += RemoveOwned(npcRules, owner, static entry => entry.Owner);
        removed += RemoveOwned(playerRules, owner, static entry => entry.Owner);
        removed += RemoveOwned(playerRequirements, owner, static entry => entry.Owner);
        removed += RemoveOwned(petHandItems, owner, static entry => entry.Owner);

        PetLog.ClearOwner(owner);
        return removed;
    }

    /// <summary>Drops every registration and resets the logger throttle. Call this when the consuming mod unloads.</summary>
    public static void Clear()
    {
        npcDefinitions.Clear();
        npcRules.Clear();
        playerRules.Clear();
        playerRequirements.Clear();
        petHandItems.Clear();
        PetLog.Clear();
    }

    private readonly record struct OwnedNpcDefinition(Mod Owner, PetNpcDefinition Definition, PetPriority Priority, long Sequence);

    private readonly record struct OwnedNpcRule(Mod Owner, Func<NPC, bool> Predicate, PetNpcDefinition Definition, PetPriority Priority, long Sequence);

    private readonly record struct OwnedPlayerRule(Mod Owner, Func<Player, bool> Predicate);

    private readonly record struct OwnedItemRule(Mod Owner, Func<Item, bool> Predicate);

    /// <summary>
    /// Runs one consumer predicate, logging and treating the result as false when it throws. A false
    /// result reads as "does not match" for an NPC rule, "vetoes" for a player rule, "fails" for a
    /// player requirement, and "does not allow" for an item rule.
    /// </summary>
    private static bool SafeEvaluate<T>(Mod owner, Func<T, bool> predicate, T value, string context)
    {
        try
        {
            return predicate(value);
        }
        catch (Exception exception)
        {
            PetLog.Error(owner, context, exception);
            return false;
        }
    }

    /// <summary>Removes every entry owned by <paramref name="owner"/> and returns the number removed.</summary>
    private static int RemoveOwned<T>(List<T> list, Mod owner, Func<T, Mod> ownerOf)
    {
        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ownerOf(list[i]) == owner)
            {
                list.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }
}
