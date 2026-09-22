using Microsoft.Xna.Framework;
using Terraria;

namespace PetAnyone;

/// <summary>
/// Core, mod-agnostic petting logic: range/cooldown constants, target discovery, shared rule
/// checks (reach angle data) plus the tick bookkeeping used for cooldowns. Anything that needs
/// a tModLoader hook - input, packets, sound, reach animation, visuals such as hearts - lives
/// in the consuming mod. Mod-specific effects subscribe through <see cref="PetEvents"/>.
/// </summary>
public static class PetService
{
    /// <summary>How far, in tiles, a patter can reach a target.</summary>
    public const int PetRangeTiles = 3;

    /// <summary>Cooldown in ticks between tap pets against the same target.</summary>
    public const int PetCooldownTicks = 20;

    /// <summary>How long the reach arm animation stays up after a pet.</summary>
    public const int PetReachDurationTicks = 30;

    /// <summary>How often a hold refreshes the pet while right-click is held.</summary>
    public const int PetRefreshTicks = 10;

    /// <summary>Ticks without a refresh before a session auto ends. New in v2.</summary>
    public const int PetHoldTimeoutTicks = PetRefreshTicks * 2;

    /// <summary>Reach arm angle used for morphed targets, overridden by a consumer mod.</summary>
    public const float PetAngleMorphed = 0.27f;

    /// <summary>Default reach arm angle, matching vanilla's petting pose.</summary>
    public const float PetAngleVanilla = 0.37f;

    /// <summary>Terraria tile size in pixels.</summary>
    private const float TilePixels = 16f;

    /// <summary>Ticks between sweeps of stale cooldown entries.</summary>
    private const int TickSweepInterval = 300;

    private static readonly Dictionary<int, int> PlayerLastPetTick = new();
    private static readonly Dictionary<int, int> NpcLastPetTick = new();

    /// <summary>Open sessions keyed by patter index, target kind, and target index. Indices only.</summary>
    private static readonly Dictionary<SessionKey, SessionState> Sessions = new();

    /// <summary>Game update tick of the last <see cref="Tick"/> pump. Extra calls in one tick are no-ops.</summary>
    private static long LastProcessedTick = long.MinValue;

    /// <summary>
    /// Whether this player's right-click is free for petting: the empty hand always works, and
    /// consumers can register extra allowed items through <see cref="PetRegistry.RegisterPetHandItem"/>.
    /// </summary>
    public static bool IsPetHand(Player? patter)
    {
        if (patter is null)
            return false;
        Item held = patter.HeldItem;
        return held.IsAir || PetRegistry.AllowsPettingItem(held);
    }

    /// <summary>Finds the pettable player or NPC under the cursor, preferring players.</summary>
    public static PetTarget FindTargetUnderCursor(Player patter)
    {
        return TryFindTargetUnderCursor(patter, out PetTarget target) ? target : PetTarget.None;
    }

    /// <summary>Finds the pettable player or NPC under the cursor within reach, preferring players.</summary>
    public static bool TryFindTargetUnderCursor(Player? patter, out PetTarget target)
    {
        target = PetTarget.None;
        if (patter is null || !patter.active || patter.dead)
            return false;

        float rangePx = PetRangeTiles * TilePixels;
        Point cursor = Main.MouseWorld.ToPoint();

        for (int i = 0; i < Main.player.Length; i++)
        {
            Player candidate = Main.player[i];
            if (candidate is null || !candidate.active || candidate.dead)
                continue;
            if (candidate.whoAmI == patter.whoAmI)
                continue;
            if (!candidate.Hitbox.Contains(cursor))
                continue;
            if (!WithinPixels(patter.Center, candidate.Center, rangePx))
                continue;
            if (!PetRegistry.IsPlayerPettable(candidate))
                continue;

            target = PetTarget.FromPlayer(candidate);
            return true;
        }

        for (int i = 0; i < Main.npc.Length; i++)
        {
            NPC candidate = Main.npc[i];
            if (candidate is null || !candidate.active || candidate.life <= 0)
                continue;
            if (!candidate.Hitbox.Contains(cursor))
                continue;
            if (!WithinPixels(patter.Center, candidate.Center, rangePx))
                continue;
            if (!PetRegistry.IsNpcPettable(candidate))
                continue;

            target = PetTarget.FromNpc(candidate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="patter"/> may pet <paramref name="target"/> right now: both alive,
    /// in range, target registered, and no <c>CanPet</c> subscriber vetoed it.
    /// </summary>
    public static bool CanPet(Player? patter, PetTarget target)
    {
        if (patter is null || !patter.active || patter.dead)
            return false;
        if (!target.IsActive)
            return false;

        if (target.IsPlayer)
        {
            if (!target.TryGetPlayer(out Player? player) || player.dead || player.whoAmI == patter.whoAmI)
                return false;
            if (!PetRegistry.IsPlayerPettable(player))
                return false;
        }
        else
        {
            if (!target.TryGetNpc(out NPC? npc) || !npc.active || npc.life <= 0)
                return false;
            if (!PetRegistry.IsNpcPettable(npc))
                return false;
        }

        if (!WithinPixels(patter.Center, target.Center, PetRangeTiles * TilePixels))
            return false;

        return PetEvents.CanPet(new PetContext(patter, target));
    }

    /// <summary>
    /// The recommended apply entry point. A local tap runs the full gate: target, registry, range,
    /// the <c>CanPet</c> event, and the per target cooldown. A local hold, a synced replay, and a
    /// manual application check the target only. Every path records the session and dispatches
    /// <see cref="PetStartEvent"/> or <see cref="PetHoldEvent"/>.
    /// </summary>
    /// <param name="patter">The player applying the pet, or null when the caller has no player.</param>
    /// <param name="target">The player or NPC being petted.</param>
    /// <param name="mode">Tap or hold intent. The first application of a session opens with this mode.</param>
    /// <param name="source">Local, Synced, or Manual. Only <see cref="PetEventSource.Local"/> runs the full gate.</param>
    public static PetApplyResult TryApplyPet(
        Player? patter,
        PetTarget target,
        PetApplyMode mode,
        PetEventSource source = PetEventSource.Local)
    {
        ExpireStaleSessions();

        if (source == PetEventSource.Local && mode == PetApplyMode.Tap)
            return ApplyLocalTap(patter, target, source);

        return ApplySessionEvent(patter, target, mode, source);
    }

    /// <summary>Deprecated. Kept for v1 binaries. Prefer <see cref="TryApplyPet"/>.</summary>
    [Obsolete("Use PetService.TryApplyPet(patter, target, mode) instead.")]
    public static bool ApplyPetCore(Player? patter, PetTarget target, bool bypassCooldown)
    {
        if (!target.IsActive)
            return false;

        int now = (int)Main.GameUpdateCount;
        if (!bypassCooldown && now - GetTargetLastPetTick(target) < PetCooldownTicks)
            return false;

        SetTargetLastPetTick(target, now);
        var context = new PetContext(patter, target);
        if (bypassCooldown)
            PetEvents.RaisePetHold(context);
        else
            PetEvents.RaisePetStart(context);
        return true;
    }

    /// <summary>Deprecated. Kept for v1 binaries. Prefer <see cref="TryApplyPet"/> with <see cref="PetEventSource.Synced"/>.</summary>
    [Obsolete("Use PetService.TryApplyPet(patter, target, mode, PetEventSource.Synced) instead.")]
    public static void HandleSyncedPet(Player? patter, PetTarget target)
    {
        if (!target.IsActive)
            return;

        PlayPetHeartSynced(target);
        ApplyPetCore(patter, target, bypassCooldown: true);
    }

    /// <summary>
    /// Closes the active session for the patter and target pair, raises <see cref="PetEndEvent"/>
    /// with the given reason and source, and returns whether a session was closed. Prefer this over
    /// raising end manually so session state stays consistent.
    /// </summary>
    public static bool EndPet(
        Player? patter,
        PetTarget target,
        PetEndReason reason = PetEndReason.Released,
        PetEventSource source = PetEventSource.Local)
    {
        ExpireStaleSessions();
        return CloseSession(KeyFor(patter, target), reason, source);
    }

    /// <summary>
    /// True when any active session targets this entity. Two patters on one target produce two
    /// sessions, so this answers whether anyone is petting the target. Side effect free.
    /// </summary>
    public static bool IsPetActive(PetTarget target)
    {
        foreach (SessionKey key in Sessions.Keys)
        {
            if (key.Kind == target.Kind && key.Index == target.Index)
                return true;
        }

        return false;
    }

    /// <summary>True when an active session exists for this exact patter and target pair. Side effect free.</summary>
    public static bool IsPetActive(Player? patter, PetTarget target)
    {
        return Sessions.ContainsKey(KeyFor(patter, target));
    }

    /// <summary>
    /// Copies the active session for the target when one exists. When two patters hold sessions on
    /// the same target, the most recently refreshed session wins, ties broken by the newest start.
    /// Use <see cref="IsPetActive(Player, PetTarget)"/> for one specific pair. Side effect free.
    /// </summary>
    public static bool TryGetActiveSession(PetTarget target, out PetSessionInfo session)
    {
        session = default;
        SessionState? best = null;
        SessionKey bestKey = default;

        foreach (KeyValuePair<SessionKey, SessionState> pair in Sessions)
        {
            if (pair.Key.Kind != target.Kind || pair.Key.Index != target.Index)
                continue;

            if (best is null
                || pair.Value.LastRefreshTick > best.LastRefreshTick
                || (pair.Value.LastRefreshTick == best.LastRefreshTick && pair.Value.StartedTick > best.StartedTick))
            {
                best = pair.Value;
                bestKey = pair.Key;
            }
        }

        if (best is null)
            return false;

        session = new PetSessionInfo(
            new PetTarget(bestKey.Kind, bestKey.Index),
            ResolvePatter(bestKey.PatterIndex),
            best.Source,
            best.StartedTick,
            best.LastRefreshTick);
        return true;
    }

    /// <summary>Angle used for the reach arm; consumers can override it per target.</summary>
    public static float GetReachAngle(PetTarget target)
    {
        return PetEvents.TryGetReachAngle(target, out float angle) ? angle : PetAngleVanilla;
    }

    /// <summary>Tick of the last applied pet against this player, tracked per player index.</summary>
    public static int GetLastPetTick(Player player)
    {
        return PlayerLastPetTick.TryGetValue(player.whoAmI, out int tick) ? tick : -PetCooldownTicks;
    }

    /// <summary>Records the tick of the last applied pet against this player.</summary>
    public static void SetLastPetTick(Player player, int tick)
    {
        PlayerLastPetTick[player.whoAmI] = tick;
    }

    /// <summary>Tick of the last applied pet against this target (player or NPC).</summary>
    public static int GetTargetLastPetTick(PetTarget target)
    {
        if (target.IsPlayer && target.TryGetPlayer(out Player? player))
            return GetLastPetTick(player);
        if (target.IsNpc)
            return NpcLastPetTick.TryGetValue(target.Index, out int last) ? last : -PetCooldownTicks;
        return -PetCooldownTicks;
    }

    /// <summary>Records the tick of the last applied pet against this target.</summary>
    public static void SetTargetLastPetTick(PetTarget target, int tick)
    {
        if (target.IsPlayer && target.TryGetPlayer(out Player? player))
        {
            SetLastPetTick(player, tick);
            return;
        }
        if (target.IsNpc)
            NpcLastPetTick[target.Index] = tick;
    }

    /// <summary>
    /// Deprecated. The API does not render visuals. Subscribe to <see cref="PetEvents.OnPetStart"/>,
    /// <see cref="PetEvents.OnPetHold"/>, and <see cref="PetEvents.OnPetEnd"/> and spawn your own
    /// effects such as heart particles from the consuming mod.
    /// </summary>
    [Obsolete("The API no longer spawns heart visuals. Use PetEvents.OnPetStart, OnPetHold, and OnPetEnd and render your own effects.")]
    public static void PlayPetHeartSynced(PetTarget target)
    {
    }

    /// <summary>
    /// Per tick pump: expires sessions that missed their refresh window and, every 300 ticks,
    /// sweeps cooldown entries whose entity index went stale or inactive. Safe to call
    /// repeatedly inside one game tick; only the first call does work.
    /// </summary>
    public static void Tick()
    {
        long now = Main.GameUpdateCount;
        if (LastProcessedTick == now)
            return;
        LastProcessedTick = now;

        ExpireStaleSessions();

        if (now % TickSweepInterval == 0)
            SweepStaleTickState();
    }

    /// <summary>
    /// Clears sessions, cooldown ticks, and tick guards without touching registrations or event
    /// handlers. Call this from a world unload hook. New in v2.
    /// </summary>
    public static void ResetWorldState()
    {
        Sessions.Clear();
        PlayerLastPetTick.Clear();
        NpcLastPetTick.Clear();
        LastProcessedTick = long.MinValue;
    }

    /// <summary>Drops all session and cooldown state; call this when the consuming mod unloads.</summary>
    public static void Clear()
    {
        ResetWorldState();
    }

    // Private session plumbing. Sessions key on indices only and never hold entity references.

    private static PetApplyResult ApplyLocalTap(Player? patter, PetTarget target, PetEventSource source)
    {
        if (!target.IsAlive)
            return PetApplyResult.Reject(PetApplyCode.RejectedTarget);

        if (!PassesRegistryGate(patter, target))
            return PetApplyResult.Reject(PetApplyCode.RejectedRegistry);

        // A null patter has no reach, so it cannot pass the range gate.
        if (patter is null || !WithinPixels(patter.Center, target.Center, PetRangeTiles * TilePixels))
            return PetApplyResult.Reject(PetApplyCode.RejectedRange);

        var context = new PetContext(patter, target);
        if (!PetEvents.RaiseCanPet(context, PetEventSource.Local))
            return PetApplyResult.Reject(PetApplyCode.RejectedCancelled);

        long now = Main.GameUpdateCount;
        int nowTick = (int)now;
        if (nowTick - GetTargetLastPetTick(target) < PetCooldownTicks)
            return PetApplyResult.Reject(PetApplyCode.RejectedCooldown);

        SetTargetLastPetTick(target, nowTick);

        SessionKey key = KeyFor(patter, target);
        CloseSessionWithStoredSource(key, PetEndReason.Replaced);

        Sessions[key] = new SessionState(source, now, now);
        PetEvents.RaisePetStart(new PetStartEvent(context, source, PetApplyMode.Tap));

        return PetApplyResult.Success;
    }

    private static PetApplyResult ApplySessionEvent(Player? patter, PetTarget target, PetApplyMode mode, PetEventSource source)
    {
        SessionKey key = KeyFor(patter, target);
        if (!target.IsAlive)
        {
            CloseSessionWithStoredSource(key, PetEndReason.TargetLost);
            return PetApplyResult.Reject(PetApplyCode.RejectedTarget);
        }

        long now = Main.GameUpdateCount;
        var context = new PetContext(patter, target);

        if (Sessions.TryGetValue(key, out SessionState? session))
        {
            session.LastRefreshTick = now;
            PetEvents.RaisePetHold(new PetHoldEvent(context, source));
        }
        else
        {
            Sessions[key] = new SessionState(source, now, now);
            PetEvents.RaisePetStart(new PetStartEvent(context, source, mode));
        }

        return PetApplyResult.Success;
    }

    private static bool PassesRegistryGate(Player? patter, PetTarget target)
    {
        if (target.IsPlayer)
        {
            if (!target.TryGetPlayer(out Player? player) || player.dead)
                return false;
            if (patter is not null && player.whoAmI == patter.whoAmI)
                return false;
            return PetRegistry.IsPlayerPettable(player);
        }

        if (!target.TryGetNpc(out NPC? npc) || !npc.active || npc.life <= 0)
            return false;

        return PetRegistry.IsNpcPettable(npc);
    }

    private static SessionKey KeyFor(Player? patter, PetTarget target)
    {
        return new SessionKey(patter?.whoAmI ?? -1, target.Kind, target.Index);
    }

    private static Player? ResolvePatter(int patterIndex)
    {
        if ((uint)patterIndex >= (uint)Main.player.Length)
            return null;
        return Main.player[patterIndex];
    }

    private static bool CloseSession(SessionKey key, PetEndReason reason, PetEventSource source)
    {
        if (!Sessions.Remove(key))
            return false;

        RaiseSessionEnd(key, reason, source);
        return true;
    }

    /// <summary>Closes a session using the source it was opened with, per the session contract.</summary>
    private static bool CloseSessionWithStoredSource(SessionKey key, PetEndReason reason)
    {
        if (!Sessions.TryGetValue(key, out SessionState? session))
            return false;

        return CloseSession(key, reason, session.Source);
    }

    private static void RaiseSessionEnd(SessionKey key, PetEndReason reason, PetEventSource source)
    {
        var target = new PetTarget(key.Kind, key.Index);
        var context = new PetContext(ResolvePatter(key.PatterIndex), target);
        PetEvents.RaisePetEnd(new PetEndEvent(context, source, reason));
    }

    private static void ExpireStaleSessions()
    {
        if (Sessions.Count == 0)
            return;

        long now = Main.GameUpdateCount;
        var expired = new List<KeyValuePair<SessionKey, SessionState>>();
        foreach (KeyValuePair<SessionKey, SessionState> pair in Sessions)
        {
            if (now - pair.Value.LastRefreshTick <= PetHoldTimeoutTicks)
                continue;
            expired.Add(pair);
        }

        for (int i = 0; i < expired.Count; i++)
        {
            KeyValuePair<SessionKey, SessionState> pair = expired[i];
            Sessions.Remove(pair.Key);
            PetEndReason reason = new PetTarget(pair.Key.Kind, pair.Key.Index).IsActive
                ? PetEndReason.Timeout
                : PetEndReason.TargetLost;
            RaiseSessionEnd(pair.Key, reason, pair.Value.Source);
        }
    }

    private static void SweepStaleTickState()
    {
        var stalePlayerIndices = new List<int>();
        foreach (int index in PlayerLastPetTick.Keys)
        {
            if (!IsPlayerIndexActive(index))
                stalePlayerIndices.Add(index);
        }

        for (int i = 0; i < stalePlayerIndices.Count; i++)
            PlayerLastPetTick.Remove(stalePlayerIndices[i]);

        var staleNpcIndices = new List<int>();
        foreach (int index in NpcLastPetTick.Keys)
        {
            if (!IsNpcIndexActive(index))
                staleNpcIndices.Add(index);
        }

        for (int i = 0; i < staleNpcIndices.Count; i++)
            NpcLastPetTick.Remove(staleNpcIndices[i]);
    }

    private static bool IsPlayerIndexActive(int index)
    {
        if ((uint)index >= (uint)Main.player.Length)
            return false;
        Player? player = Main.player[index];
        return player is not null && player.active;
    }

    private static bool IsNpcIndexActive(int index)
    {
        if ((uint)index >= (uint)Main.npc.Length)
            return false;
        NPC? npc = Main.npc[index];
        return npc is not null && npc.active;
    }

    /// <summary>Index-only key for one pet session: patter index, target kind, and target index.</summary>
    private readonly record struct SessionKey(int PatterIndex, PetTargetKind Kind, int Index);

    /// <summary>Per session bookkeeping: opening source and tick stamps only, never entity references.</summary>
    private sealed class SessionState
    {
        internal SessionState(PetEventSource source, long startedTick, long lastRefreshTick)
        {
            Source = source;
            StartedTick = startedTick;
            LastRefreshTick = lastRefreshTick;
        }

        internal PetEventSource Source { get; }

        internal long StartedTick { get; }

        internal long LastRefreshTick { get; set; }
    }

    private static bool WithinPixels(Vector2 a, Vector2 b, float pixels)
    {
        return Vector2.DistanceSquared(a, b) <= pixels * pixels;
    }
}
