# Stack Buffs

A Stardew Valley mod that lets food and drink buffs stack instead of replacing each other, and extends the Dwarf King statue with quality-of-life options.

---

## Requirements

- **Stardew Valley** 1.6 or later
- **SMAPI** 4.2.1 or later
- **Generic Mod Config Menu** *(optional)* — for in-game configuration

---

## Installation

1. Install [SMAPI](https://smapi.io/).
2. Download the latest release and unzip it into your `Stardew Valley/Mods` folder.
3. Run the game through SMAPI.

---

## Buff Stacking

By default, eating food or drinking a beverage replaces any buff of the same type already active. Stack Buffs changes this behaviour with three modes:

| Mode | Behaviour |
|---|---|
| **Additive** *(default)* | Eating the same food while a buff is active extends its remaining duration by the new buff's full duration. |
| **Independent** | Each application runs its own timer independently. Eating the same food twice gives two separate countdowns. |
| **Refresh And Stack** | Resets the timer to the new buff's full duration and adds an additional effect layer on top. |

---

## Statue Stacking

The Blessing Statue and Dwarf King statue normally allow one blessing per day across all statues.

Enable **One per statue** to lift that limit — each physical statue on your farm grants its own blessing independently. Place multiple statues to receive multiple blessings per day.

---

## Dwarf King: Extra Ore Nodes

The Dwarf King's *+1 ore from rocks* blessing applies only to standard ore nodes in vanilla. Three toggles extend it to additional node types:

| Option | Default | Description |
|---|---|---|
| Calico Egg Nodes | On | Apply the ore bonus to Calico Egg nodes. |
| Gem Nodes | On | Apply the ore bonus to gem nodes (Diamond, Ruby, Emerald, etc.). |
| Mussel Nodes | On | Apply the ore bonus to mussel nodes. |

---

## Dwarf King: Auto-Pick

When **Auto-Pick Blessing** is enabled, interacting with the Dwarf King statue skips the choice menu and automatically selects the highest-priority blessing from the two offered.

**Randomize Daily Offering** re-rolls the two offered blessings at random on first interaction each day, rather than using the game's default day-seeded pair. The same pair is shown until you pick.

### Blessing Priority

Each blessing has a priority rank from 1 (pick first) to 5 (pick last). When auto-pick is active, the offered blessing with the lower rank is selected. If both offered blessings share the same rank, either may be chosen.

| Blessing | Default Rank |
|---|---|
| +1 Ore from Nodes | 1 |
| Ladders & Shafts | 2 |
| Coal Find Chance | 3 |
| Bomb Immunity | 4 |
| Geode Find Chance | 5 |

---

## Configuration

All options are available in `config.json` or through **Generic Mod Config Menu** in-game.

| Setting | Default | Description |
|---|---|---|
| `Mode` | `Additive` | Buff stacking behaviour (Additive / Independent / RefreshAndStack) |
| `EnableStatueStacking` | `true` | One blessing per statue per day instead of one total |
| `DwarfCalicoEggScaling` | `true` | Ore bonus applies to Calico Egg nodes |
| `DwarfGemNodeScaling` | `true` | Ore bonus applies to gem nodes |
| `DwarfMusselNodeScaling` | `true` | Ore bonus applies to mussel nodes |
| `DwarfAutoPick` | `false` | Automatically select the highest-priority blessing |
| `DwarfRandomizeOffering` | `false` | Randomize the two offered blessings on first interaction |
| `DwarfPriority_*` | 1–5 | Priority rank for each Dwarf King blessing |

---

## Credits

- Built with [SMAPI](https://smapi.io/) by Pathoschild
- Statue patching informed by the [Stardew Valley](https://www.stardewvalley.net/) game code via [StardewXnbHack](https://github.com/Pathoschild/StardewXnbHack)
