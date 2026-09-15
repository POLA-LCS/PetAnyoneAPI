# PetAnyone

A plain utility library for generic petting of players and NPCs. It is **not** a mod:
it has no `Mod` subclass and no autoloaded content. A consuming mod owns every
tModLoader hook (player loop, packets, effects); this library owns the shared rules and
visuals so they can be reused and unit-reasoned about in isolation.

## Wiring it into a consuming mod

1. Project reference (the built dll lives in the consuming mod's `lib\` folder):

   ```xml
   <Reference Include="PetAnyone">
     <HintPath>lib\PetAnyone.dll</HintPath>
     <Private>False</Private>
   </Reference>
   ```

2. Runtime dependency in the consuming mod's `build.txt`:

   ```
   dllReferences = SpreadsheetSplit, PetAnyone
   ```

3. `PetAnyone.dll` is copied into the consuming mod's `lib\` folder by this project's
   post-build copy target whenever that folder exists. Build this project before building
   the consuming mod.

## API surface

- `PetTargetKind`, `PetTarget` - a lightweight handle for a player or NPC target.
- `PetNpcDefinition` - per-NPC registration flags (world petting, chat button, display name).
- `PetRegistry` - register pettable NPC types/rules, player rules and "held item allows petting" rules.
- `PetEvents` - cancellable/subscription hooks: `CanPet`, `OnPetStart`, `OnPetHold`, `OnPetEnd`, reach angle.
- `PetService` - constants, cursor/range target discovery, `CanPet`, `ApplyPetCore`, `HandleSyncedPet`, heart throttle/visuals, tick helpers.
- `PetEventCallbacks` - bundle for registering several hooks at once.
- `PettingApi` - static entry point plus `Mod.Call` command handling (`GetVersion`, `RegisterPettableNpc`, `RegisterPettablePlayer`, `RegisterEvents`).

## Design notes

- No `Mod`-derived types here, so nothing in this assembly is auto-loaded by tModLoader.
- Per-target cooldown and heart state is kept in static dictionaries keyed by kind+index;
  call `PetService.Clear()` when the consuming mod unloads.
- Cross-mod registrations are pruned lazily: an entry whose owning mod is no longer the
  loaded instance under its name is skipped and removed.
