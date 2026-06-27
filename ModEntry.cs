using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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

    }
}
