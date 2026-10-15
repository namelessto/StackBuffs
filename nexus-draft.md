[center][size=5][b]Stack Buffs[/b][/size]
[i]Control how food and drink buffs stack, how long they last, and which effects are affected. Statue stacking and Dwarf King automation included.[/i][/center]

[size=4][b]Overview[/b][/size]

Vanilla Stardew replaces an active buff the moment you eat or drink the same type again. This mod lets you choose how buffs stack instead, or have them last until you sleep. It also makes placing multiple statues worthwhile and automates the Dwarf King's daily choice.

[size=4][b]Features[/b][/size]

[list]
[*]Four stacking modes: Additive, Independent, Refresh And Stack, Timer Only
[*]Buffs Last All Day toggle, which keeps food and drink buffs active until you sleep
[*]Per-effect stacking controls to opt individual effect types (Speed, Luck, Mining, etc.) out of stacking
[*]Per-effect whole-day controls to make individual effect types last all day, independent of the global toggle
[*]Statue stacking, so each Blessing Statue and Dwarf King statue grants its own blessing per day
[*]Dwarf King ore extension, applying the +1 ore bonus to calico egg, gem, and mussel nodes
[*]Dwarf King auto-pick, which selects the highest-priority blessing automatically based on a rank list you set
[/list]

[size=4][b]How to Use[/b][/size]

[list=1]
[*]Install the mod and launch the game through SMAPI.
[*]Open [url=https://www.nexusmods.com/stardewvalley/mods/5098]Generic Mod Config Menu[/url] (or edit [font=Courier New]config.json[/font]) and pick a stacking mode.
[*]Eat and drink normally. Buffs will stack or persist according to your settings.
[/list]

[size=4][b]Configuration[/b][/size]

All options are in [b]config.json[/b] or through [url=https://www.nexusmods.com/stardewvalley/mods/5098]Generic Mod Config Menu[/url] in-game.

[b]Buff Stacking[/b]
[list]
[*][b]Stacking Mode[/b]: Additive stacks effects and adds timers. Independent gives each food or drink its own buff icon and timer, so effects from different sources stack. Eating the same item again refreshes its timer. Refresh And Stack stacks effects and resets the timer. Timer Only extends the timer without stacking effects. (default: Additive)
[*][b]Buffs Last All Day[/b]: food and drink buffs last until you sleep, whatever their normal duration. Works with any stacking mode. (default: off)
[*][b]Mod Buff Compatibility[/b]: when another mod updates a buff it gave you (for example a speed bonus while riding), the new value replaces the old one instead of stacking on top of it. Food, drinks and statue blessings still stack. (default: on)
[*][b]Blessings Per Day[/b]: One per statue gives each placed Dwarf King or Blessing Statue its own blessing per day. One (vanilla) limits you to one per statue type. (default: One per statue)
[/list]

[b]Dwarf King: Extra Ore Nodes[/b]
[list]
[*][b]Calico Egg Nodes[/b]: apply the +1 ore bonus to calico egg nodes. (default: on)
[*][b]Gem Nodes[/b]: apply the +1 ore bonus to gem nodes. (default: on)
[*][b]Mussel Nodes[/b]: apply the +1 ore bonus to mussel nodes. (default: on)
[/list]

[b]Dwarf King: Auto-Pick[/b]
[list]
[*][b]Auto-Pick Blessing[/b]: skip the statue menu and pick automatically. (default: off)
[*][b]Randomize Daily Offering[/b]: re-roll the two offered blessings at random each day instead of seeding them to the day. (default: off)
[*][b]Blessing Priority[/b]: rank each of the five blessings 1 to 5. Auto-pick chooses the lower number when two are offered. (defaults: +1 Ore 1, Ladders 2, Coal 3, Bomb Immunity 4, Geodes 5)
[/list]

[b]Per-Effect: Stacking[/b]
[list]
[*]A checklist of twelve effect types. Uncheck any to make that effect use vanilla replacement instead of stacking, whatever the global stacking mode. Other effects on the same buff keep stacking. (default: all checked)
[/list]

[b]Per-Effect: Whole Day[/b]
[list]
[*]The same twelve effect types. Check any to make buffs that grant it last until you sleep, even when Buffs Last All Day is off. (default: all unchecked)
[/list]

[size=4][b]Requirements[/b][/size]

[list]
[*][url=https://smapi.io/]SMAPI[/url] 4.2.1 or later
[*][url=https://www.nexusmods.com/stardewvalley/mods/5098]Generic Mod Config Menu[/url] [i](optional, for in-game config)[/i]
[/list]

[size=4][b]Installation[/b][/size]

[list=1]
[*]Install [url=https://smapi.io/]SMAPI[/url].
[*]Download and unzip into your [font=Courier New]Stardew Valley/Mods[/font] folder.
[*]Run the game through SMAPI.
[/list]

[size=4][b]Compatibility[/b][/size]

Safe to add to an existing save. It only changes how buffs are applied and how statue cooldowns work. Works with mods that manage their own buffs, such as Horse Master, Tractor Mod and CJB Cheats Menu.

[size=4][b]Credits[/b][/size]

[list]
[*][b]SMAPI[/b] by [url=https://www.nexusmods.com/profile/Pathoschild]Pathoschild[/url]
[*][b]Generic Mod Config Menu[/b] by [url=https://www.nexusmods.com/profile/spacechase0]spacechase0[/url]
[*][b]ConcernedApe[/b] for Stardew Valley
[*]Chinese translation by [url=https://www.nexusmods.com/profile/CNSCZJ/mods]CNSCZJ[/url]
[/list]

[size=4][b]See Also[/b][/size]

[list]
[*][url=https://github.com/namelessto/StackBuffs]Source Code[/url]
[*][url=https://www.nexusmods.com/stardewvalley/mods/28301]Smith Yourself[/url]
[*][url=https://www.nexusmods.com/stardewvalley/mods/21904]Enchanted Galaxy and Infinity Weapons[/url]
[/list]

---

[size=4][b]Changelog[/b][/size]

[b]1.2.0[/b]
[list]
[*]Fixed a crash with Horse Master in Independent mode, and other mods not finding their own buff in that mode
[*]Added [b]Mod Buff Compatibility[/b] (on by default). When another mod updates a buff it gave you, like Horse Master's riding speed or CJB Cheats' move speed, the new value replaces the old one instead of stacking on top of it. Food, drinks and statue blessings still stack
[*]Buffs Last All Day: now only affects buffs from food, drinks and statues, not buffs other mods manage themselves
[*]Lots of under the hood changes that should not affect anything
[/list]

[b]1.1.2[/b]
[list]
[*]Fixed unchecking an effect in Per-Effect: Stacking wiping your other stacked effects. Now only the unchecked effect is replaced
[*]Fixed Timer Only mode ignoring unchecked effects
[*]Independent mode: food and drinks from different sources now each get their own buff icon and timer
[*]Fixed buff checks and buff removal not working in Independent mode
[*]Fixed duplicate buffs after changing the stacking mode mid-day
[*]Fixed the mod changing other players' buffs in multiplayer
[*]Fixed the statue "(×N stacks)" text not updating, and made it translatable
[*]Added warnings in the SMAPI log when a game update breaks a statue stacking patch
[*]Fixed the update key
[*]Lots of under the hood changes that should not affect anything
[/list]

[b]1.1.1[/b]
[list]
[*]Chinese translation by CNSCZJ
[/list]

[b]1.1.0[/b]
[list]
[*]Added [b]Timer Only[/b] stacking mode, which extends the buff timer without stacking effects
[*]Added [b]Buffs Last All Day[/b] toggle. Food and drink buffs last until you sleep; works with any stacking mode
[*]Added [b]Per-Effect: Stacking[/b] to opt individual effect types out of stacking
[*]Added [b]Per-Effect: Whole Day[/b] to make individual effect types last all day independently of the global toggle
[*][b]Breaking:[/b] config format changed. Delete config.json when updating from 1.0.x
[/list]

[b]1.0.1[/b]
[list]
[*]Initial public release
[/list]
