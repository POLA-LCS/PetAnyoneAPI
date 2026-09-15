using Terraria;
using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>
/// Public entry point for consumer mods. Use the static helpers below, or <c>Mod.Call(...)</c>
/// with the commands documented on <see cref="Call"/>. Always pass your own <see cref="Mod"/>
/// instance as the first argument after the command so registrations can be dropped when your mod
/// unloads.
/// </summary>
public static class PettingApi
{
    /// <summary>Bumped whenever the public petting surface changes.</summary>
    public const int ApiVersion = 1;

    /// <summary>Registers an NPC type as pettable.</summary>
    public static bool RegisterPettableNpc(Mod owner, int npcType, PetNpcDefinition? definition = null)
    {
        PetRegistry.RegisterNpc(owner, npcType, definition);
        return true;
    }

    /// <summary>Registers a predicate that marks matching NPCs as pettable.</summary>
    public static bool RegisterPettableNpc(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition = null)
    {
        PetRegistry.RegisterNpcRule(owner, predicate, definition);
        return true;
    }

    /// <summary>Registers a rule that can veto petting a player; players are pettable by default.</summary>
    public static bool RegisterPettablePlayer(Mod owner, Func<Player, bool> isPettable)
    {
        PetRegistry.RegisterPlayerRule(owner, isPettable);
        return true;
    }

    /// <summary>Registers an allow-list requirement: player is pettable only when every requirement passes.</summary>
    public static bool RegisterPettablePlayerRequirement(Mod owner, Func<Player, bool> predicate)
    {
        PetRegistry.RegisterPlayerRequirement(owner, predicate);
        return true;
    }

    /// <summary>Registers several pet hooks at once.</summary>
    public static void RegisterEvents(Mod owner, PetEventCallbacks callbacks)
    {
        PetEvents.RegisterCallbacks(owner, callbacks);
    }

    /// <summary>
    /// Handles <see cref="Mod.Call"/> from other mods. Supported commands:
    /// <list type="bullet">
    /// <item><c>"GetVersion"</c> - returns <see cref="ApiVersion"/>.</item>
    /// <item><c>"RegisterPettableNpc", owner, npcType, [allowWorldPet, showChatButton, displayName]</c> - per type.</item>
    /// <item><c>"RegisterPettableNpc", owner, predicate, [allowWorldPet, showChatButton, displayName]</c> - by rule.</item>
    /// <item><c>"RegisterPettablePlayer", owner, isPettable</c> - veto rule for players.</item>
    /// <item><c>"RegisterEvents", owner, callbacks</c> - a <see cref="PetEventCallbacks"/> bundle.</item>
    /// </list>
    /// Returns null for unknown commands.
    /// </summary>
    public static object? Call(object[] args)
    {
        if (args is null || args.Length == 0 || args[0] is not string command)
            return null;

        switch (command)
        {
            case "GetVersion":
                return ApiVersion;
            case "RegisterPettableNpc":
                return CallRegisterPettableNpc(args);
            case "RegisterPettablePlayer":
                return CallRegisterPettablePlayer(args);
            case "RegisterPettablePlayerRequirement":
            case "RegisterPlayerRequirement":
                if (args.Length >= 3 && args[1] is Mod reqOwner && args[2] is Func<Player, bool> req)
                {
                    PetRegistry.RegisterPlayerRequirement(reqOwner, req);
                    return true;
                }
                return false;
            case "RegisterEvents":
                if (args.Length >= 3 && args[1] is Mod eventsOwner && args[2] is PetEventCallbacks callbacks)
                {
                    PetEvents.RegisterCallbacks(eventsOwner, callbacks);
                    return true;
                }
                return false;
            default:
                return null;
        }
    }

    private static object CallRegisterPettableNpc(object[] args)
    {
        if (args.Length < 4 || args[1] is not Mod owner)
            return false;

        PetNpcDefinition definition = args[3] as PetNpcDefinition ?? ParseNpcDefinition(args, 3);

        if (args[2] is int npcType)
        {
            PetRegistry.RegisterNpc(owner, npcType, definition);
            return true;
        }

        if (args[2] is Func<NPC, bool> predicate)
        {
            PetRegistry.RegisterNpcRule(owner, predicate, definition);
            return true;
        }

        return false;
    }

    private static object CallRegisterPettablePlayer(object[] args)
    {
        if (args.Length >= 3 && args[1] is Mod owner && args[2] is Func<Player, bool> isPettable)
        {
            PetRegistry.RegisterPlayerRule(owner, isPettable);
            return true;
        }
        return false;
    }

    /// <summary>Parses optional (allowWorldPet, showChatButton, displayName, buttonText) arguments.</summary>
    private static PetNpcDefinition ParseNpcDefinition(object[] args, int start)
    {
        bool allowWorldPet = true;
        bool showChatButton = true;
        string? displayName = null;
        string? buttonText = null;

        if (start < args.Length && args[start] is bool world)
        {
            allowWorldPet = world;
            start++;
        }
        if (start < args.Length && args[start] is bool chat)
        {
            showChatButton = chat;
            start++;
        }
        if (start < args.Length && args[start] is string disp)
        {
            displayName = disp;
            start++;
        }
        if (start < args.Length && args[start] is string btn)
        {
            buttonText = btn;
        }

        return new PetNpcDefinition(displayName, allowWorldPet, showChatButton, buttonText);
    }
}
