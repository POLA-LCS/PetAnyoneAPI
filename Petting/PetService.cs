using Microsoft.Xna.Framework;
using Terraria;

namespace PetAnyone;

/// <summary>
/// Core, mod-agnostic petting logic: range/cooldown constants, target discovery, shared rule
/// checks and visuals (love hearts, reach angle data) plus the tick bookkeeping used for
/// cooldowns. Anything that needs a tModLoader hook - input, packets, sound, reach animation -
/// lives in the consuming mod. Mod-specific effects subscribe through <see cref="PetEvents"/>.
/// </summary>
public static class PetService
{
    /// <summary>How far, in tiles, a patter can reach a target.</summary>
    public const int PetRangeTiles = 3;

    /// <summary>Cooldown in ticks between tap pets against the same target.</summary>
    public const int PetCooldownTicks = 20;

    /// <summary>How long the reach arm animation stays up after a pet.</summary>
    public const int PetReachDurationTicks = 30;

    /// <summary>Minimum ticks between love hearts per target, no matter how often a hold refreshes.</summary>
    public const int PetHeartIntervalTicks = 30;

    /// <summary>How often a hold refreshes the pet while right-click is held.</summary>
    public const int PetRefreshTicks = 10;

    /// <summary>Reach arm angle used for morphed targets, overridden by a consumer mod.</summary>
    public const float PetAngleMorphed = 0.27f;

    /// <summary>Default reach arm angle, matching vanilla's petting pose.</summary>
    public const float PetAngleVanilla = 0.37f;

    /// <summary>Gore type 331 is the full bright heart used by the Love Potion.</summary>
    private const int LoveHeartGore = 331;

    /// <summary>Terraria tile size in pixels.</summary>
    private const float TilePixels = 16f;

    private static readonly Dictionary<(PetTargetKind Kind, int Index), int> LastHeartTick = new();
    private static readonly Dictionary<int, int> PlayerLastPetTick = new();
    private static readonly Dictionary<int, int> NpcLastPetTick = new();

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
    /// Core application: per-target cooldown bookkeeping and the OnPetStart/OnPetHold event. The
    /// consuming mod decides when a tap or hold applies and owns all networking around it.
    /// </summary>
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

    /// <summary>
    /// Applies a synced pet on a client: shared visuals and core bookkeeping. The consuming mod
    /// adds its own sound and reach animation around the call.
    /// </summary>
    public static void HandleSyncedPet(Player? patter, PetTarget target)
    {
        if (!target.IsActive)
            return;

        PlayPetHeartSynced(target);
        ApplyPetCore(patter, target, bypassCooldown: true);
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
    /// Spawns at most one heart per target every <see cref="PetHeartIntervalTicks"/>, no matter how
    /// often the hold refresh or a network broadcast asks for one. Sound is owned by the consumer.
    /// </summary>
    public static void PlayPetHeartSynced(PetTarget target)
    {
        if (Main.dedServ || !target.IsActive)
            return;

        var key = (target.Kind, target.Index);
        int now = (int)Main.GameUpdateCount;
        if (LastHeartTick.TryGetValue(key, out int last) && now - last < PetHeartIntervalTicks)
            return;

        LastHeartTick[key] = now;
        SpawnPetHeart(target);
    }

    /// <summary>Spawns one Love Potion style full heart that gently floats up over the target.</summary>
    private static void SpawnPetHeart(PetTarget target)
    {
        if (!target.TryGetEntity(out Entity? entity) || entity is null)
            return;

        Vector2 position = entity.Center
            + new Vector2(0f, -entity.Hitbox.Height * 0.6f)
            + Main.rand.NextVector2Circular(6f, 4f);

        int index = Gore.NewGore(
            entity.GetSource_FromThis(),
            position,
            Vector2.Zero,
            LoveHeartGore,
            Main.rand.NextFloat(0.55f, 0.85f));

        if (index < 0 || index >= Main.gore.Length)
            return;

        Gore heart = Main.gore[index];
        heart.sticky = false;
        heart.velocity = new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.9f, -0.5f));
        heart.rotation = 0f;
        heart.timeLeft = 70;
    }

    /// <summary>Drops all per-target tick and heart state; call this when the consuming mod unloads.</summary>
    public static void Clear()
    {
        LastHeartTick.Clear();
        PlayerLastPetTick.Clear();
        NpcLastPetTick.Clear();
    }

    private static bool WithinPixels(Vector2 a, Vector2 b, float pixels)
    {
        return Vector2.DistanceSquared(a, b) <= pixels * pixels;
    }
}
