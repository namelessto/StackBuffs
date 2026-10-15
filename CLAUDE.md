# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Shared house conventions for all my mods are imported below. This file only covers what is specific to StackBuffs.

@../CONVENTIONS.md

## Build

```
dotnet build
```

`Pathoschild.Stardew.ModBuildConfig` deploys the DLL + `manifest.json` to the SMAPI `Mods` folder on each build. In-game tests: build `..\StackBuffs.Tests` too (needs the TestRunner mod installed), launch through SMAPI and load the `TEST` save; TestRunner runs the scenarios automatically. Anything the scenarios can't see (the buff bar, real mods) still needs checking in game.

## Architecture

SMAPI mod (manifest UniqueID `NamelessTo.StackBuffs`) that makes buffs stack instead of replacing each other, and optionally lets the Statue of the Dwarf King / Statue of Blessings be used once per statue per day with their buffs stacking. All behavior is Harmony patches (`new Harmony(...).PatchAll()` in `ModEntry`), applied via attributes in `Patches/`.

Config is pushed into the patch classes as static fields (`BuffManagerPatch.Config`, `StatuePatch.Config`). Anything that replaces `Config` must go through `ModEntry.PushConfigToPatches` (as the GMCM reset does via `ResetConfig`), or the patches keep reading the old instance. The GMCM page lives in `ConfigMenu`, which reads config through a getter for the same reason.

### `Patches/BuffManagerPatch.cs`: food/drink stacking

A prefix on `BuffManager.Apply`, switching on `ModConfig.Mode` (`StackingMode`). Debuffs and non-locally-controlled managers (remote farmers in multiplayer) always fall through to vanilla.

- `Additive`, `RefreshAndStack`, `TimerOnly`: merge into the existing buff with the same id; the modes differ in whether effects add and whether timers add or refresh.
- `Independent`: same-id buffs from different sources (vanilla food/drink all use id `food`/`drink`) each keep their own icon and timer. **Every buff stays under its plain id** so vanilla and other mods' lookups (`AppliedBuffs[id]`, `hasBuff`, `Remove(id)`) work unchanged. 1.1.2 used hidden composed keys instead, which crashed Horse Master; don't reintroduce extra keys. When a second source arrives, the slot holds a `StackedBuff` (`Patches/StackedBuff.cs`) with the copies inside:
  - It's only used for 2+ copies. A single copy is always the plain buff object, and `update` puts the last copy back into the slot (safe because `BuffManager.Update` loops over `AppliedBuffIds`, not the dictionary).
  - It ticks each copy's own `update` (mod subclasses like Love of Cooking's `CurryBuff` rely on theirs), and its `effects`/timer are the sum/longest of the copies.
  - Other mods edit the buff they look up (Love of Cooking changes `millisecondsDuration` and effects directly), so `update` passes any outside change on to the newest copy before recalculating.
  - `BuffsDisplayPatch` expands containers in `BuffsDisplay.GetSortedBuffs` so each copy gets its own icon. If it ever fails to apply, the container shows as combined per-effect icons (it has no own icon).
  - `OnModeChanged` folds containers into one plain buff when leaving Independent.
- `statue_of_blessings_*` buffs always stay a single merged buff, whatever the mode.
- **Mods updating their own buff** (`Patches/BuffCaller.cs`, `LetModsManageOwnBuffs`): vanilla `Apply` replaces, and many mods re-apply to update (Horse Master, Tractor Mod, CJB Cheats, Movement Mod, Daleao's Professions). When the same id *and source* is re-applied and the caller (found by walking the stack past StackBuffs, Harmony, `BuffManager` and `Farmer.applyBuff`) is a C# mod rather than the game, it's replaced instead of stacked. **Don't cache the verdict**: the game and a mod can apply the same id with the same (null) source, e.g. `applyBuff("20")`, so only each call's own stack tells them apart (a cache keyed by id/source mixed them up). **Don't exempt `isEating` here**: the flag is on for the whole eating animation, so a mod renewing its buff mid-eating would stack. Game paths (eating, statues, rings, trigger actions/Content Patcher) always stack.
- In-game tests live in a separate test mod, `..\StackBuffs.Tests` (TestRunner scenarios; run `test run` on the `TEST` save, results in `%AppData%\StardewValley\results.json`).
- `DisabledEffects` = per-effect opt-out: those fields use vanilla replacement instead of adding. `WholeDay` / `WholeDayEffects` make buffs `ENDLESS`, but only buffs the game hands out (the same `BuffCaller` check as below; other mods' buffs keep their own timers, whatever `LetModsManageOwnBuffs` is set to). Exception: while `isEating`, a buff whose id isn't active yet also counts as food, since C# food frameworks like BuffFramework apply their buff once mid-animation (they never re-apply an active one); `RemoveWholeDayBuffs` (called from `ModEntry.OnDayEnding`) removes those at day end unless the buff's data says it is endless anyway.
- Since the prefix returns `false`, it must replicate vanilla side effects itself (`actionsOnApply` via `FireActionsOnApply`, `Dirty`, `OnAdded`, `buffsDisplay.updatedIDs`).

### `Patches/StatuePatch.cs`: statue stacking (`EnableStatueStacking`)

- Per-player state lives in `Farmer.modData` with `StackBuffs.`-prefixed keys: which statue was used today (keyed by tile), today's offered Dwarf King pair, and a per-buff stack count reset by day.
- Prefixes replace statue interaction (`SObject.checkForAction` for the Dwarf King, `CheckForActionOnBlessedStatue`) and the Dwarf King choice (`ChooseFromIconsMenu.doIconAction`).
- Vanilla reads these buffs as `hasBuff(id) ? value : default`, so stacking is done with **transpilers** that swap the constant for a call scaled by stack count. `ReplaceFirstBuffValue` finds `ldstr id` + `hasBuff` and replaces the instruction at a fixed offset (+3 for a ternary, +4 for an if-block).
- **These transpilers depend on the exact IL layout.** After a game update, re-check each patched method against the decompiled source (`D:\projects\stardew-mods\Stardew Source\Stardew Code`). Every transpiler logs a warning when its pattern isn't found. The bonus values they scale are named constants at the top of `StatuePatch`, copied from vanilla; re-check those too.
- Prismatic butterfly blessing: `PrismaticButterflyPatch` spawns one butterfly per stack; `ButterflyCapturePatch` makes a catch consume one stack instead of removing the buff.
