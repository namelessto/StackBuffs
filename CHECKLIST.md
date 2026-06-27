# StackBuffs — Manual Test Checklist

## Setup
- [ ] `dotnet build` succeeds with 0 errors, 0 warnings
- [ ] SMAPI console shows `StackBuffs` loaded with no errors on game launch
- [ ] SMAPI console shows **no** `[StackBuffs] ButterflyCapturePatch: unexpected IL layout` warning

---

## GMCM Config
- [ ] Generic Mod Config Menu opens; "StackBuffs" section appears
- [ ] "Stacking Mode" dropdown shows Independent / Additive / Refresh And Stack
- [ ] "Per-Statue Daily Limit" toggle is present
- [ ] Changing mode in-menu and saving persists across game restart

---

## Food/Drink Buff Stacking

**Independent mode** (same source = refresh, different source = stack)
- [ ] Eat the same food twice → timer refreshes, single icon, no effect doubling
- [ ] Eat two different foods with the same buff type → icon remains single, effects accumulate (e.g. Speed shows combined bonus)

**Additive mode**
- [ ] Eat the same food twice → timer extends, effects accumulate

**RefreshAndStack mode**
- [ ] Eat the same food twice → timer resets to full duration, effects accumulate

---

## ENDLESS Duration Fixes
*(Requires a buff source with ENDLESS duration — e.g. a statue blessing, which has Duration: -2)*

- [ ] **Additive mode**: Apply a finite food buff → apply an ENDLESS buff of the same ID → existing buff becomes ENDLESS (not reduced by 2 ms)
- [ ] **RefreshAndStack mode**: Same scenario → existing finite buff is promoted to ENDLESS

---

## Dwarf King Statue
*(Use `stackbuffs_maxskills` in SMAPI console first — requires Mining Mastery)*

- [ ] Interacting opens the icon-choice menu with 2 **distinct** options (never identical)
- [ ] Picking an option applies the buff and menu closes with sound + particle effect
- [ ] Interacting with a **different** placed statue on the same day applies the buff again; "(×2 stacks)" appears in buff description
- [ ] Interacting with an **already-used** statue (same player, same day) → cancel sound plays + statue shakes, no new buff
- [ ] `dwarfStatue_3` (bomb immunity) is not offered if the player already holds it

**Effect scaling with 2+ stacks:**
- [ ] `dwarfStatue_0` — extra ore per stone: 2 stacks → 2× extra ore
- [ ] `dwarfStatue_1` — monster loot bonus: 2 stacks → 2× drop rate bonus
- [ ] `dwarfStatue_2` — gem find chance: 2 stacks → 2× chance
- [ ] `dwarfStatue_4` — stone quantity multiplier: 2 stacks → 1.25² = ~1.5625×

---

## Statue of Blessings
*(Requires Farming Mastery)*

- [ ] First interaction each day applies a random blessing with sound + decorative butterflies + particles
- [ ] Second interaction with the **same** statue same day → cancel sound + statue shakes, no buff
- [ ] Second interaction with a **different** statue same day → applies a second blessing; "(×2 stacks)" in description
- [ ] `_2` (unlimited stamina) is re-rolled if already held; never offered twice

**Effect scaling with 2+ stacks:**
- [ ] `_0` (Speed +0.5): 2 stacks → +1.0 speed total
- [ ] `_1` (Luck +1): 2 stacks → +2 luck total
- [ ] `_3` (Fishing charges): 2 stacks → 6 charges; all 6 consumed before buff is removed
- [ ] `_4` (Friendship): 2 stacks → NPC interaction grants 120 friendship instead of 60
- [ ] `_5` (Crit chance): 2 stacks → +0.2f crit chance instead of +0.1f

---

## Prismatic Butterfly (`_6`)
- [ ] 1 stack → exactly 1 prismatic butterfly appears in the target location
- [ ] 2 stacks → exactly 2 prismatic butterflies appear in the target location
- [ ] Catching 1 butterfly with 2 stacks → count drops to 1, description updates
- [ ] Catching the last butterfly → buff is fully removed

---

## Desert Festival Cooking Buff
*(Requires Calico Desert Festival access)*

- [ ] Completing the cooking questionnaire applies a buff with the chosen stat effects
- [ ] Buff appears in HUD with correct icon and duration
- [ ] In Additive mode, a second "DesertFestival" buff of the same ID stacks correctly

---

## Multiplayer
*(Requires co-op session)*

- [ ] Player A uses Statue #1 → Player B can also use the same Statue #1 the same day
- [ ] Player A uses Statue #1 → Player A cannot use Statue #1 again the same day
- [ ] Two players using two different statues both receive their buffs independently

---

## Debug Command
- [ ] `stackbuffs_maxskills` → all skills set to 10, all masteries set to 5 (confirmed in SMAPI console)
- [ ] Running without a save loaded → logs "Load a save first." at Warn level
