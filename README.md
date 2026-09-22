# PetAnyone

A shared petting interaction library for Terraria mods built on tModLoader. It is not a mod itself: it has no `Mod` subclass, nothing is auto loaded, and no content is registered just by referencing it. A consuming mod owns every tModLoader hook; this library owns the shared rules, registrations, and visuals.

## Description / Usage Context

PetAnyone lets a player pet another player or a registered NPC. The player doing the petting is the patter. Targets are pettable players and registered NPCs, reached with a cursor, a chat button, or whatever affordance your mod builds on top.

The library is Terraria oriented on purpose. It is built against the Terraria and tModLoader types, including `Player`, `NPC`, `Item`, `Main`, and `Mod`, and it targets `net8.0`, which is the runtime tModLoader mods use. The deliverable is a plain class library, `PetAnyone.dll`, that a mod references from its `lib` folder.

What the library owns: registration, rules, and shared visuals.

* `PetRegistry` is the registration table for pettable NPC types and predicate rules, player veto rules, player allow list requirements, and held item rules that define what counts as a petting hand.
* `PetEvents` holds the subscription hooks: `CanPet`, `OnPetStart`, `OnPetHold`, `OnPetEnd`, and an optional reach angle provider.
* `PetService` holds the shared rules and visuals: range, cooldown, refresh, reach, and heart constants, cursor target discovery, the `CanPet` gate, cooldown bookkeeping, `ApplyPetCore`, `HandleSyncedPet`, the reach angle, and the per target heart throttle.
* `PettingApi` is the static entry point for consumer mods and the `Mod.Call` dispatcher for cross mod registration.

What the consuming mod owns: everything that needs a hook.

* Input, deciding when a tap or a hold applies, and all networking around every applied pet.
* Sound, extra particles, and the reach arm animation.
* Chat button and other UI affordances, using the flags in `PetNpcDefinition`.
* Unload cleanup: call `PetEvents.Clear()`, `PetRegistry.Clear()`, and `PetService.Clear()`.

Public API at a glance:

* `PetTargetKind` and `PetTarget`: which kind of entity a target points at, and a copyable handle for a player or an NPC by index. Members: `None`, `Kind`, `Index`, `IsPlayer`, `IsNpc`, `IsValid`, `IsActive`, `IsAlive`, `AsEntity`, `Center`, `Hitbox`, `DisplayName`, `TryGetPlayer`, `TryGetNpc`, `TryGetEntity`, `FromPlayer`, `FromNpc`.
* `PetContext`: the payload for every hook. Members: `Patter`, `Target`, `IsValid`.
* `PetNpcDefinition`: per NPC flags for consumers. Members: `Default`, `DisplayName`, `AllowWorldPet`, `ShowChatButton`, `ButtonText`.
* `PetEventCallbacks`: bundle for registering several hooks at once. Members: `CanPet`, `OnPetStart`, `OnPetHold`, `OnPetEnd`.
* `PetRegistry`: registration and lookup. Members: `RegisterNpc`, `UnregisterNpc`, `RegisterNpcRule`, `UnregisterNpcRule`, `TryGetNpcDefinition`, `IsNpcPettable`, `RegisterPlayerRule`, `UnregisterPlayerRule`, `RegisterPlayerRequirement`, `UnregisterPlayerRequirement`, `IsPlayerPettable`, `RegisterPetHandItem`, `UnregisterPetHandItem`, `AllowsPettingItem`, `IsOwnerLoaded`, `Clear`.
* `PetEvents`: hooks. Members: `RegisterCanPet`, `RegisterOnPetStart`, `RegisterOnPetHold`, `RegisterOnPetEnd`, `RegisterReachAngle`, `RegisterCallbacks`, `CanPet`, `RaisePetStart`, `RaisePetHold`, `RaisePetEnd`, `TryGetReachAngle`, `Clear`.
* `PetService`: shared rules and visuals. Members: `PetRangeTiles`, `PetCooldownTicks`, `PetReachDurationTicks`, `PetHeartIntervalTicks`, `PetRefreshTicks`, `PetAngleMorphed`, `PetAngleVanilla`, `IsPetHand`, `FindTargetUnderCursor`, `TryFindTargetUnderCursor`, `CanPet`, `ApplyPetCore`, `HandleSyncedPet`, `GetReachAngle`, `GetLastPetTick`, `SetLastPetTick`, `GetTargetLastPetTick`, `SetTargetLastPetTick`, `PlayPetHeartSynced`, `Clear`.
* `PettingApi`: consumer entry point. Members: `ApiVersion`, `RegisterPettableNpc` (type and predicate overloads), `RegisterPettablePlayer`, `RegisterPettablePlayerRequirement`, `RegisterEvents`, `Call`.

### Requirements

* tModLoader installed locally. The project imports the local tModLoader targets, and it is built with the .NET 8 SDK and C# 12.
* `BuildMod` is false, so the build produces a library only, with no `.tmod` and no auto loaded content. Build with `dotnet build /p:Configuration=Release`, output at `bin\Release\net8.0\PetAnyone.dll`.
* No NuGet packages.
* Reference it from the consuming mod:

```xml
<Reference Include="PetAnyone">
  <HintPath>lib\PetAnyone.dll</HintPath>
  <Private>False</Private>
</Reference>
```

* Declare the runtime dependency in the consuming mod's `build.txt`:

```
dllReferences = PetAnyone
```

* Place `PetAnyone.dll` in the consuming mod's `lib` folder before building that mod. This project copies the built DLL into a sibling mod's `lib` folder when that folder already exists, and other consumers copy the DLL themselves.

### Behavior and unload safety

* Players are pettable by default when active and alive. NPCs are pettable only after registration, either per type or through a predicate rule. The most recently registered matching rule wins.
* `RegisterPlayerRule` can veto petting a player. `RegisterPlayerRequirement` is an allow list, and every requirement must pass.
* The empty hand always allows petting. A held item only does when a `RegisterPetHandItem` rule allows it.
* `CanPet` runs subscribers in registration order, and any handler returning false cancels the pet.
* Reach is 3 tiles (48 pixels) from center to center. A tap pet has a 20 tick cooldown per target, a hold refreshes every 10 ticks, the reach arm lasts 30 ticks, and love hearts are throttled to one per target every 30 ticks.
* Registrations and handlers are tagged with the owning `Mod` and pruned lazily, so an unloaded mod cannot leave dangling handlers behind.
* Call `PetEvents.Clear()`, `PetRegistry.Clear()`, and `PetService.Clear()` from the consuming mod's unload path.

## Why Would You Use It

* You get the whole petting interaction without writing the registration table, the range and cooldown rules, the heart visuals, or the synced replay yourself.
* Other mods can register their own pettable NPCs with their own labels through the same registry, including at runtime through `Mod.Call`, so the interaction grows without coupling mods together.
* Registrations are tied to the owning mod and pruned automatically, so unloading is safe.
* The library takes no hooks. Your mod decides when a tap or a hold applies and owns all input, networking, sound, and animation.
* Shared constants keep reach, timing, and cooldown behavior consistent across every mod that uses it.
* It is Terraria oriented by design and built against the same runtime as tModLoader mods, so it drops into a mod project without adapters.

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
        PetRegistry.RegisterNpcRule(this, npc => npc.type == NPCID.Bunny, new PetNpcDefinition(buttonText: "Boop"));

        // Players are pettable by default, so a requirement narrows it down.
        PetRegistry.RegisterPlayerRequirement(this, player => player.GetModPlayer<MyPlayer>().IsPettable);

        // The empty hand always works. Other items need a rule.
        PetRegistry.RegisterPetHandItem(this, item => item.type == ModContent.ItemType<Clicker>());

        // Hooks, including a cancel check and an angle override.
        PetEvents.RegisterCanPet(this, context => context.Target.IsAlive);
        PetEvents.RegisterOnPetStart(this, context => Main.NewText($"You pet {context.Target.DisplayName}."));
        PetEvents.RegisterReachAngle(this, target => target.IsNpc ? PetService.PetAngleMorphed : (float?)null);
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

    if (!PetService.CanPet(patter, target))
        return;

    PetService.ApplyPetCore(patter, target, bypassCooldown: false);

    // The consuming mod owns the packet and the sound here.
}
```

### Replay a synced pet

```csharp
// On each client, when the pet arrives over the network:
PetService.HandleSyncedPet(patter, target);

// The shared heart is spawned by the library and throttled per target.
// The reach arm and sound stay in your mod:
int armTicks = PetService.PetReachDurationTicks;
float armAngle = PetService.GetReachAngle(target);
```

### Register from another mod with Mod.Call

Host mod:

```csharp
public override object Call(params object[] args) => PettingApi.Call(args);
```

Caller:

```csharp
// The library has no Mod subclass, so ask the host mod that forwards
// PettingApi.Call for its own name.
Mod host = ModLoader.GetMod("PettingHost");

if (host is not null)
{
    Func<Player, bool> isPettable = player => player.GetModPlayer<MyPlayer>().IsPettable;

    host.Call("RegisterPettableNpc", this, ModContent.NPCType<MyPet>(), new PetNpcDefinition(buttonText: "Pet <3"));
    host.Call("RegisterPettablePlayer", this, isPettable);

    int version = (int)host.Call("GetVersion")!;
}
```

The host mod must be the mod that overrides `Call` and forwards to `PettingApi.Call`, as shown in the host snippet above. Replace `PettingHost` with that mod's internal name.

Supported commands: `GetVersion`, `RegisterPettableNpc`, `RegisterPettablePlayer`, `RegisterPettablePlayerRequirement` (also accepted as `RegisterPlayerRequirement`), and `RegisterEvents`.

### Clean up on unload

```csharp
public sealed class PettingSystem : ModSystem
{
    public override void Unload()
    {
        PetEvents.Clear();
        PetRegistry.Clear();
        PetService.Clear();
    }
}
```
