# PetAnyoneAPI v1 Baseline

Captured on 2026-09-22 from the repository root.

Purpose: freeze the v1 public surface, the documentation warning baseline, and the observed v1 behavior so every v2 gate can diff against a fixed reference.

## Commits

- HEAD at capture time: `7b0d66b1d26fe31f6df76cf225c9f64eeacf7f26` ("setup: added icons"). This commit landed during the baseline session, after the task brief was written, and it only adds the three icon files.
- HEAD recorded in the task brief at session start: `9bd40f3e46f0c22926d6894862a4c8b876860462` ("Ignore harness context state"). It is the parent of `7b0d66b`.
- Source baseline commit used by every v1 extraction and diff in this document: `3bb1244dc3ad858e008a03ef86017938804ffa21` ("Clarify BuildMod comment for DLL consumers").

This file is committed as baseline commit c1 on top of `7b0d66b`. The three icon files are tracked in `7b0d66b`, so `git status --porcelain` is clean at capture time.

## Public surface inventory

Command, run from the repository root:

```
git grep -h -E '^[[:space:]]*public ' 3bb1244 -- Petting
```

Output, 99 lines in file order:

```csharp
public readonly struct PetContext(Player? patter, PetTarget target)
    public Player? Patter { get; } = patter;
    public PetTarget Target { get; } = target;
    public bool IsValid => Target.IsActive;
public sealed class PetEventCallbacks
    public Func<PetContext, bool>? CanPet { get; init; }
    public Action<PetContext>? OnPetStart { get; init; }
    public Action<PetContext>? OnPetHold { get; init; }
    public Action<PetContext>? OnPetEnd { get; init; }
public static class PetEvents
    public static void RegisterCanPet(Mod owner, Func<PetContext, bool> handler) => Add(canPetHandlers, owner, handler);
    public static void RegisterOnPetStart(Mod owner, Action<PetContext> handler) => Add(petStartHandlers, owner, handler);
    public static void RegisterOnPetHold(Mod owner, Action<PetContext> handler) => Add(petHoldHandlers, owner, handler);
    public static void RegisterOnPetEnd(Mod owner, Action<PetContext> handler) => Add(petEndHandlers, owner, handler);
    public static void RegisterReachAngle(Mod owner, Func<PetTarget, float?> provider) => Add(reachAngleProviders, owner, provider);
    public static void RegisterCallbacks(Mod owner, PetEventCallbacks? callbacks)
    public static bool CanPet(PetContext context)
    public static void RaisePetStart(PetContext context) => Raise(petStartHandlers, context);
    public static void RaisePetHold(PetContext context) => Raise(petHoldHandlers, context);
    public static void RaisePetEnd(PetContext context) => Raise(petEndHandlers, context);
    public static bool TryGetReachAngle(PetTarget target, out float angle)
    public static void Clear()
public sealed class PetNpcDefinition(string? displayName = null, bool allowWorldPet = true, bool showChatButton = true, string? buttonText = null)
    public static readonly PetNpcDefinition Default = new();
    public string? DisplayName { get; } = displayName;
    public bool AllowWorldPet { get; } = allowWorldPet;
    public bool ShowChatButton { get; } = showChatButton;
    public string ButtonText { get; } = string.IsNullOrWhiteSpace(buttonText) ? "pet <3" : buttonText;
public static class PetRegistry
    public static void RegisterNpc(Mod owner, int npcType, PetNpcDefinition? definition = null)
    public static bool UnregisterNpc(Mod owner, int npcType)
    public static void RegisterNpcRule(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition = null)
    public static bool UnregisterNpcRule(Mod owner, Func<NPC, bool> predicate)
    public static bool TryGetNpcDefinition(NPC npc, [NotNullWhen(true)] out PetNpcDefinition? definition)
    public static bool IsNpcPettable(NPC npc) => npc is not null && TryGetNpcDefinition(npc, out _);
    public static void RegisterPlayerRule(Mod owner, Func<Player, bool> isPettable)
    public static bool UnregisterPlayerRule(Mod owner, Func<Player, bool> isPettable)
    public static void RegisterPlayerRequirement(Mod owner, Func<Player, bool> predicate)
    public static bool UnregisterPlayerRequirement(Mod owner, Func<Player, bool> predicate)
    public static bool IsPlayerPettable(Player player)
    public static void RegisterPetHandItem(Mod owner, Func<Item, bool> allowsPetting)
    public static bool UnregisterPetHandItem(Mod owner, Func<Item, bool> allowsPetting)
    public static bool AllowsPettingItem(Item item)
    public static bool IsOwnerLoaded(Mod owner)
    public static void Clear()
public static class PetService
    public const int PetRangeTiles = 3;
    public const int PetCooldownTicks = 20;
    public const int PetReachDurationTicks = 30;
    public const int PetHeartIntervalTicks = 30;
    public const int PetRefreshTicks = 10;
    public const float PetAngleMorphed = 0.27f;
    public const float PetAngleVanilla = 0.37f;
    public static bool IsPetHand(Player? patter)
    public static PetTarget FindTargetUnderCursor(Player patter)
    public static bool TryFindTargetUnderCursor(Player? patter, out PetTarget target)
    public static bool CanPet(Player? patter, PetTarget target)
    public static bool ApplyPetCore(Player? patter, PetTarget target, bool bypassCooldown)
    public static void HandleSyncedPet(Player? patter, PetTarget target)
    public static float GetReachAngle(PetTarget target)
    public static int GetLastPetTick(Player player)
    public static void SetLastPetTick(Player player, int tick)
    public static int GetTargetLastPetTick(PetTarget target)
    public static void SetTargetLastPetTick(PetTarget target, int tick)
    public static void PlayPetHeartSynced(PetTarget target)
    public static void Clear()
public readonly struct PetTarget(PetTargetKind kind, int index) : IEquatable<PetTarget>
    public static readonly PetTarget None = new(PetTargetKind.Player, -1);
    public PetTargetKind Kind { get; } = kind;
    public int Index { get; } = index;
    public bool IsPlayer => Kind == PetTargetKind.Player;
    public bool IsNpc => Kind == PetTargetKind.Npc;
    public bool IsValid => TryGetEntity(out _);
    public bool IsActive => TryGetEntity(out Entity? entity) && entity.active;
    public bool IsAlive => Kind == PetTargetKind.Player
    public Entity? AsEntity => TryGetEntity(out Entity? entity) ? entity : null;
    public Vector2 Center => AsEntity?.Center ?? Vector2.Zero;
    public Rectangle Hitbox => AsEntity?.Hitbox ?? Rectangle.Empty;
    public string DisplayName => Kind == PetTargetKind.Player
    public bool TryGetPlayer([NotNullWhen(true)] out Player? player)
    public bool TryGetNpc([NotNullWhen(true)] out NPC? npc)
    public bool TryGetEntity([NotNullWhen(true)] out Entity? entity)
    public static PetTarget FromPlayer(Player? player) => player is null ? None : new PetTarget(PetTargetKind.Player, player.whoAmI);
    public static PetTarget FromNpc(NPC? npc) => npc is null ? None : new PetTarget(PetTargetKind.Npc, npc.whoAmI);
    public bool Equals(PetTarget other) => Kind == other.Kind && Index == other.Index;
    public override bool Equals(object? obj) => obj is PetTarget other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Kind, Index);
    public static bool operator ==(PetTarget left, PetTarget right) => left.Equals(right);
    public static bool operator !=(PetTarget left, PetTarget right) => !left.Equals(right);
    public override string ToString() => Kind == PetTargetKind.Player ? $"Player[{Index}]" : $"Npc[{Index}]";
public enum PetTargetKind : byte
public static class PettingApi
    public const int ApiVersion = 1;
    public static bool RegisterPettableNpc(Mod owner, int npcType, PetNpcDefinition? definition = null)
    public static bool RegisterPettableNpc(Mod owner, Func<NPC, bool> predicate, PetNpcDefinition? definition = null)
    public static bool RegisterPettablePlayer(Mod owner, Func<Player, bool> isPettable)
    public static bool RegisterPettablePlayerRequirement(Mod owner, Func<Player, bool> predicate)
    public static void RegisterEvents(Mod owner, PetEventCallbacks callbacks)
    public static object? Call(object[] args)
```

The anchored regex matches the declaration line of each member and not any following continuation lines. `PetTarget.IsAlive` and `PetTarget.DisplayName` each continue over two extra source lines that begin with a question mark or a colon. The counts below are per matched declaration line.

## Per file counts

Command:

```
git grep -c -E '^[[:space:]]*public ' 3bb1244 -- Petting
```

| File | Matching lines |
|---|---|
| Petting/PetContext.cs | 4 |
| Petting/PetEventCallbacks.cs | 5 |
| Petting/PetEvents.cs | 13 |
| Petting/PetNpcDefinition.cs | 6 |
| Petting/PetRegistry.cs | 17 |
| Petting/PetService.cs | 21 |
| Petting/PetTarget.cs | 24 |
| Petting/PetTargetKind.cs | 1 |
| Petting/PettingApi.cs | 8 |
| Total | 99 |

The counts include the matched type declaration line for each file. For example, PetContext 4 is the struct declaration plus three members, and PetTargetKind 1 is only the enum declaration.

## ApiVersion at the baseline

`PettingApi.ApiVersion` is 1. The declaration is `public const int ApiVersion = 1;` in `Petting/PettingApi.cs` in the `3bb1244` tree.

## Documentation warning baseline

Command:

```
dotnet build "C:\Users\zarap\Documents\My Games\Terraria\tModLoader\ModSources\PetAnyone\PetAnyoneAPI.csproj" /p:Configuration=Release /p:GenerateDocumentationFile=true
```

Result at capture time: exit 0, `Build succeeded.`, `20 Warning(s)`, `0 Error(s)`, elapsed 00:00:04.95. Every warning is CS1591 (missing XML comment for a publicly visible type or member). The console log prints each warning once inline and once again in the summary block, so the captured log contains 40 CS1591 text lines for 20 unique warnings.

| File | Unique CS1591 warnings |
|---|---|
| Petting/PetEvents.cs | 3 |
| Petting/PetTarget.cs | 15 |
| Petting/PetTargetKind.cs | 2 |
| Total | 20 |

The warned members:

- PetEvents: `RaisePetStart(PetContext)`, `RaisePetHold(PetContext)`, `RaisePetEnd(PetContext)`.
- PetTarget: `IsPlayer`, `IsNpc`, `Center`, `Hitbox`, `TryGetPlayer(out Player?)`, `TryGetNpc(out NPC?)`, `TryGetEntity(out Entity?)`, `FromPlayer(Player?)`, `FromNpc(NPC?)`, `Equals(PetTarget)`, `Equals(object?)`, `GetHashCode()`, `operator ==(PetTarget, PetTarget)`, `operator !=(PetTarget, PetTarget)`, `ToString()`.
- PetTargetKind: `Player`, `Npc`.

The plain release build without the documentation flag is green with `0 Warning(s)` and `0 Error(s)`, so this CS1591 set is the entire documentation debt at the baseline. Later gates count only CS1591 warnings that reference new v2 files, because these v1 warnings already exist.

## Behavior baseline

The table is the compatibility reference for the v2 shims. Line numbers are in the `3bb1244` tree.

| Member | Baseline behavior |
|---|---|
| `PetService.ApplyPetCore(Player?, PetTarget, bool)` | Returns false when the target is not active. With `bypassCooldown` false, returns false when `now - GetTargetLastPetTick(target) < 20`, writing no state. Otherwise records `SetTargetLastPetTick(target, now)`, builds a `PetContext`, raises `RaisePetHold` when bypass is true and `RaisePetStart` when false, then returns true. It does not validate the patter, and a null patter reaches the context as null. A hold records the tick too, so a following tap is inside the cooldown. A handler exception propagates after the tick was recorded. |
| `PetService.HandleSyncedPet(Player?, PetTarget)` | Returns immediately when the target is not active. Otherwise calls `PlayPetHeartSynced(target)` first and then `ApplyPetCore(patter, target, bypassCooldown: true)`. It never consults `CanPet`, range, or the registry, and it ignores the `ApplyPetCore` result. |
| `PetRegistry.UnregisterNpc(Mod, int)` | Refuses to remove an entry that another owner replaced. Returns true only when an entry for the type exists and its owner is reference-equal to the caller, then removes it. Returns false when no entry exists or the current entry belongs to a different owner, leaving that entry in place. `RegisterNpc` overwrites any existing entry regardless of owner, so the last registration for a type wins. |
| `PetService.PlayPetHeartSynced(PetTarget)` | Returns immediately on `Main.dedServ` or when the target is not active. The throttle key is `(Kind, Index)` and the throttled interval is 30 ticks. Inside the interval it returns without spawning and without updating the stored tick. Otherwise it records `now`, then spawns one gore of type 331 through `SpawnPetHeart`. The tick is recorded before the spawn attempt, so a missing entity or an out-of-range gore index still consumes the interval. The spawned gore is non-sticky, has `timeLeft` 70, `rotation` 0, x velocity in -0.25 to 0.25, and y velocity in -0.9 to -0.5. |
| `PetEvents.RegisterCanPet(Mod, Func<PetContext, bool>)` | Appends an owned handler to the `canPetHandlers` list in registration order. Throws `ArgumentNullException` when owner or handler is null. There is no dedupe and no priority. A handler returning false cancels the pet. |
| `PetEvents.CanPet(PetContext)` | Walks handlers in list order and prunes entries whose owner is not the loaded instance for that mod name. The first handler returning false stops the walk and returns false, so later handlers are not called and are not pruned. Returns true when the list is exhausted with no veto, including the empty list. Handler exceptions propagate, there is no per-handler isolation. |
| `PetService.CanPet(Player?, PetTarget)` | The gate: false when the patter is null, inactive, or dead, or when the target is not active. For a player target: must resolve, not be dead, not be the patter, and pass `PetRegistry.IsPlayerPettable`. For an NPC target: must resolve, be active, have `life > 0`, and pass `PetRegistry.IsNpcPettable`. Then the range check, squared center distance at most 48 * 48 pixels (`PetRangeTiles * 16`). Finally `PetEvents.CanPet` on the context, so any false from a subscriber cancels the whole gate. |

`PetService.CanPet` is the gate named in the plan behavior note "registry plus range plus handlers". The `PetEvents.CanPet` row above it is the subscriber dispatch that `RegisterCanPet` feeds.

Supporting facts:

- `GetTargetLastPetTick` returns `-PetCooldownTicks` when no tick is stored, so the first tap against a target is always allowed. The cooldown rejects only when the difference is strictly less than 20.
- `SetTargetLastPetTick` writes to `PlayerLastPetTick[whoAmI]` for player targets and `NpcLastPetTick[Index]` for NPC targets.
- `PetService.Clear` clears `LastHeartTick`, `PlayerLastPetTick`, and `NpcLastPetTick`.
- `PetEvents.Clear` clears the five handler lists. `PetRegistry.Clear` clears the five registry lists. The three Clears are the v1 unload path named in the README.

## Extraction and diff commands for later gates

Run from the repository root. The v2 working tree extraction uses `rg --sort path --no-filename` so file order matches git output.

```
git grep -h -E '^[[:space:]]*public ' 3bb1244 -- Petting > C:\Users\zarap\AppData\Local\Temp\petanyone-v1-surface.txt
rg --sort path --no-filename '^\s*public ' Petting > C:\Users\zarap\AppData\Local\Temp\petanyone-v2-surface.txt
git diff --no-index --word-diff=plain C:\Users\zarap\AppData\Local\Temp\petanyone-v1-surface.txt C:\Users\zarap\AppData\Local\Temp\petanyone-v2-surface.txt
```

Count command:

```
git grep -c -E '^[[:space:]]*public ' 3bb1244 -- Petting
```

Notes:

- The expected v1 total is 99 across the nine files.
- In this workspace the shell wrapper is nu, and `>` redirection is not applied for external commands. The capture used `| save -f <path>` as the equivalent. PowerShell and cmd apply `>` normally.
- At capture time both extraction files were 99 lines and `git diff --no-index` reported no differences, because `7b0d66b` adds only icon files and no v2 work has started.
