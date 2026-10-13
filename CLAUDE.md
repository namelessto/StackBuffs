# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Shared house conventions for all my mods are imported below. This file only covers what is specific to StackBuffs.

@../CONVENTIONS.md

## Build

```
dotnet build
```

`Pathoschild.Stardew.ModBuildConfig` deploys the DLL + `manifest.json` to the SMAPI `Mods` folder on each build. No test project; verify by launching the game through SMAPI.

## Architecture

SMAPI mod (manifest UniqueID `NamelessTo.StackBuffs`) that makes buffs stack instead of replacing each other, and optionally lets the Statue of the Dwarf King / Statue of Blessings be used once per statue per day with their buffs stacking. All behavior is Harmony patches (`new Harmony(...).PatchAll()` in `ModEntry`), applied via attributes in `Patches/`.

Config is pushed into the patch classes as static fields (`BuffManagerPatch.Config`, `StatuePatch.Config`). Anything that replaces `Config` must go through `ModEntry.PushConfigToPatches` (as the GMCM reset does via `ResetConfig`), or the patches keep reading the old instance. The GMCM page lives in `ConfigMenu`, which reads config through a getter for the same reason.

### `Patches/BuffManagerPatch.cs`: food/drink stacking

A prefix on `BuffManager.Apply`, switching on `ModConfig.Mode` (`StackingMode`). Debuffs and non-locally-controlled managers (remote farmers in multiplayer) always fall through to vanilla.

- `Additive`, `RefreshAndStack`, `TimerOnly`: merge into the existing buff with the same id; the modes differ in whether effects add and whether timers add or refresh.
- `Independent`: stores each buff under a composed key `id + '\u001F' + source` so same-id buffs from different sources get their own icon and timer. Because that breaks id-based lookups, **`IsAppliedPatch` and `RemovePatch` exist to make `hasBuff`, `PLAYER_HAS_BUFF` and `Remove(id)` still match composed keys.** Use `BuffManagerPatch.FindApplied` rather than `AppliedBuffs[id]` anywhere a buff is looked up by plain id. `IsAppliedPatch` runs on every `hasBuff` call, some of them per tick, so keep it allocation-free.
- `statue_of_blessings_*` buffs always stay under their plain id, whatever the mode.
- `NormalizeKeys` re-keys applied buffs when the mode changes mid-game.
- `DisabledEffects` = per-effect opt-out: those fields use vanilla replacement instead of adding. `WholeDay` / `WholeDayEffects` make buffs `ENDLESS`; `RemoveWholeDayBuffs` (called from `ModEntry.OnDayEnding`) removes those at day end unless the buff's data says it is endless anyway.
- Since the prefix returns `false`, it must replicate vanilla side effects itself (`actionsOnApply` via `FireActionsOnApply`, `Dirty`, `OnAdded`, `buffsDisplay.updatedIDs`).

### `Patches/StatuePatch.cs`: statue stacking (`EnableStatueStacking`)

- Per-player state lives in `Farmer.modData` with `StackBuffs.`-prefixed keys: which statue was used today (keyed by tile), today's offered Dwarf King pair, and a per-buff stack count reset by day.
- Prefixes replace statue interaction (`SObject.checkForAction` for the Dwarf King, `CheckForActionOnBlessedStatue`) and the Dwarf King choice (`ChooseFromIconsMenu.doIconAction`).
- Vanilla reads these buffs as `hasBuff(id) ? value : default`, so stacking is done with **transpilers** that swap the constant for a call scaled by stack count. `ReplaceFirstBuffValue` finds `ldstr id` + `hasBuff` and replaces the instruction at a fixed offset (+3 for a ternary, +4 for an if-block).
- **These transpilers depend on the exact IL layout.** After a game update, re-check each patched method against the decompiled source (`D:\projects\stardew-mods\Stardew Source\Stardew Code`). Every transpiler logs a warning when its pattern isn't found. The bonus values they scale are named constants at the top of `StatuePatch`, copied from vanilla; re-check those too.
- Prismatic butterfly blessing: `PrismaticButterflyPatch` spawns one butterfly per stack; `ButterflyCapturePatch` makes a catch consume one stack instead of removing the buff.
