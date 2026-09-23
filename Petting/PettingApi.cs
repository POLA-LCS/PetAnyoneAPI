using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

#nullable enable

namespace PetAnyone;

/// <summary>
/// Public entry point for consumer mods. Use the static helpers below, or <c>Mod.Call(...)</c>
/// with the commands documented on <see cref="Call"/>. Always pass your own <see cref="Mod"/>
/// instance as the owner so registrations and subscriptions can be dropped when your mod unloads.
/// The API provides hooks and session state only. Visuals such as hearts are consumer mod work:
/// subscribe through <see cref="PetEvents"/> and spawn your own effects.
/// </summary>
public static class PettingApi
{
    /// <summary>Major API version, bumped whenever the public surface changes incompatibly.</summary>
    public const int ApiVersion = 2;

    /// <summary>Minor API version, bumped for additive changes only.</summary>
    public const int ApiMinorVersion = 0;

    /// <summary>Human readable API version string.</summary>
    public const string ApiVersionString = "2.0.0";

    /// <summary>tModLoader internal name of this mod, for <c>ModLoader.GetMod</c>.</summary>
    public const string ModName = "PetAnyoneAPI";

    /// <summary>Immutable API identity for version negotiation through <c>Call("GetApi")</c>.</summary>
    public static PetApiInfo Info { get; } = new(ApiVersion, ApiMinorVersion, 0, ApiVersionString, ModName);

    /// <summary>Registers an NPC type as pettable with Normal priority.</summary>
    public static bool RegisterPettableNpc(Mod owner, int npcType, PetNpcDefinition? definition = null)
    {
        PetRegistry.RegisterNpc(owner, npcType, definition);
        return true;
    }

    /// <summary>Registers an NPC type as pettable at the given priority.</summary>
    public static bool RegisterPettableNpc(Mod owner, int npcType, PetNpcDefinition? definition, PetPriority priority)
    {
        PetRegistry.RegisterNpc(owner, npcType, definition, priority);
        return true;
    }

    /// <summary>Registers a predicate that marks matching NPCs as pettable with Normal priority.</summary>
    public static bool RegisterPettableNpc(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition = null)
    {
        PetRegistry.RegisterNpcRule(owner, predicate, definition);
        return true;
    }

    /// <summary>Registers a predicate rule at the given priority.</summary>
    public static bool RegisterPettableNpc(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition, PetPriority priority)
    {
        PetRegistry.RegisterNpcRule(owner, predicate, definition, priority);
        return true;
    }

    /// <summary>Registers a rule that can veto petting a player. Players are pettable by default.</summary>
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

    /// <summary>Registers an item rule that allows petting while held.</summary>
    public static bool RegisterPetHandItem(Mod owner, Func<Item, bool> allowsPetting)
    {
        PetRegistry.RegisterPetHandItem(owner, allowsPetting);
        return true;
    }

    /// <summary>Registers several pet hooks at once through a <see cref="PetEventCallbacks"/> bundle.</summary>
    public static void RegisterEvents(Mod owner, PetEventCallbacks callbacks)
    {
        PetEvents.RegisterCallbacks(owner, callbacks);
    }

    /// <summary>
    /// Handles <see cref="Mod.Call"/> from other mods. Returns null for unknown commands.
    /// <para>
    /// v2 commands carry the integer API version as the second argument, for example
    /// <c>Call("ApplyPet", 2, patter, target)</c>. The full command table lives in the README.
    /// </para>
    /// <para>
    /// v1 command shapes without the version integer are still accepted for compiled consumers:
    /// GetVersion, RegisterPettableNpc, RegisterPettablePlayer, RegisterPettablePlayerRequirement,
    /// RegisterPlayerRequirement, and RegisterEvents.
    /// </para>
    /// </summary>
    public static object? Call(object[] args)
    {
        if (args is null || args.Length == 0 || args[0] is not string command)
            return null;

        if (command == "GetVersion")
            return ApiVersion;

        if (command == "GetApi")
            return Info;

        // v2 shapes carry the API version as an int at args[1]. v1 shapes passed the owner Mod there.
        if (args.Length >= 2 && args[1] is int apiVersion)
        {
            if (apiVersion > ApiVersion)
            {
                throw new NotSupportedException(
                    $"PetAnyoneAPI command '{command}' requested API version {apiVersion}, but this build provides {ApiVersion}. Update the PetAnyoneAPI dependency.");
            }

            if (apiVersion < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(args),
                    apiVersion,
                    $"PetAnyoneAPI command '{command}' received an invalid API version.");
            }

            return CallV2(command, args);
        }

        return CallV1(command, args);
    }

    private static object? CallV2(string command, object[] args)
    {
        switch (command)
        {
            case "RegisterPettableNpc":
            {
                Mod owner = OwnerAt(args, command);
                object selector = SelectorAt(args, command);
                PetNpcDefinition? definition = null;
                int index = 4;
                if (index < args.Length && (args[index] is null || args[index] is PetNpcDefinition))
                {
                    definition = args[index] as PetNpcDefinition;
                    index++;
                }

                PetPriority priority = ReadEnum(args, index, PetPriority.Normal, command, "a PetPriority");
                if (selector is int npcType)
                {
                    PetRegistry.RegisterNpc(owner, npcType, definition, priority);
                    return true;
                }

                if (selector is Func<NPC, bool> predicate)
                {
                    PetRegistry.RegisterNpcRule(owner, predicate, definition, priority);
                    return true;
                }

                throw new ArgumentException(
                    $"PetAnyoneAPI command '{command}' expects an int npcType or a Func<NPC, bool> predicate at argument index 3.");
            }

            case "RegisterPettablePlayer":
            {
                Mod owner = OwnerAt(args, command);
                Func<Player, bool> rule = DelegateAt<Func<Player, bool>>(args, 3, command, "a Func<Player, bool> isPettable");
                PetRegistry.RegisterPlayerRule(owner, rule);
                return true;
            }

            case "RegisterPettablePlayerRequirement":
            case "RegisterPlayerRequirement":
            {
                Mod owner = OwnerAt(args, command);
                Func<Player, bool> requirement = DelegateAt<Func<Player, bool>>(args, 3, command, "a Func<Player, bool> predicate");
                PetRegistry.RegisterPlayerRequirement(owner, requirement);
                return true;
            }

            case "RegisterPetHandItem":
            {
                Mod owner = OwnerAt(args, command);
                Func<Item, bool> rule = DelegateAt<Func<Item, bool>>(args, 3, command, "a Func<Item, bool> allowsPetting");
                PetRegistry.RegisterPetHandItem(owner, rule);
                return true;
            }

            case "RegisterReachAngle":
            {
                Mod owner = OwnerAt(args, command);
                Func<PetTarget, float?> provider = DelegateAt<Func<PetTarget, float?>>(args, 3, command, "a Func<PetTarget, float?> provider");
                PetEvents.OnReachAngle(owner, provider, OptionsAt(args, 4, command));
                return true;
            }

            case "Subscribe":
            {
                Mod owner = OwnerAt(args, command);
                string eventName = StringAt(args, 3, command, "an event name string");
                Delegate handler = DelegateAt<Delegate>(args, 4, command, "a delegate handler");
                return SubscribeV2(owner, eventName, handler, OptionsAt(args, 5, command));
            }

            case "Unsubscribe":
            {
                Mod owner = OwnerAt(args, command);
                string eventName = StringAt(args, 3, command, "an event name string");
                if (!IsKnownEventName(eventName))
                    throw new ArgumentException(UnknownEventMessage(eventName));
                Delegate handler = DelegateAt<Delegate>(args, 4, command, "a delegate handler");
                return PetEvents.Unsubscribe(owner, handler);
            }

            case "UnregisterPettableNpc":
            {
                Mod owner = OwnerAt(args, command);
                int npcType = IntAt(args, 3, command, "an int npcType");
                return PetRegistry.UnregisterNpc(owner, npcType);
            }

            case "UnregisterPettableNpcRule":
            {
                Mod owner = OwnerAt(args, command);
                Func<NPC, bool> predicate = DelegateAt<Func<NPC, bool>>(args, 3, command, "a Func<NPC, bool> predicate");
                return PetRegistry.UnregisterNpcRule(owner, predicate);
            }

            case "UnregisterPettablePlayer":
            {
                Mod owner = OwnerAt(args, command);
                Func<Player, bool> rule = DelegateAt<Func<Player, bool>>(args, 3, command, "a Func<Player, bool> isPettable");
                return PetRegistry.UnregisterPlayerRule(owner, rule);
            }

            case "UnregisterPettablePlayerRequirement":
            case "UnregisterPlayerRequirement":
            {
                Mod owner = OwnerAt(args, command);
                Func<Player, bool> requirement = DelegateAt<Func<Player, bool>>(args, 3, command, "a Func<Player, bool> predicate");
                return PetRegistry.UnregisterPlayerRequirement(owner, requirement);
            }

            case "UnregisterPetHandItem":
            {
                Mod owner = OwnerAt(args, command);
                Func<Item, bool> rule = DelegateAt<Func<Item, bool>>(args, 3, command, "a Func<Item, bool> allowsPetting");
                return PetRegistry.UnregisterPetHandItem(owner, rule);
            }

            case "UnregisterReachAngle":
            {
                Mod owner = OwnerAt(args, command);
                Func<PetTarget, float?> provider = DelegateAt<Func<PetTarget, float?>>(args, 3, command, "a Func<PetTarget, float?> provider");
                return PetEvents.Unsubscribe(owner, provider);
            }

            case "ClearOwner":
            {
                Mod owner = OwnerAt(args, command);
                return PetEvents.ClearOwner(owner) + PetRegistry.ClearOwner(owner);
            }

            case "CanPet":
            {
                if (args.Length < 4 || args[2] is not Player patter)
                    throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a Player patter at argument index 2 and a target at argument index 3.");
                if (!TryReadTarget(args, 3, out PetTarget target, out _))
                    throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a PetTarget or two ints (kind, index) at argument index 3.");
                return PetService.CanPet(patter, target);
            }

            case "ApplyPet":
            {
                if (args.Length < 4 || args[2] is not Player patter)
                    throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a Player patter at argument index 2 and a target at argument index 3.");
                if (!TryReadTarget(args, 3, out PetTarget target, out int consumed))
                    throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a PetTarget or two ints (kind, index) at argument index 3.");
                int next = 3 + consumed;
                PetApplyMode mode = ReadEnum(args, next, PetApplyMode.Tap, command, "a PetApplyMode");
                PetEventSource source = ReadEnum(args, next + 1, PetEventSource.Local, command, "a PetEventSource");
                return PetService.TryApplyPet(patter, target, mode, source);
            }

            case "EndPet":
            {
                if (args.Length < 4 || args[2] is not Player patter)
                    throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a Player patter at argument index 2 and a target at argument index 3.");
                if (!TryReadTarget(args, 3, out PetTarget target, out int consumed))
                    throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a PetTarget or two ints (kind, index) at argument index 3.");
                int next = 3 + consumed;
                PetEndReason reason = ReadEnum(args, next, PetEndReason.Released, command, "a PetEndReason");
                PetEventSource source = ReadEnum(args, next + 1, PetEventSource.Local, command, "a PetEventSource");
                return PetService.EndPet(patter, target, reason, source);
            }

            case "IsPetActive":
            {
                if (args.Length >= 3 && args[2] is Player patter)
                {
                    if (!TryReadTarget(args, 3, out PetTarget target, out _))
                        throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a target after the Player.");
                    return PetService.IsPetActive(patter, target);
                }

                if (TryReadTarget(args, 2, out PetTarget single, out _))
                    return PetService.IsPetActive(single);

                throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a target or a Player followed by a target.");
            }

            default:
                return null;
        }
    }

    private static bool SubscribeV2(Mod owner, string eventName, Delegate handler, PetHandlerOptions? options)
    {
        switch (eventName)
        {
            case "CanPet":
                if (handler is Action<CanPetEvent> canPet)
                {
                    PetEvents.OnCanPet(owner, canPet, options);
                    return true;
                }

                if (handler is Func<PetContext, bool> legacy)
                {
                    PetEvents.RegisterCallbacks(owner, new PetEventCallbacks { CanPet = legacy });
                    return true;
                }

                throw new ArgumentException(
                    "PetAnyoneAPI Subscribe expects Action<CanPetEvent> or Func<PetContext, bool> for the CanPet event.");
            case "PetStart":
                PetEvents.OnPetStart(owner, RequireHandler<Action<PetStartEvent>>(handler, eventName), options);
                return true;
            case "PetHold":
                PetEvents.OnPetHold(owner, RequireHandler<Action<PetHoldEvent>>(handler, eventName), options);
                return true;
            case "PetEnd":
                PetEvents.OnPetEnd(owner, RequireHandler<Action<PetEndEvent>>(handler, eventName), options);
                return true;
            case "ReachAngle":
                PetEvents.OnReachAngle(owner, RequireHandler<Func<PetTarget, float?>>(handler, eventName), options);
                return true;
            default:
                throw new ArgumentException(UnknownEventMessage(eventName));
        }
    }

    private static object? CallV1(string command, object[] args)
    {
        switch (command)
        {
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

    private static Mod OwnerAt(object[] args, string command)
    {
        if (args.Length < 3 || args[2] is not Mod owner)
            throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a Mod owner at argument index 2.");
        return owner;
    }

    private static object SelectorAt(object[] args, string command)
    {
        if (args.Length < 4)
            throw new ArgumentException($"PetAnyoneAPI command '{command}' expects an int npcType or a Func<NPC, bool> predicate at argument index 3.");
        return args[3];
    }

    private static int IntAt(object[] args, int index, string command, string schema)
    {
        if (index >= args.Length || args[index] is not int value)
            throw new ArgumentException($"PetAnyoneAPI command '{command}' expects {schema} at argument index {index}.");
        return value;
    }

    private static string StringAt(object[] args, int index, string command, string schema)
    {
        if (index >= args.Length || args[index] is not string value)
            throw new ArgumentException($"PetAnyoneAPI command '{command}' expects {schema} at argument index {index}.");
        return value;
    }

    private static T DelegateAt<T>(object[] args, int index, string command, string schema)
        where T : Delegate
    {
        if (index >= args.Length || args[index] is not T typed)
            throw new ArgumentException($"PetAnyoneAPI command '{command}' expects {schema} at argument index {index}.");
        return typed;
    }

    private static T RequireHandler<T>(Delegate handler, string eventName)
        where T : Delegate
    {
        if (handler is not T typed)
            throw new ArgumentException($"PetAnyoneAPI Subscribe expects {typeof(T).Name} for the {eventName} event.");
        return typed;
    }

    private static PetHandlerOptions? OptionsAt(object[] args, int index, string command)
    {
        if (index >= args.Length || args[index] is null)
            return null;
        if (args[index] is PetHandlerOptions options)
            return options;
        throw new ArgumentException($"PetAnyoneAPI command '{command}' expects a PetHandlerOptions at argument index {index}.");
    }

    private static T ReadEnum<T>(object[] args, int index, T defaultValue, string command, string schema)
        where T : struct, Enum
    {
        if (index >= args.Length || args[index] is null)
            return defaultValue;
        if (args[index] is T value)
            return value;
        if (args[index] is int raw && Enum.IsDefined(typeof(T), raw))
            return (T)Enum.ToObject(typeof(T), raw);
        throw new ArgumentException($"PetAnyoneAPI command '{command}' expects {schema} at argument index {index}.");
    }

    private static bool TryReadTarget(object[] args, int index, out PetTarget target, out int consumed)
    {
        target = PetTarget.None;
        consumed = 0;
        if (index >= args.Length)
            return false;

        if (args[index] is PetTarget boxed)
        {
            target = boxed;
            consumed = 1;
            return true;
        }

        if (index + 1 < args.Length
            && args[index] is int kind
            && args[index + 1] is int entityIndex
            && (kind == 0 || kind == 1))
        {
            target = new PetTarget(kind == 0 ? PetTargetKind.Player : PetTargetKind.Npc, entityIndex);
            consumed = 2;
            return true;
        }

        return false;
    }

    private static bool IsKnownEventName(string eventName)
    {
        return eventName is "CanPet" or "PetStart" or "PetHold" or "PetEnd" or "ReachAngle";
    }

    private static string UnknownEventMessage(string eventName)
    {
        return $"PetAnyoneAPI received unknown event name '{eventName}'. Valid names: CanPet, PetStart, PetHold, PetEnd, ReachAngle.";
    }
}
