using HarmonyLib;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Triggers;

namespace StackBuffs.Patches;

[HarmonyPatch(typeof(BuffManager), nameof(BuffManager.Apply))]
internal static class BuffManagerPatch
{
    internal static ModConfig Config = null!;

    [HarmonyPrefix]
    static bool Prefix(BuffManager __instance, Buff buff)
    {
        if (buff is null) return true;
        if (DataLoader.Buffs(Game1.content).TryGetValue(buff.id, out var buffData) && buffData.IsDebuff)
            return true;
        bool isWholeDay = Config?.WholeDay == true
                       || (Config?.WholeDayEffects is { Count: > 0 } wde && HasMatchingEffect(buff, wde));
        if (isWholeDay && buff.millisecondsDuration != Buff.ENDLESS)
        {
            buff.millisecondsDuration = Buff.ENDLESS;
            buff.totalMillisecondsDuration = Buff.ENDLESS;
        }

        if (Config?.DisabledEffects is { Count: > 0 } de && HasMatchingEffect(buff, de))
            return true;

        switch (Config?.Mode ?? StackingMode.Independent)
        {
            case StackingMode.Independent:
                if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingInd))
                {
                    bool isBlessingBuff = buff.id.StartsWith("statue_of_blessings_");
                    if (isBlessingBuff || buff.source != existingInd.source)
                    {
                        existingInd.effects.Add(buff.effects);
                        if (existingInd.millisecondsDuration != Buff.ENDLESS &&
                            buff.millisecondsDuration != Buff.ENDLESS &&
                            buff.millisecondsDuration > existingInd.millisecondsDuration)
                        {
                            existingInd.millisecondsDuration = buff.millisecondsDuration;
                            existingInd.totalMillisecondsDuration = buff.millisecondsDuration;
                        }
                    }
                    else
                    {
                        if (existingInd.millisecondsDuration != Buff.ENDLESS)
                        {
                            existingInd.millisecondsDuration = buff.millisecondsDuration;
                            existingInd.totalMillisecondsDuration = buff.millisecondsDuration;
                        }
                    }
                    FireActionsOnApply(buff);
                    __instance.Dirty = true;
                    return false;
                }
                return true;

            case StackingMode.Additive:
                if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingAdd))
                {
                    existingAdd.effects.Add(buff.effects);
                    if (existingAdd.millisecondsDuration != Buff.ENDLESS)
                    {
                        if (buff.millisecondsDuration == Buff.ENDLESS)
                        {
                            existingAdd.millisecondsDuration = Buff.ENDLESS;
                            existingAdd.totalMillisecondsDuration = Buff.ENDLESS;
                        }
                        else
                        {
                            long combined = (long)existingAdd.millisecondsDuration + buff.millisecondsDuration;
                            existingAdd.millisecondsDuration = (int)Math.Min(combined, int.MaxValue - 1);
                            existingAdd.totalMillisecondsDuration = existingAdd.millisecondsDuration;
                        }
                    }
                    FireActionsOnApply(buff);
                    __instance.Dirty = true;
                    return false;
                }
                return true;

            case StackingMode.RefreshAndStack:
                if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingRAS))
                {
                    existingRAS.effects.Add(buff.effects);
                    if (buff.millisecondsDuration == Buff.ENDLESS)
                    {
                        existingRAS.millisecondsDuration = Buff.ENDLESS;
                        existingRAS.totalMillisecondsDuration = Buff.ENDLESS;
                    }
                    else
                    {
                        existingRAS.millisecondsDuration = buff.millisecondsDuration;
                        existingRAS.totalMillisecondsDuration = buff.millisecondsDuration;
                    }
                    FireActionsOnApply(buff);
                    __instance.Dirty = true;
                    return false;
                }
                return true;

            case StackingMode.TimerOnly:
                if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingTO))
                {
                    if (existingTO.millisecondsDuration != Buff.ENDLESS)
                    {
                        if (buff.millisecondsDuration == Buff.ENDLESS)
                        {
                            existingTO.millisecondsDuration = Buff.ENDLESS;
                            existingTO.totalMillisecondsDuration = Buff.ENDLESS;
                        }
                        else
                        {
                            long combined = (long)existingTO.millisecondsDuration + buff.millisecondsDuration;
                            existingTO.millisecondsDuration = (int)Math.Min(combined, int.MaxValue - 1);
                            existingTO.totalMillisecondsDuration = existingTO.millisecondsDuration;
                        }
                    }
                    FireActionsOnApply(buff);
                    __instance.Dirty = true;
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    private static bool HasMatchingEffect(Buff buff, HashSet<string> effectNames)
    {
        if (buff.effects is not { } e) return false;
        return (effectNames.Contains("FarmingLevel")   && e.FarmingLevel.Value   != 0)
            || (effectNames.Contains("FishingLevel")   && e.FishingLevel.Value   != 0)
            || (effectNames.Contains("MiningLevel")    && e.MiningLevel.Value    != 0)
            || (effectNames.Contains("LuckLevel")      && e.LuckLevel.Value      != 0)
            || (effectNames.Contains("ForagingLevel")  && e.ForagingLevel.Value  != 0)
            || (effectNames.Contains("MaxStamina")     && e.MaxStamina.Value     != 0)
            || (effectNames.Contains("MagneticRadius") && e.MagneticRadius.Value != 0)
            || (effectNames.Contains("Speed")          && e.Speed.Value          != 0)
            || (effectNames.Contains("Defense")        && e.Defense.Value        != 0)
            || (effectNames.Contains("Attack")         && e.Attack.Value         != 0)
            || (effectNames.Contains("CombatLevel")    && e.CombatLevel.Value    != 0)
            || (effectNames.Contains("Immunity")       && e.Immunity.Value       != 0);
    }

    private static void FireActionsOnApply(Buff buff)
    {
        if (buff.actionsOnApply is not { Length: > 0 }) return;
        foreach (string action in buff.actionsOnApply)
            TriggerActionManager.TryRunAction(action, out _, out _);
    }
}
