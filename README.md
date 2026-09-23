# PetAnyoneAPI

A shared petting interaction library for Terraria mods built on tModLoader, displayed as Pet Anyone API. It ships as a dependency mod: consumer mods add `modReferences = PetAnyoneAPI` and use the typed API directly, or call in through `Mod.Call`. The API owns registration rules, session state, and hook dispatch. Consuming mods own every tModLoader hook: input, packets, sound, animation, and visuals such as heart particles.

## Description / Usage Context

PetAnyoneAPI lets a player pet another player or a registered NPC. The player doing the petting is the patter. Targets are pettable players and registered NPCs, reached with a cursor, a chat button, or whatever affordance your mod builds on top.

The library is Terraria oriented on purpose. It is built against the Terraria and tModLoader types, including `Player`, `NPC`, `Item`, `Main`, and `Mod`, and it targets `net8.0`, which is the runtime tModLoader mods use. It ships as a tModLoader mod with internal name `PetAnyoneAPI`, and it also still builds as a plain `PetAnyoneAPI.dll` for consumers that prefer a frozen DLL reference.

What the API owns:

* `PetRegistry` is the registration table for pettable NPC types and predicate rules, player veto rules, player allow list requirements, and held item rules that define what counts as a petting hand. Entries are owner scoped and layered by priority.
* `PetEvents` holds the hook engine: `OnCanPet`, `OnPetStart`, `OnPetHold`, `OnPetEnd`, and `OnReachAngle`, with priorities, per target filters, snapshot dispatch, and exception isolation. The old `Register*` methods remain as obsolete adapters.
* `PetService` holds the shared rules and the validated apply path: range, cooldown, refresh, and reach constants, cursor target discovery, `TryApplyPet`, session tracking, `EndPet`, the reach angle, and the tick pump.
* `PettingApi` is the static entry point for consumer mods and the versioned `Mod.Call` dispatcher for cross mod registration.

What the consuming mod owns:

* Input, deciding when a tap or a hold applies, and all networking around every applied pet.
* Sound, particles, and heart visuals. The API never spawns visual effects.
* The reach arm animation, using `PetService.GetReachAngle`.
* Chat button and other UI affordances, using the flags in `PetNpcDefinition`.
* In DLL mode only: `PetService.Tick()`, `PetService.ResetWorldState()`, and the three `Clear` methods. In mod mode the API mod does this itself.

## Public API at a glance

* `PetTargetKind` and `PetTarget`: which kind of entity a target points at, and a copyable handle for a player or an NPC by index. Members: `None`, `Kind`, `Index`, `IsPlayer`, `IsNpc`, `IsValid`, `IsActive`, `IsAlive`, `AsEntity`, `Center`, `Hitbox`, `DisplayName`, `TryGetPlayer`, `TryGetNpc`, `TryGetEntity`, `FromPlayer`, `FromNpc`, plus the standard equality members and `ToString`.
* `PetContext`: the payload base data for every hook. Members: `Patter`, `Target`, `IsValid`.
* `PetNpcDefinition`: per NPC flags for consumers. Members: `Default`, `DisplayName`, `AllowWorldPet`, `ShowChatButton`, `ButtonText`.
* `PetEventCallbacks`: bundle for registering several hooks at once. Members: `CanPet`, `OnPetStart`, `OnPetHold`, `OnPetEnd`.
* `PetPriority`: dispatch order for handlers and registry entries. `High`, `Normal`, `Low`.
* `PetHandlerOptions`: subscription options. Members: `Default`, `Priority`, `TargetKind`, `Filter`.
* Event payloads: `PetEvent` base with `Context`, `Source`, `Issuer`, `Tick`, `Patter`, `Target`, `IsLocal`, `IsSynced`, `IsValid`. Subclasses: `CanPetEvent` with `Cancel` and `RejectionReason`, `PetStartEvent` with `Mode` and `IsHoldStart`, `PetHoldEvent`, `PetEndEvent` with `Reason`, all sealed.
* `PetApplyResult` and `PetApplyCode`: machine readable outcome of `TryApplyPet`. `PetApplyResult` members: `Applied`, `Code`, `Success`, `IsApplied`, `IsRejected`, `Reject`, `ToString`. Codes: `Applied`, `RejectedTarget`, `RejectedRegistry`, `RejectedRange`, `RejectedCancelled`, `RejectedCooldown`, `RejectedUnsupported`.
* `PetEventSource`: `Local`, `Synced`, `Manual`. `PetApplyMode`: `Tap`, `Hold`. `PetEndReason`: `Released`, `Timeout`, `TargetLost`, `Replaced`, `WorldUnload`, `Manual`.
* `PetSessionInfo`: readonly snapshot of an active session. Members: `Target`, `Patter`, `Source`, `StartedTick`, `LastRefreshTick`. `PetApiInfo`: version negotiation object. Members: `Major`, `Minor`, `Patch`, `VersionString`, `ModName`.
* `PetRegistry`: registration and lookup. Members: `RegisterNpc` (with a priority overload), `UnregisterNpc`, `RegisterNpcRule` (with a priority overload), `UnregisterNpcRule`, `TryGetNpcDefinition`, `IsNpcPettable`, `RegisterPlayerRule`, `UnregisterPlayerRule`, `RegisterPlayerRequirement`, `UnregisterPlayerRequirement`, `IsPlayerPettable`, `RegisterPetHandItem`, `UnregisterPetHandItem`, `AllowsPettingItem`, `IsOwnerLoaded`, `ClearOwner`, `Clear`.
* `PetEvents`: hooks and dispatch. Members: `OnCanPet`, `OnPetStart`, `OnPetHold`, `OnPetEnd`, `OnReachAngle`, `Unsubscribe`, `ClearOwner`, the obsolete `Register*` adapters, `RegisterCallbacks`, `CanPet`, `RaiseCanPet`, `RaisePetStart`, `RaisePetHold`, `RaisePetEnd`, `TryGetReachAngle`, `Clear`.
* `PetService`: shared rules and sessions. Members: `PetRangeTiles`, `PetCooldownTicks`, `PetReachDurationTicks`, `PetRefreshTicks`, `PetHoldTimeoutTicks`, `PetAngleMorphed`, `PetAngleVanilla`, `IsPetHand`, `FindTargetUnderCursor`, `TryFindTargetUnderCursor`, `CanPet`, `TryApplyPet`, `EndPet`, `IsPetActive` (two overloads), `TryGetActiveSession`, `GetReachAngle`, `GetLastPetTick`, `SetLastPetTick`, `GetTargetLastPetTick`, `SetTargetLastPetTick`, `Tick`, `ResetWorldState`, `Clear`, plus obsolete `ApplyPetCore`, `HandleSyncedPet`, and `PlayPetHeartSynced`.
* `PettingApi`: consumer entry point. Members: `ApiVersion`, `ApiMinorVersion`, `ApiVersionString`, `ModName`, `Info`, `RegisterPettableNpc` (type and predicate overloads, with priority), `RegisterPettablePlayer`, `RegisterPettablePlayerRequirement`, `RegisterPetHandItem`, `RegisterEvents`, `Call`.
* `PetAnyoneMod` and `PetAnyoneSystem` (namespace `PetAnyoneAPI`, mod builds only): the dependency mod lifecycle. They clear registries, handlers, and state on unload, end sessions and reset state on world transitions, and pump `PetService.Tick` every game update.

### Hook model

| Hook | When it fires | What you can do |
|---|---|---|
| `CanPetEvent` | before a local pet is accepted | set `Cancel` to veto, add `RejectionReason` |
| `PetStartEvent` | a session opens, for a tap or the first hold | read `Mode`, `Source`, `Patter`, `Target` |
| `PetHoldEvent` | each hold refresh, every `PetRefreshTicks` | refresh or advance your own effects |
| `PetEndEvent` | a session closes | read `Reason` and stop your effects |
| `OnReachAngle` | pulled by `GetReachAngle` | return the arm angle you want, first non null wins |

* Ordering is `PetPriority` ascending (High, then Normal, then Low), then registration order inside one priority. `CanPetEvent` short circuits on the first cancel. Every other hook runs all handlers.
* `PetHandlerOptions` can restrict a handler to a target kind or a filter predicate. Throwing filters and handlers are logged through the owner mod and never abort the remaining handlers.
* `Unsubscribe` removes one matching handler by delegate equality. Handlers registered through the obsolete `Register*` adapters are stored as wrapper delegates, so `Unsubscribe` cannot match them. Use `ClearOwner` or `Clear` for those. `ClearOwner` removes everything a mod owns and returns the count.

### Sessions

`PetService.TryApplyPet(patter, target, mode, source)` is the single recommended entry point.

* A local tap runs the full gate: target, registry, range, `CanPetEvent`, and the per target cooldown. It returns a `PetApplyResult`, so a rejected pet tells you why.
* A local hold and a synced replay check the target only. Remote clients do not re-run local authority gates.
* The first application opens a session and raises `PetStartEvent`. Later holds refresh it and raise `PetHoldEvent`. Every session ends exactly once, through `EndPet` or the automatic timeout after `PetHoldTimeoutTicks` (20 ticks) without a refresh. Expiry reports `Timeout` or `TargetLost`.
* `IsPetActive` answers whether a target is being petted. `TryGetActiveSession` returns a `PetSessionInfo` snapshot. Two patters on one target produce two sessions.
* `PetService.Tick()` expires sessions and sweeps stale cooldown state. In mod mode the API mod pumps it for you. In DLL mode call it from your own update hook.

### Sync contract

The API performs no networking. The applying client sends its own packet and calls `TryApplyPet` locally. Packet payloads should carry the patter player index, the target kind and index, the mode, and an end marker. On receive:

```csharp
PetService.TryApplyPet(patter, target, mode, PetEventSource.Synced);
```

End packets call `EndPet(patter, target, reason, PetEventSource.Synced)`. If an end packet is lost, the session still converges through the timeout. Do not replay your own packet locally, or hold events fire twice.

## Requirements

* tModLoader installed locally. The project imports the local tModLoader targets, and it is built with the .NET 8 SDK and C# 12.
* `BuildMod` is true, so the build produces `PetAnyoneAPI.tmod` with internal name `PetAnyoneAPI` for dependency consumers, and the same build still emits `bin\Release\net8.0\PetAnyoneAPI.dll` for DLL consumers.
* The old `BuildMod=false` mode remains available for library only builds with `dotnet build /p:Configuration=Release /p:BuildMod=false`.
* No NuGet packages.

### Two ways to use it

**Recommended: a tModLoader dependency mod.** Add the API to the consuming mod's `build.txt`:

```
modReferences = PetAnyoneAPI
```

The dependency resolves at runtime, so the API must be installed and enabled alongside the consumer. The consumer still needs a compile time reference in its `.csproj` pointing at `PetAnyoneAPI.dll`, either a `ProjectReference` to the sibling project or a `Reference` with a `HintPath` to the built DLL. The API types are then used directly, and `Mod.Call` stays available.

**Alternative: a compiled DLL, frozen at your version.** Ship `PetAnyoneAPI.dll` in your mod's `lib` folder:

```xml
<Reference Include="PetAnyoneAPI">
  <HintPath>lib\PetAnyoneAPI.dll</HintPath>
  <Private>False</Private>
</Reference>
```

```
dllReferences = PetAnyoneAPI
```

Use this path when you plan to customize the codebase and compile your own frozen DLL. You own the version you ship and the manual lifecycle calls it needs: `PetService.Tick()`, `PetService.ResetWorldState()`, and the three `Clear` methods on unload. One API assembly per process applies: two different frozen copies cannot share registrations because delegate identity differs.

### Behavior and unload safety

* Players are pettable by default when active and alive. NPCs are pettable only after registration, either per type or through a predicate rule. Rules resolve before type layers, each ordered by priority then recency, and definitions never merge.
* `RegisterPlayerRule` can veto petting a player. `RegisterPlayerRequirement` is an allow list, and every requirement must pass. A throwing predicate logs and resolves as false.
* The empty hand always allows petting. A held item only does when a `RegisterPetHandItem` rule allows it.
* Reach is 3 tiles (48 pixels) from center to center. A tap pet has a 20 tick cooldown per target, a hold refreshes every 10 ticks, the reach arm lasts 30 ticks, and sessions time out after 20 ticks without a refresh.
* Registrations and handlers are tagged with the owning `Mod` and pruned lazily, so an unloaded mod cannot leave dangling handlers behind.
* In mod mode the API mod clears all state on unload and resets world state on world transitions. In DLL mode, call `PetEvents.Clear()`, `PetRegistry.Clear()`, and `PetService.Clear()` from the consuming mod's unload path.

## Why Would You Use It

* You get the whole petting interaction without writing the registration table, the range and cooldown rules, the session lifecycle, or the synced replay yourself.
* Other mods can register their own pettable NPCs with their own labels through the same registry, including at runtime through `Mod.Call`, so the interaction grows without coupling mods together.
* The hook engine gives every consumer priority ordering, per target filters, and crash isolation without each consumer reinventing unsubscribe and unload safety.
* Registrations are tied to the owning mod and pruned automatically, so unloading is safe.
* Visuals stay yours. The API reports what is being petted and when. Hearts, sounds, and animations live in the mod that owns the content, so each mod can express its own personality.
* Shared constants keep reach, timing, and cooldown behavior consistent across every mod that uses it.

## Usage Examples

### Register content and hooks

```csharp
using PetAnyone;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

public sealed class PettingMod : Mod
{
    public override void Load()
    {
        // NPCs must be registered to be pettable, by type or by rule.
        PetRegistry.RegisterNpc(this, ModContent.NPCType<MyPet>(), new PetNpcDefinition(displayName: "My Pet"));
        PetRegistry.RegisterNpcRule(this, npc => npc.type == NPCID.Bunny, new PetNpcDefinition(buttonText: "Boop"), PetPriority.Normal);

        // Players are pettable by default, so a requirement narrows it down.
        PetRegistry.RegisterPlayerRequirement(this, player => player.GetModPlayer<MyPlayer>().IsPettable);

        // The empty hand always works. Other items need a rule.
        PetRegistry.RegisterPetHandItem(this, item => item.type == ModContent.ItemType<Clicker>());

        // Hooks with priorities, filters, and cancellation.
        PetEvents.OnCanPet(this, e => e.Cancel = !e.Target.IsAlive);
        PetEvents.OnPetStart(this, e => Main.NewText($"You pet {e.Target.DisplayName}."));
        PetEvents.OnPetHold(this, OnPetHold, new PetHandlerOptions { Priority = PetPriority.Low });
        PetEvents.OnPetEnd(this, e => Main.NewText($"Petting stopped ({e.Reason})."));
        PetEvents.OnReachAngle(this, target => target.IsNpc ? PetService.PetAngleMorphed : null);
    }
}
```

### Apply a pet from your own input

```csharp
public static void TryPet(Player patter)
{
    if (!PetService.IsPetHand(patter))
        return;

    if (!PetService.TryFindTargetUnderCursor(patter, out PetTarget target))
        return;

    // TryApplyPet runs the full gate for Local, so a separate CanPet call is not needed.
    PetApplyResult result = PetService.TryApplyPet(patter, target, PetApplyMode.Tap, PetEventSource.Local);
    if (!result.IsApplied)
        return;

    // Your packet, your sound, your animation here.
}
```

### Draw your own hearts while a pet is active

The API does not spawn visuals. React to the hooks and draw what fits your mod.

```csharp
private static long lastHeartTick;

private static void OnPetHold(PetHoldEvent e)
{
    if (Main.dedServ || !IsMyContent(e.Target))
        return;

    // Your own cadence. The example shows one heart every 30 ticks.
    long now = Main.GameUpdateCount;
    if (now - lastHeartTick < 30)
        return;

    lastHeartTick = now;
    SpawnHeart(e.Target);
}

private static void SpawnHeart(PetTarget target)
{
    if (!target.TryGetEntity(out Entity? entity) || entity is null)
        return;

    int index = Gore.NewGore(
        entity.GetSource_FromThis(),
        entity.Center + new Vector2(0f, -entity.Hitbox.Height * 0.6f),
        Vector2.Zero,
        331,
        Main.rand.NextFloat(0.55f, 0.85f));

    if (index < 0 || index >= Main.gore.Length)
        return;

    Gore heart = Main.gore[index];
    heart.sticky = false;
    heart.velocity = new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.9f, -0.5f));
    heart.rotation = 0f;
    heart.timeLeft = 70;
}
```

### Replay a synced pet

```csharp
// On each client, when the pet arrives over the network:
PetService.TryApplyPet(patter, target, mode, PetEventSource.Synced);

// When a release arrives, end promptly. Otherwise the session times out.
PetService.EndPet(patter, target, PetEndReason.Released, PetEventSource.Synced);

// The reach arm and sound stay in your mod:
int armTicks = PetService.PetReachDurationTicks;
float armAngle = PetService.GetReachAngle(target);
```

### Register from another mod with Mod.Call

v2 commands carry the API version as the second argument. Version 3 or later throws, so callers always know when to update.

```csharp
if (ModLoader.TryGetMod("PetAnyoneAPI", out Mod api) && api.Call("GetVersion") is int version && version >= 2)
{
    api.Call("RegisterPettableNpc", 2, this, ModContent.NPCType<MyPet>(), new PetNpcDefinition(buttonText: "Pet <3"), PetPriority.Normal);
    api.Call("Subscribe", 2, this, "PetHold", (Action<PetHoldEvent>)OnPetHold);
    api.Call("ClearOwner", 2, this); // on your unload path
}
```

Command table, arguments after the command name:

| Command | Arguments | Returns |
|---|---|---|
| `GetVersion` | none | `int`, 2 |
| `GetApi` | none | `PetApiInfo` |
| `RegisterPettableNpc` | `2, owner, npcType or Func<NPC,bool>, [definition], [priority]` | `bool` |
| `RegisterPettablePlayer` | `2, owner, Func<Player,bool>` | `bool` |
| `RegisterPettablePlayerRequirement` (alias `RegisterPlayerRequirement`) | `2, owner, Func<Player,bool>` | `bool` |
| `RegisterPetHandItem` | `2, owner, Func<Item,bool>` | `bool` |
| `RegisterReachAngle` | `2, owner, Func<PetTarget,float?>, [options]` | `bool` |
| `Subscribe` | `2, owner, eventName, handler, [options]` | `bool` |
| `Unsubscribe` | `2, owner, eventName, handler` | `bool` |
| `UnregisterPettableNpc` | `2, owner, npcType` | `bool` |
| `UnregisterPettableNpcRule` | `2, owner, Func<NPC,bool>` | `bool` |
| `UnregisterPettablePlayer` | `2, owner, Func<Player,bool>` | `bool` |
| `UnregisterPettablePlayerRequirement` (alias `UnregisterPlayerRequirement`) | `2, owner, Func<Player,bool>` | `bool` |
| `UnregisterPetHandItem` | `2, owner, Func<Item,bool>` | `bool` |
| `UnregisterReachAngle` | `2, owner, Func<PetTarget,float?>` | `bool` |
| `ClearOwner` | `2, owner` | `int` removed count |
| `CanPet` | `2, patter, target` | `bool` |
| `ApplyPet` | `2, patter, target, [mode], [source]` | `PetApplyResult` |
| `EndPet` | `2, patter, target, [reason], [source]` | `bool` |
| `IsPetActive` | `2, target` or `2, patter, target` | `bool` |
| `RegisterEvents` (v1 shape) | `owner, PetEventCallbacks` | `bool` |

Notes for `Mod.Call` users:

* A target is a boxed `PetTarget` or two ints, kind first (0 Player, 1 Npc) then index.
* Enum values: `PetApplyMode` Tap 0, Hold 1. `PetEventSource` Local 0, Synced 1, Manual 2. `PetEndReason` Released 0, Timeout 1, TargetLost 2, Replaced 3, WorldUnload 4, Manual 5. `PetPriority` High 0, Normal 100, Low 200.
* `Subscribe` accepts the event names `CanPet`, `PetStart`, `PetHold`, `PetEnd`, and `ReachAngle`. `CanPet` takes `Action<CanPetEvent>` or the legacy `Func<PetContext, bool>`.
* Unknown commands return null. Malformed known commands throw `ArgumentException` naming the command and the expected schema. Unknown events and too new API versions throw too.
* v1 shapes without the version integer are still accepted for `GetVersion`, `RegisterPettableNpc`, `RegisterPettablePlayer`, `RegisterPettablePlayerRequirement` (alias `RegisterPlayerRequirement`), and `RegisterEvents`.
* Delegate arguments need a compile time reference to the API assembly, because delegate type identity comes from that assembly.

### Clean up on unload

```csharp
public sealed class PettingSystem : ModSystem
{
    public override void Unload()
    {
        // Mod mode: the API mod clears itself, so this is optional there.
        // DLL mode: call all three from your unload path.
        PetEvents.Clear();
        PetRegistry.Clear();
        PetService.Clear();
    }
}
```

## Migration notes from v1

1. The API no longer spawns hearts. `PlayPetHeartSynced` is obsolete and does nothing, and the heart constants and `TrySpawnDefaultHeart` are gone. Subscribe to `OnPetStart`, `OnPetHold`, and `OnPetEnd` and draw your own visuals.
2. `ApplyPetCore` and `HandleSyncedPet` are obsolete. Use `TryApplyPet` and `EndPet`.
3. Event registration uses the `On*` methods with priorities and filters. The `Register*` methods still work as obsolete adapters.
4. A tap opens a session and raises `Start`, then an `End` after `PetHoldTimeoutTicks`. A hold raises `Start` once, `Hold` per refresh, and `End` when released or timed out.
5. Synced replays now raise `Start` on the first application and `Hold` on refreshes, with `Source` set to `Synced`.
6. `UnregisterNpc` is owner scoped and removes every layer the mod registered. `ClearOwner` removes everything a mod owns.
7. `ApiVersion` is 2 and `Call("GetVersion")` returns 2. Old binaries keep working with the surfaces they were compiled against.

## License

Licensed under the Creative Commons Attribution NonCommercial 4.0 International license. See the LICENSE file for the full legal text.

Source repository: https://github.com/POLA-LCS/PetAnyoneAPI
