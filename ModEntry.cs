using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StackBuffs.Patches;

namespace StackBuffs;

internal sealed class ModEntry : Mod
{
    private ModConfig Config = null!;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        PushConfigToPatches();
        StatuePatch.Monitor = Monitor;
        StatuePatch.Translation = helper.Translation;

        new Harmony(ModManifest.UniqueID).PatchAll();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        try
        {
            BuffManagerPatch.RemoveWholeDayBuffs(Game1.player.buffs);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to remove whole-day buffs at day end: {ex}", LogLevel.Error);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        ConfigMenu.Register(Helper, ModManifest, () => Config, ResetConfig, OnModeChanged);
    }

    private void ResetConfig()
    {
        Config = new ModConfig();
        PushConfigToPatches();
        OnModeChanged();
    }

    /// <summary>The patches read config from static fields, so any new config instance must be pushed to them.</summary>
    private void PushConfigToPatches()
    {
        BuffManagerPatch.Config = Config;
        StatuePatch.Config = Config;
    }

    /// <summary>Fold Independent mode's multi-copy slots into plain buffs when the stacking mode is changed mid-game.</summary>
    private void OnModeChanged()
    {
        try
        {
            if (Context.IsWorldReady)
                BuffManagerPatch.OnModeChanged(Game1.player.buffs);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to update applied buffs after the stacking mode changed: {ex}", LogLevel.Error);
        }
    }
}
