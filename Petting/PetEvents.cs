using System;
using System.Collections.Generic;
using Terraria.ModLoader;

#nullable enable

namespace PetAnyone;

/// <summary>
/// Subscription and dispatch surface for pet hooks. v2 subscriptions use the <c>On*</c> methods,
/// ordered by priority then registration order, filtered per target, exception isolated, and
/// snapshotted before dispatch. v1 <c>Register*</c> methods remain as obsolete adapters over the
/// same engine.
/// </summary>
public static class PetEvents
{
    private const string CanPetEventName = "CanPet";
    private const string PetStartEventName = "PetStart";
    private const string PetHoldEventName = "PetHold";
    private const string PetEndEventName = "PetEnd";
    private const string ReachAngleEventName = "ReachAngle";

    private static readonly SubscriptionList<Action<CanPetEvent>> canPetHandlers = new();
    private static readonly SubscriptionList<Action<PetStartEvent>> petStartHandlers = new();
    private static readonly SubscriptionList<Action<PetHoldEvent>> petHoldHandlers = new();
    private static readonly SubscriptionList<Action<PetEndEvent>> petEndHandlers = new();
    private static readonly SubscriptionList<Func<PetTarget, float?>> reachAngleProviders = new();

    private static long nextSequence;

    // v2 subscriptions

    /// <summary>Adds a cancellable pre event handler. Set <see cref="CanPetEvent.Cancel"/> to veto.</summary>
    public static void OnCanPet(Mod owner, Action<CanPetEvent> handler, PetHandlerOptions? options = null)
        => Add(canPetHandlers, owner, handler, options);

    /// <summary>Adds a session start handler, raised for a tap and for the first application of a hold.</summary>
    public static void OnPetStart(Mod owner, Action<PetStartEvent> handler, PetHandlerOptions? options = null)
        => Add(petStartHandlers, owner, handler, options);

    /// <summary>Adds a hold refresh handler.</summary>
    public static void OnPetHold(Mod owner, Action<PetHoldEvent> handler, PetHandlerOptions? options = null)
        => Add(petHoldHandlers, owner, handler, options);

    /// <summary>Adds a session end handler.</summary>
    public static void OnPetEnd(Mod owner, Action<PetEndEvent> handler, PetHandlerOptions? options = null)
        => Add(petEndHandlers, owner, handler, options);

    /// <summary>Adds a reach angle provider. The first live provider returning a value wins.</summary>
    public static void OnReachAngle(Mod owner, Func<PetTarget, float?> provider, PetHandlerOptions? options = null)
        => Add(reachAngleProviders, owner, provider, options);

    /// <summary>
    /// Removes the first subscription matching the owner and delegate in every list. Delegate
    /// equality applies. Returns whether anything was removed.
    /// </summary>
    public static bool Unsubscribe(Mod owner, Delegate handler)
    {
        if (owner is null || handler is null)
            return false;

        bool removed = RemoveFirst(canPetHandlers.Items, owner, handler);
        removed |= RemoveFirst(petStartHandlers.Items, owner, handler);
        removed |= RemoveFirst(petHoldHandlers.Items, owner, handler);
        removed |= RemoveFirst(petEndHandlers.Items, owner, handler);
        removed |= RemoveFirst(reachAngleProviders.Items, owner, handler);
        return removed;
    }

    /// <summary>Removes every subscription owned by the mod across every list. Returns the number removed.</summary>
    public static int ClearOwner(Mod owner)
    {
        if (owner is null)
            return 0;

        int removed = RemoveOwner(canPetHandlers.Items, owner);
        removed += RemoveOwner(petStartHandlers.Items, owner);
        removed += RemoveOwner(petHoldHandlers.Items, owner);
        removed += RemoveOwner(petEndHandlers.Items, owner);
        removed += RemoveOwner(reachAngleProviders.Items, owner);
        PetLog.ClearOwner(owner);
        return removed;
    }

    // legacy v1 surface, kept for binary and source compatibility

    /// <summary>Deprecated adapter. Return false to cancel the pet.</summary>
    [Obsolete("Use PetEvents.OnCanPet(owner, handler) instead.")]
    public static void RegisterCanPet(Mod owner, Func<PetContext, bool> handler) => Add(canPetHandlers, owner, handler);

    /// <summary>Deprecated adapter.</summary>
    [Obsolete("Use PetEvents.OnPetStart(owner, handler) instead.")]
    public static void RegisterOnPetStart(Mod owner, Action<PetContext> handler) => Add(petStartHandlers, owner, handler);

    /// <summary>Deprecated adapter.</summary>
    [Obsolete("Use PetEvents.OnPetHold(owner, handler) instead.")]
    public static void RegisterOnPetHold(Mod owner, Action<PetContext> handler) => Add(petHoldHandlers, owner, handler);

    /// <summary>Deprecated adapter.</summary>
    [Obsolete("Use PetEvents.OnPetEnd(owner, handler) instead.")]
    public static void RegisterOnPetEnd(Mod owner, Action<PetContext> handler) => Add(petEndHandlers, owner, handler);

    /// <summary>Deprecated adapter.</summary>
    [Obsolete("Use PetEvents.OnReachAngle(owner, provider) instead.")]
    public static void RegisterReachAngle(Mod owner, Func<PetTarget, float?> provider) => Add(reachAngleProviders, owner, provider);

    /// <summary>
    /// Registers every non-null callback in a v1 bundle through the same engine as the v2
    /// subscriptions. Kept for Mod.Call parity and not obsolete.
    /// </summary>
    public static void RegisterCallbacks(Mod owner, PetEventCallbacks? callbacks)
    {
        if (callbacks is null)
            return;
        if (callbacks.CanPet is not null)
            Add(canPetHandlers, owner, callbacks.CanPet);
        if (callbacks.OnPetStart is not null)
            Add(petStartHandlers, owner, callbacks.OnPetStart);
        if (callbacks.OnPetHold is not null)
            Add(petHoldHandlers, owner, callbacks.OnPetHold);
        if (callbacks.OnPetEnd is not null)
            Add(petEndHandlers, owner, callbacks.OnPetEnd);
    }

    // dispatch and queries

    /// <summary>Runs the CanPet gate without applying anything. True when no handler cancelled.</summary>
    public static bool CanPet(PetContext context)
    {
        return RaiseCanPet(context, PetEventSource.Manual);
    }

    /// <summary>Runs the CanPet gate with explicit source metadata. False when a handler cancelled.</summary>
    public static bool RaiseCanPet(PetContext context, PetEventSource source, Mod? issuer = null)
    {
        var evt = new CanPetEvent(context, source, issuer);
        Subscription[] snapshot = Snapshot(canPetHandlers.Items);
        for (int i = 0; i < snapshot.Length; i++)
        {
            Subscription entry = snapshot[i];
            if (!PassesFilters(entry, evt.Target, CanPetEventName))
                continue;

            try
            {
                ((Action<CanPetEvent>)entry.Handler)(evt);
            }
            catch (Exception exception)
            {
                PetLog.Error(entry.Owner, CanPetEventName, exception);
            }

            if (evt.Cancel)
                return false;
        }

        return true;
    }

    /// <summary>Dispatches a prepared start payload. Library code raises through PetService.</summary>
    public static void RaisePetStart(PetStartEvent evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));
        Dispatch(petStartHandlers.Items, evt, PetStartEventName);
    }

    /// <summary>Dispatches a prepared hold payload. Library code raises through PetService.</summary>
    public static void RaisePetHold(PetHoldEvent evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));
        Dispatch(petHoldHandlers.Items, evt, PetHoldEventName);
    }

    /// <summary>Dispatches a prepared end payload. Prefer PetService.EndPet so sessions stay consistent.</summary>
    public static void RaisePetEnd(PetEndEvent evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));
        Dispatch(petEndHandlers.Items, evt, PetEndEventName);
    }

    /// <summary>Kept v1 dispatch. Raises a Manual, Tap start. Prefer <see cref="RaisePetStart(PetStartEvent)"/>.</summary>
    public static void RaisePetStart(PetContext context) => Raise(petStartHandlers, context);

    /// <summary>Kept v1 dispatch. Raises a Manual hold. Prefer <see cref="RaisePetHold(PetHoldEvent)"/>.</summary>
    public static void RaisePetHold(PetContext context) => Raise(petHoldHandlers, context);

    /// <summary>Kept v1 dispatch. Raises a Manual end with <see cref="PetEndReason.Manual"/>. Prefer <see cref="RaisePetEnd(PetEndEvent)"/>.</summary>
    public static void RaisePetEnd(PetContext context) => Raise(petEndHandlers, context);

    /// <summary>The first live provider returning an angle wins.</summary>
    public static bool TryGetReachAngle(PetTarget target, out float angle)
    {
        angle = 0f;
        Subscription[] snapshot = Snapshot(reachAngleProviders.Items);
        for (int i = 0; i < snapshot.Length; i++)
        {
            Subscription entry = snapshot[i];
            if (!PassesFilters(entry, target, ReachAngleEventName))
                continue;

            float? result;
            try
            {
                result = ((Func<PetTarget, float?>)entry.Handler)(target);
            }
            catch (Exception exception)
            {
                PetLog.Error(entry.Owner, ReachAngleEventName, exception);
                continue;
            }

            if (result.HasValue)
            {
                angle = result.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>Drops every subscription and resets the log throttle state.</summary>
    public static void Clear()
    {
        canPetHandlers.Items.Clear();
        petStartHandlers.Items.Clear();
        petHoldHandlers.Items.Clear();
        petEndHandlers.Items.Clear();
        reachAngleProviders.Items.Clear();
        PetLog.Clear();
    }

    // Internal add paths. The typed list keeps the legacy expression bodied methods compiling
    // unchanged while wrapping their v1 delegates into v2 payload handlers.

    private static void Add<TEvent>(SubscriptionList<Action<TEvent>> list, Mod owner, Action<TEvent> handler, PetHandlerOptions? options = null)
        where TEvent : PetEvent
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        list.Add(owner, handler, options);
    }

    private static void Add<TEvent>(SubscriptionList<Action<TEvent>> list, Mod owner, Action<PetContext> handler, PetHandlerOptions? options = null)
        where TEvent : PetEvent
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        list.Add(owner, (Action<TEvent>)(evt => handler(evt.Context)), options);
    }

    private static void Add(SubscriptionList<Action<CanPetEvent>> list, Mod owner, Func<PetContext, bool> handler, PetHandlerOptions? options = null)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        list.Add(owner, (Action<CanPetEvent>)(evt =>
        {
            if (handler(evt.Context))
                return;

            evt.Cancel = true;
            evt.RejectionReason ??= "Cancelled by a legacy RegisterCanPet handler.";
        }), options);
    }

    private static void Add(SubscriptionList<Func<PetTarget, float?>> list, Mod owner, Func<PetTarget, float?> provider, PetHandlerOptions? options = null)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        list.Add(owner, provider, options);
    }

    private static void Raise(SubscriptionList<Action<PetStartEvent>> list, PetContext context)
    {
        Dispatch(list.Items, new PetStartEvent(context, PetEventSource.Manual, PetApplyMode.Tap), PetStartEventName);
    }

    private static void Raise(SubscriptionList<Action<PetHoldEvent>> list, PetContext context)
    {
        Dispatch(list.Items, new PetHoldEvent(context, PetEventSource.Manual), PetHoldEventName);
    }

    private static void Raise(SubscriptionList<Action<PetEndEvent>> list, PetContext context)
    {
        Dispatch(list.Items, new PetEndEvent(context, PetEventSource.Manual, PetEndReason.Manual), PetEndEventName);
    }

    private static void Dispatch<TEvent>(List<Subscription> items, TEvent evt, string eventName)
        where TEvent : PetEvent
    {
        Subscription[] snapshot = Snapshot(items);
        for (int i = 0; i < snapshot.Length; i++)
        {
            Subscription entry = snapshot[i];
            if (!PassesFilters(entry, evt.Target, eventName))
                continue;

            try
            {
                ((Action<TEvent>)entry.Handler)(evt);
            }
            catch (Exception exception)
            {
                PetLog.Error(entry.Owner, eventName, exception);
            }
        }
    }

    private static bool PassesFilters(Subscription entry, PetTarget target, string eventName)
    {
        if (entry.TargetKind is PetTargetKind kind)
        {
            try
            {
                if (kind != target.Kind)
                    return false;
            }
            catch (Exception exception)
            {
                PetLog.Error(entry.Owner, eventName, exception);
                return false;
            }
        }

        if (entry.Filter is not null)
        {
            try
            {
                if (!entry.Filter(target))
                    return false;
            }
            catch (Exception exception)
            {
                PetLog.Error(entry.Owner, eventName, exception);
                return false;
            }
        }

        return true;
    }

    private static Subscription[] Snapshot(List<Subscription> items)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!PetRegistry.IsOwnerLoaded(items[i].Owner))
                items.RemoveAt(i);
        }

        Subscription[] snapshot = items.ToArray();
        Array.Sort(snapshot, static (a, b) =>
        {
            int byPriority = ((byte)a.Priority).CompareTo((byte)b.Priority);
            return byPriority != 0 ? byPriority : a.Sequence.CompareTo(b.Sequence);
        });
        return snapshot;
    }

    private static bool RemoveFirst(List<Subscription> items, Mod owner, Delegate handler)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Subscription entry = items[i];
            if (ReferenceEquals(entry.Owner, owner) && entry.Handler.Equals(handler))
            {
                items.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private static int RemoveOwner(List<Subscription> items, Mod owner)
    {
        int removed = 0;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(items[i].Owner, owner))
                continue;

            items.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    /// <summary>One live subscription: owner, payload handler, ordering, and target filters.</summary>
    private sealed record Subscription(
        Mod Owner,
        Delegate Handler,
        PetPriority Priority,
        long Sequence,
        PetTargetKind? TargetKind,
        Func<PetTarget, bool>? Filter);

    /// <summary>
    /// Typed wrapper around the live subscription list. The delegate type parameter keeps the
    /// legacy Add overloads unambiguous so the v1 methods can stay source identical.
    /// </summary>
    private sealed class SubscriptionList<TDelegate>
        where TDelegate : Delegate
    {
        internal List<Subscription> Items { get; } = new();

        internal void Add(Mod owner, TDelegate handler, PetHandlerOptions? options)
        {
            PetHandlerOptions effective = options ?? PetHandlerOptions.Default;
            Items.Add(new Subscription(owner, handler, effective.Priority, nextSequence++, effective.TargetKind, effective.Filter));
        }
    }
}
