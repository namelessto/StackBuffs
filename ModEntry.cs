using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StackBuffs.Patches;

namespace StackBuffs;

internal sealed class ModEntry : Mod
{
    private ModConfig Config = null!;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        BuffManagerPatch.Config = Config;
        StatuePatch.Config = Config;
        StatuePatch.Monitor = Monitor;

        new Harmony(ModManifest.UniqueID).PatchAll();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        var dataBuffs = DataLoader.Buffs(Game1.content);
        var toRemove = Game1.player.buffs.AppliedBuffs
            .Where(kv => kv.Value.millisecondsDuration == Buff.ENDLESS
                      && (!dataBuffs.TryGetValue(kv.Key, out var d) || d.Duration != Buff.ENDLESS))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var id in toRemove)
            Game1.player.buffs.Remove(id);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = Helper.ModRegistry.GetApi<IGMCMApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null) return;
        var T = Helper.Translation;

        gmcm.Register(
            ModManifest,
            reset: () => { Config = new ModConfig(); BuffManagerPatch.Config = Config; StatuePatch.Config = Config; },
            save: () => Helper.WriteConfig(Config)
        );

        // ── Buff Stacking ────────────────────────────────────────────────────
        gmcm.AddSectionTitle(ModManifest, () => T.Get("config.section.buff-stacking"));

        gmcm.AddTextOption(
            ModManifest,
            getValue: () => Config.Mode.ToString(),
            setValue: v => Config.Mode = Enum.Parse<StackingMode>(v),
            name: () => T.Get("config.stacking-mode.name"),
            tooltip: () => T.Get("config.stacking-mode.tooltip"),
            allowedValues: Enum.GetNames<StackingMode>(),
            formatAllowedValue: v => T.Get($"config.mode.{v}")
        );

        gmcm.AddBoolOption(
            ModManifest,
            getValue: () => Config.WholeDay,
            setValue: v => Config.WholeDay = v,
            name: () => T.Get("config.whole-day.name"),
            tooltip: () => T.Get("config.whole-day.tooltip")
        );

        gmcm.AddTextOption(
            ModManifest,
            getValue: () => Config.EnableStatueStacking ? "PerStatue" : "Vanilla",
            setValue: v => { Config.EnableStatueStacking = v == "PerStatue"; StatuePatch.Config = Config; },
            name: () => T.Get("config.statue-stacking.name"),
            tooltip: () => T.Get("config.statue-stacking.tooltip"),
            allowedValues: new[] { "PerStatue", "Vanilla" },
            formatAllowedValue: v => T.Get($"config.statue-stacking.{v.ToLower()}")
        );

        // ── Dwarf King: Extra Ore Nodes ──────────────────────────────────────
        gmcm.AddSectionTitle(ModManifest,
            () => T.Get("config.section.ore-nodes"),
            () => T.Get("config.section.ore-nodes.tooltip"));

        gmcm.AddBoolOption(
            ModManifest,
            getValue: () => Config.DwarfCalicoEggScaling,
            setValue: v => Config.DwarfCalicoEggScaling = v,
            name: () => T.Get("config.calico-egg-nodes.name"),
            tooltip: () => T.Get("config.calico-egg-nodes.tooltip")
        );

        gmcm.AddBoolOption(
            ModManifest,
            getValue: () => Config.DwarfGemNodeScaling,
            setValue: v => Config.DwarfGemNodeScaling = v,
            name: () => T.Get("config.gem-nodes.name"),
            tooltip: () => T.Get("config.gem-nodes.tooltip")
        );

        gmcm.AddBoolOption(
            ModManifest,
            getValue: () => Config.DwarfMusselNodeScaling,
            setValue: v => Config.DwarfMusselNodeScaling = v,
            name: () => T.Get("config.mussel-nodes.name"),
            tooltip: () => T.Get("config.mussel-nodes.tooltip")
        );

        // ── Dwarf King: Auto-Pick ────────────────────────────────────────────
        gmcm.AddSectionTitle(ModManifest,
            () => T.Get("config.section.auto-pick"),
            () => T.Get("config.section.auto-pick.tooltip"));

        gmcm.AddBoolOption(
            ModManifest,
            getValue: () => Config.DwarfAutoPick,
            setValue: v => Config.DwarfAutoPick = v,
            name: () => T.Get("config.auto-pick.name"),
            tooltip: () => T.Get("config.auto-pick.tooltip")
        );

        gmcm.AddBoolOption(
            ModManifest,
            getValue: () => Config.DwarfRandomizeOffering,
            setValue: v => Config.DwarfRandomizeOffering = v,
            name: () => T.Get("config.randomize-offering.name"),
            tooltip: () => T.Get("config.randomize-offering.tooltip")
        );

        gmcm.AddSectionTitle(ModManifest,
            () => T.Get("config.section.blessing-priority"),
            () => T.Get("config.section.blessing-priority.tooltip"));

        string RankTooltip(string blessingKey) =>
            T.Get("config.rank-tooltip", new { blessing = T.Get(blessingKey) });

        gmcm.AddNumberOption(ModManifest,
            getValue: () => Config.DwarfPriority_ExtraOre,
            setValue: v => Config.DwarfPriority_ExtraOre = v,
            name: () => T.Get("config.priority.ore.name"),
            tooltip: () => RankTooltip("config.priority.ore.blessing"),
            min: 1, max: 5, interval: 1);

        gmcm.AddNumberOption(ModManifest,
            getValue: () => Config.DwarfPriority_Ladders,
            setValue: v => Config.DwarfPriority_Ladders = v,
            name: () => T.Get("config.priority.ladders.name"),
            tooltip: () => RankTooltip("config.priority.ladders.blessing"),
            min: 1, max: 5, interval: 1);

        gmcm.AddNumberOption(ModManifest,
            getValue: () => Config.DwarfPriority_Coal,
            setValue: v => Config.DwarfPriority_Coal = v,
            name: () => T.Get("config.priority.coal.name"),
            tooltip: () => RankTooltip("config.priority.coal.blessing"),
            min: 1, max: 5, interval: 1);

        gmcm.AddNumberOption(ModManifest,
            getValue: () => Config.DwarfPriority_BombImmunity,
            setValue: v => Config.DwarfPriority_BombImmunity = v,
            name: () => T.Get("config.priority.bomb-immunity.name"),
            tooltip: () => RankTooltip("config.priority.bomb-immunity.blessing"),
            min: 1, max: 5, interval: 1);

        gmcm.AddNumberOption(ModManifest,
            getValue: () => Config.DwarfPriority_Geodes,
            setValue: v => Config.DwarfPriority_Geodes = v,
            name: () => T.Get("config.priority.geodes.name"),
            tooltip: () => RankTooltip("config.priority.geodes.blessing"),
            min: 1, max: 5, interval: 1);

        // ── Per-Effect Stacking ──────────────────────────────────────────────
        gmcm.AddSectionTitle(ModManifest,
            () => T.Get("config.section.effect-stacking"),
            () => T.Get("config.section.effect-stacking.tooltip"));

        foreach (var (effectKey, i18nKey) in EffectList)
        {
            string key = effectKey;
            gmcm.AddBoolOption(ModManifest,
                getValue: () => !Config.DisabledEffects.Contains(key),
                setValue: v => { if (v) Config.DisabledEffects.Remove(key); else Config.DisabledEffects.Add(key); },
                name: () => T.Get(i18nKey));
        }

        // ── Per-Effect Whole Day ─────────────────────────────────────────────
        gmcm.AddSectionTitle(ModManifest,
            () => T.Get("config.section.effect-wholeday"),
            () => T.Get("config.section.effect-wholeday.tooltip"));

        foreach (var (effectKey, i18nKey) in EffectList)
        {
            string key = effectKey;
            gmcm.AddBoolOption(ModManifest,
                getValue: () => Config.WholeDayEffects.Contains(key),
                setValue: v => { if (v) Config.WholeDayEffects.Add(key); else Config.WholeDayEffects.Remove(key); },
                name: () => T.Get(i18nKey));
        }

    }

    private static readonly (string effectKey, string i18nKey)[] EffectList =
    {
        ("FarmingLevel",   "config.effect.farming"),
        ("FishingLevel",   "config.effect.fishing"),
        ("MiningLevel",    "config.effect.mining"),
        ("LuckLevel",      "config.effect.luck"),
        ("ForagingLevel",  "config.effect.foraging"),
        ("MaxStamina",     "config.effect.stamina"),
        ("MagneticRadius", "config.effect.magnetism"),
        ("Speed",          "config.effect.speed"),
        ("Defense",        "config.effect.defense"),
        ("Attack",         "config.effect.attack"),
        ("CombatLevel",    "config.effect.combat"),
        ("Immunity",       "config.effect.immunity"),
    };
}
