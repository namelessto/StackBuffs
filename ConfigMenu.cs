using StardewModdingAPI;

namespace StackBuffs;

/// <summary>Registers the Generic Mod Config Menu page. GMCM is optional; nothing here runs without it.</summary>
internal static class ConfigMenu
{
    /// <param name="helper">The mod's helper, for the GMCM API, translations and writing the config.</param>
    /// <param name="manifest">The mod's manifest.</param>
    /// <param name="getConfig">Returns the current config. A getter because GMCM's reset replaces the instance.</param>
    /// <param name="resetConfig">Replaces the config with defaults and pushes it to the patches.</param>
    /// <param name="onModeChanged">Called after the stacking mode changes.</param>
    public static void Register(IModHelper helper, IManifest manifest, Func<ModConfig> getConfig, Action resetConfig, Action onModeChanged)
    {
        var gmcm = helper.ModRegistry.GetApi<IGMCMApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null) return;
        var T = helper.Translation;

        gmcm.Register(
            manifest,
            reset: resetConfig,
            save: () => helper.WriteConfig(getConfig())
        );

        // ── Buff Stacking ────────────────────────────────────────────────────
        gmcm.AddSectionTitle(manifest, () => T.Get("config.section.buff-stacking"));

        gmcm.AddTextOption(
            manifest,
            getValue: () => getConfig().Mode.ToString(),
            setValue: v => { getConfig().Mode = Enum.Parse<StackingMode>(v); onModeChanged(); },
            name: () => T.Get("config.stacking-mode.name"),
            tooltip: () => T.Get("config.stacking-mode.tooltip"),
            allowedValues: Enum.GetNames<StackingMode>(),
            formatAllowedValue: v => T.Get($"config.mode.{v}")
        );

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().WholeDay,
            setValue: v => getConfig().WholeDay = v,
            name: () => T.Get("config.whole-day.name"),
            tooltip: () => T.Get("config.whole-day.tooltip")
        );

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().LetModsManageOwnBuffs,
            setValue: v => getConfig().LetModsManageOwnBuffs = v,
            name: () => T.Get("config.mod-buffs.name"),
            tooltip: () => T.Get("config.mod-buffs.tooltip")
        );

        gmcm.AddTextOption(
            manifest,
            getValue: () => getConfig().EnableStatueStacking ? "PerStatue" : "Vanilla",
            setValue: v => getConfig().EnableStatueStacking = v == "PerStatue",
            name: () => T.Get("config.statue-stacking.name"),
            tooltip: () => T.Get("config.statue-stacking.tooltip"),
            allowedValues: new[] { "PerStatue", "Vanilla" },
            formatAllowedValue: v => T.Get($"config.statue-stacking.{v.ToLower()}")
        );

        // ── Dwarf King: Extra Ore Nodes ──────────────────────────────────────
        gmcm.AddSectionTitle(manifest,
            () => T.Get("config.section.ore-nodes"),
            () => T.Get("config.section.ore-nodes.tooltip"));

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().DwarfCalicoEggScaling,
            setValue: v => getConfig().DwarfCalicoEggScaling = v,
            name: () => T.Get("config.calico-egg-nodes.name"),
            tooltip: () => T.Get("config.calico-egg-nodes.tooltip")
        );

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().DwarfGemNodeScaling,
            setValue: v => getConfig().DwarfGemNodeScaling = v,
            name: () => T.Get("config.gem-nodes.name"),
            tooltip: () => T.Get("config.gem-nodes.tooltip")
        );

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().DwarfMusselNodeScaling,
            setValue: v => getConfig().DwarfMusselNodeScaling = v,
            name: () => T.Get("config.mussel-nodes.name"),
            tooltip: () => T.Get("config.mussel-nodes.tooltip")
        );

        // ── Dwarf King: Auto-Pick ────────────────────────────────────────────
        gmcm.AddSectionTitle(manifest,
            () => T.Get("config.section.auto-pick"),
            () => T.Get("config.section.auto-pick.tooltip"));

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().DwarfAutoPick,
            setValue: v => getConfig().DwarfAutoPick = v,
            name: () => T.Get("config.auto-pick.name"),
            tooltip: () => T.Get("config.auto-pick.tooltip")
        );

        gmcm.AddBoolOption(
            manifest,
            getValue: () => getConfig().DwarfRandomizeOffering,
            setValue: v => getConfig().DwarfRandomizeOffering = v,
            name: () => T.Get("config.randomize-offering.name"),
            tooltip: () => T.Get("config.randomize-offering.tooltip")
        );

        gmcm.AddSectionTitle(manifest,
            () => T.Get("config.section.blessing-priority"),
            () => T.Get("config.section.blessing-priority.tooltip"));

        string RankTooltip(string blessingKey) =>
            T.Get("config.rank-tooltip", new { blessing = T.Get(blessingKey) });

        gmcm.AddNumberOption(manifest,
            getValue: () => getConfig().DwarfPriority_ExtraOre,
            setValue: v => getConfig().DwarfPriority_ExtraOre = v,
            name: () => T.Get("config.priority.ore.name"),
            tooltip: () => RankTooltip("config.priority.ore.blessing"),
            min: ModConfig.MinPriorityRank, max: ModConfig.MaxPriorityRank, interval: 1);

        gmcm.AddNumberOption(manifest,
            getValue: () => getConfig().DwarfPriority_Ladders,
            setValue: v => getConfig().DwarfPriority_Ladders = v,
            name: () => T.Get("config.priority.ladders.name"),
            tooltip: () => RankTooltip("config.priority.ladders.blessing"),
            min: ModConfig.MinPriorityRank, max: ModConfig.MaxPriorityRank, interval: 1);

        gmcm.AddNumberOption(manifest,
            getValue: () => getConfig().DwarfPriority_Coal,
            setValue: v => getConfig().DwarfPriority_Coal = v,
            name: () => T.Get("config.priority.coal.name"),
            tooltip: () => RankTooltip("config.priority.coal.blessing"),
            min: ModConfig.MinPriorityRank, max: ModConfig.MaxPriorityRank, interval: 1);

        gmcm.AddNumberOption(manifest,
            getValue: () => getConfig().DwarfPriority_BombImmunity,
            setValue: v => getConfig().DwarfPriority_BombImmunity = v,
            name: () => T.Get("config.priority.bomb-immunity.name"),
            tooltip: () => RankTooltip("config.priority.bomb-immunity.blessing"),
            min: ModConfig.MinPriorityRank, max: ModConfig.MaxPriorityRank, interval: 1);

        gmcm.AddNumberOption(manifest,
            getValue: () => getConfig().DwarfPriority_Geodes,
            setValue: v => getConfig().DwarfPriority_Geodes = v,
            name: () => T.Get("config.priority.geodes.name"),
            tooltip: () => RankTooltip("config.priority.geodes.blessing"),
            min: ModConfig.MinPriorityRank, max: ModConfig.MaxPriorityRank, interval: 1);

        // ── Per-Effect Stacking ──────────────────────────────────────────────
        gmcm.AddSectionTitle(manifest,
            () => T.Get("config.section.effect-stacking"),
            () => T.Get("config.section.effect-stacking.tooltip"));

        foreach (var (effectKey, i18nKey) in EffectList)
        {
            string key = effectKey;
            gmcm.AddBoolOption(manifest,
                getValue: () => !getConfig().DisabledEffects.Contains(key),
                setValue: v => { if (v) getConfig().DisabledEffects.Remove(key); else getConfig().DisabledEffects.Add(key); },
                name: () => T.Get(i18nKey));
        }

        // ── Per-Effect Whole Day ─────────────────────────────────────────────
        gmcm.AddSectionTitle(manifest,
            () => T.Get("config.section.effect-wholeday"),
            () => T.Get("config.section.effect-wholeday.tooltip"));

        foreach (var (effectKey, i18nKey) in EffectList)
        {
            string key = effectKey;
            gmcm.AddBoolOption(manifest,
                getValue: () => getConfig().WholeDayEffects.Contains(key),
                setValue: v => { if (v) getConfig().WholeDayEffects.Add(key); else getConfig().WholeDayEffects.Remove(key); },
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
