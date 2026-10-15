using HarmonyLib;
using Netcode;
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
        // Every branch below mutates state directly and returns false, bypassing vanilla Apply
        // (which itself no-ops for a non-locally-controlled manager, e.g. a remote farmer's buffs
        // in multiplayer). Fall through to vanilla here so that guard still applies to us too.
        if (!__instance.IsLocallyControlled()) return true;
        if (DataLoader.Buffs(Game1.content).TryGetValue(buff.id, out var buffData) && buffData.IsDebuff)
            return true;
        bool isWholeDay = Config?.WholeDay == true
                       || (Config?.WholeDayEffects is { Count: > 0 } wde && HasMatchingEffect(buff, wde));
        // The whole-day options are for buffs the game hands out (food, drinks, statues). A buff another
        // mod manages itself keeps its own timer: CJB Cheats, for one, renews its buff once its timer reads
        // 10s or less, and an endless buff's timer reads -2, so it would re-apply every tick.
        if (isWholeDay && buff.millisecondsDuration != Buff.ENDLESS && CountsAsFoodForWholeDay(__instance, buff))
        {
            buff.millisecondsDuration = Buff.ENDLESS;
            buff.totalMillisecondsDuration = Buff.ENDLESS;
        }

        var mode = Config?.Mode ?? StackingMode.Additive;
        // A slot only holds a StackedBuff in Independent mode; if one is left over (mode changed without
        // the menu's callback), fold it into a plain buff so the other modes see what they expect.
        if (mode != StackingMode.Independent
            && __instance.AppliedBuffs.TryGetValue(buff.id, out var leftover) && leftover is StackedBuff leftoverStack)
            CollapseContainer(__instance, buff.id, leftoverStack);

        switch (mode)
        {
            case StackingMode.Independent:
                return ApplyIndependent(__instance, buff);

            case StackingMode.Additive:
                if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingAdd))
                {
                    if (ShouldModReplace(existingAdd, buff)) return true;
                    MergeEffects(existingAdd.effects, buff.effects, Config?.DisabledEffects);
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
                    if (ShouldModReplace(existingRAS, buff)) return true;
                    MergeEffects(existingRAS.effects, buff.effects, Config?.DisabledEffects);
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
                    if (ShouldModReplace(existingTO, buff)) return true;
                    if (Config?.DisabledEffects is { Count: > 0 } disabledTO)
                        OverwriteDisabledEffects(existingTO.effects, buff.effects, disabledTO);
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

    /// <summary>
    /// Independent mode: each source gets its own copy, timer and icon. Copies sharing an id (e.g. Coffee and
    /// Triple Shot Espresso, both "drink") live in one <see cref="StackedBuff"/> slot under the plain id, so
    /// lookups by id keep working for the game and other mods.
    /// </summary>
    private static bool ApplyIndependent(BuffManager manager, Buff buff)
    {
        // Blessings of the same type always combine into one instance regardless of source
        // (multiple statues stacking together) - they already have unique per-blessing-type ids.
        if (buff.id.StartsWith("statue_of_blessings_"))
        {
            if (manager.AppliedBuffs.TryGetValue(buff.id, out var existingBlessing))
            {
                MergeEffects(existingBlessing.effects, buff.effects, Config?.DisabledEffects);
                if (existingBlessing.millisecondsDuration != Buff.ENDLESS &&
                    buff.millisecondsDuration != Buff.ENDLESS &&
                    buff.millisecondsDuration > existingBlessing.millisecondsDuration)
                {
                    existingBlessing.millisecondsDuration = buff.millisecondsDuration;
                    existingBlessing.totalMillisecondsDuration = buff.millisecondsDuration;
                }
                FireActionsOnApply(buff);
                manager.Dirty = true;
                return false;
            }
            return true;
        }

        if (!manager.AppliedBuffs.TryGetValue(buff.id, out var existing))
            return true;

        if (existing is StackedBuff stacked)
        {
            int sameSourceIndex = stacked.IndexOfSource(buff.source);
            if (sameSourceIndex >= 0)
            {
                Buff sameSource = stacked.Copies[sameSourceIndex];
                if (ShouldModReplace(sameSource, buff))
                {
                    stacked.ReplaceCopy(sameSourceIndex, buff);
                    Game1.buffsDisplay.updatedIDs.Add(buff.id);
                }
                else
                {
                    RefreshTimer(sameSource, buff);
                    stacked.Recompute();
                }
                FireActionsOnApply(buff);
                manager.Dirty = true;
                return false;
            }

            // A disabled effect uses vanilla replacement: if the incoming buff has that effect, it wins
            // for it, so strip it from the other copies before adding ours.
            if (Config?.DisabledEffects is { Count: > 0 } disabled)
            {
                stacked.RemoveCopies(copy => ClearDisabledEffects(copy, buff, disabled));
                // Emptied copies were removed, which may have turned the slot back into one plain buff or
                // removed it. Start over against whatever is there now; the effects are already cleared, so
                // the second pass doesn't clear anything again.
                if (!manager.AppliedBuffs.TryGetValue(buff.id, out var afterClear) || afterClear != stacked)
                    return ApplyIndependent(manager, buff);
            }

            stacked.AddCopy(buff);
            AddedNewCopy(manager, buff);
            return false;
        }

        if (existing.source == buff.source)
        {
            // Re-applying the same source while its buff is still active.
            if (ShouldModReplace(existing, buff)) return true;
            RefreshTimer(existing, buff);
            FireActionsOnApply(buff);
            manager.Dirty = true;
            return false;
        }

        if (Config?.DisabledEffects is { Count: > 0 } disabledPlain && ClearDisabledEffects(existing, buff, disabledPlain))
        {
            // The existing buff had nothing left: vanilla replacement.
            return true;
        }

        manager.AppliedBuffs[buff.id] = new StackedBuff(manager, existing, buff);
        AddedNewCopy(manager, buff);
        return false;
    }

    /// <summary>Vanilla Apply's side effects for a newly added copy (its own OnAdded, the buff bar pulse, trigger actions).</summary>
    private static void AddedNewCopy(BuffManager manager, Buff buff)
    {
        Game1.buffsDisplay.updatedIDs.Add(buff.id);
        FireActionsOnApply(buff);
        manager.Dirty = true;
        buff.OnAdded();
    }

    /// <summary>Refresh an applied buff's timer from a re-application by the same source.</summary>
    private static void RefreshTimer(Buff existing, Buff incoming)
    {
        if (existing.millisecondsDuration == Buff.ENDLESS) return;
        existing.millisecondsDuration = incoming.millisecondsDuration;
        existing.totalMillisecondsDuration = incoming.millisecondsDuration;
    }

    /// <summary>
    /// Whether a re-application should use vanilla replacement instead of stacking: the same source is
    /// re-applying a buff that's still active, and that source is another C# mod updating its own buff
    /// (see <see cref="BuffCaller"/>), not the game handing out food, drinks or statue buffs.
    /// </summary>
    private static bool ShouldModReplace(Buff existing, Buff incoming)
    {
        if (Config?.LetModsManageOwnBuffs == false) return false;
        if (existing.source != incoming.source) return false;
        return BuffCaller.IsFromMod(incoming);
    }

    /// <summary>
    /// Whether the whole-day options should make <paramref name="buff"/> endless: the game hands it out (food,
    /// drinks, statues, rings, Content Patcher data), or it's a C# food framework's buff for the item being
    /// eaten. Those frameworks (e.g. BuffFramework) apply their buff once while the eating animation plays
    /// and only reset its timer afterwards, so "being eaten" is limited to a buff that isn't active yet:
    /// a mod renewing its own active buff mid-eating (CJB Cheats, Horse Master) keeps its timer.
    /// </summary>
    private static bool CountsAsFoodForWholeDay(BuffManager manager, Buff buff)
    {
        if (Game1.player.isEating && !manager.AppliedBuffs.ContainsKey(buff.id)) return true;
        return !BuffCaller.IsFromMod(buff);
    }

    /// <summary>Add <paramref name="incoming"/> into <paramref name="target"/>, field by field. A field named in <paramref name="disabledEffects"/> is overwritten (vanilla replacement) instead of added, so disabling one effect no longer discards unrelated effects already stacked onto the same buff.</summary>
    internal static void MergeEffects(BuffEffects target, BuffEffects incoming, HashSet<string>? disabledEffects)
    {
        if (incoming is not { } e) return;
        MergeField(target.FarmingLevel,   e.FarmingLevel,   disabledEffects, "FarmingLevel");
        MergeField(target.FishingLevel,   e.FishingLevel,   disabledEffects, "FishingLevel");
        MergeField(target.MiningLevel,    e.MiningLevel,    disabledEffects, "MiningLevel");
        MergeField(target.LuckLevel,      e.LuckLevel,      disabledEffects, "LuckLevel");
        MergeField(target.ForagingLevel,  e.ForagingLevel,  disabledEffects, "ForagingLevel");
        MergeField(target.MaxStamina,     e.MaxStamina,     disabledEffects, "MaxStamina");
        MergeField(target.MagneticRadius, e.MagneticRadius, disabledEffects, "MagneticRadius");
        MergeField(target.Speed,          e.Speed,          disabledEffects, "Speed");
        MergeField(target.Defense,        e.Defense,        disabledEffects, "Defense");
        MergeField(target.Attack,         e.Attack,         disabledEffects, "Attack");
        MergeField(target.CombatLevel,    e.CombatLevel,    disabledEffects, "CombatLevel");
        MergeField(target.Immunity,       e.Immunity,       disabledEffects, "Immunity");
        // Not exposed as per-effect options, so these always add, same as vanilla BuffEffects.Add.
        target.AttackMultiplier.Value          += e.AttackMultiplier.Value;
        target.KnockbackMultiplier.Value       += e.KnockbackMultiplier.Value;
        target.WeaponSpeedMultiplier.Value     += e.WeaponSpeedMultiplier.Value;
        target.CriticalChanceMultiplier.Value  += e.CriticalChanceMultiplier.Value;
        target.CriticalPowerMultiplier.Value   += e.CriticalPowerMultiplier.Value;
        target.WeaponPrecisionMultiplier.Value += e.WeaponPrecisionMultiplier.Value;
    }

    private static void MergeField(NetFloat target, NetFloat incoming, HashSet<string>? disabledEffects, string name)
    {
        if (disabledEffects != null && disabledEffects.Contains(name))
        {
            // Replace only when the incoming buff actually grants this effect, so a buff without it
            // doesn't wipe it (same rule as ClearDisabledEffects in Independent mode).
            if (incoming.Value != 0)
                target.Value = incoming.Value;
        }
        else
            target.Value += incoming.Value;
    }

    /// <summary>Pair each disabled effect's field on <paramref name="e"/> with the same field on <paramref name="incoming"/>.</summary>
    private static IEnumerable<(NetFloat Existing, NetFloat Incoming)> DisabledFields(BuffEffects e, BuffEffects incoming, HashSet<string> names)
    {
        if (names.Contains("FarmingLevel"))   yield return (e.FarmingLevel,   incoming.FarmingLevel);
        if (names.Contains("FishingLevel"))   yield return (e.FishingLevel,   incoming.FishingLevel);
        if (names.Contains("MiningLevel"))    yield return (e.MiningLevel,    incoming.MiningLevel);
        if (names.Contains("LuckLevel"))      yield return (e.LuckLevel,      incoming.LuckLevel);
        if (names.Contains("ForagingLevel"))  yield return (e.ForagingLevel,  incoming.ForagingLevel);
        if (names.Contains("MaxStamina"))     yield return (e.MaxStamina,     incoming.MaxStamina);
        if (names.Contains("MagneticRadius")) yield return (e.MagneticRadius, incoming.MagneticRadius);
        if (names.Contains("Speed"))          yield return (e.Speed,          incoming.Speed);
        if (names.Contains("Defense"))        yield return (e.Defense,        incoming.Defense);
        if (names.Contains("Attack"))         yield return (e.Attack,         incoming.Attack);
        if (names.Contains("CombatLevel"))    yield return (e.CombatLevel,    incoming.CombatLevel);
        if (names.Contains("Immunity"))       yield return (e.Immunity,       incoming.Immunity);
    }

    /// <summary>Timer Only's vanilla replacement for disabled effects: the incoming value wins, but only where the incoming buff grants that effect.</summary>
    private static void OverwriteDisabledEffects(BuffEffects? target, BuffEffects? incoming, HashSet<string> disabled)
    {
        if (target is null || incoming is null) return;
        foreach (var (existing, incomingField) in DisabledFields(target, incoming, disabled))
        {
            if (incomingField.Value != 0)
                existing.Value = incomingField.Value;
        }
    }

    /// <summary>
    /// Independent-mode equivalent of <see cref="MergeField"/>'s overwrite for disabled effects: for each
    /// disabled effect the incoming buff actually grants, zero it on <paramref name="target"/>, so only the
    /// incoming buff's value for it counts. Disabled effects the incoming buff doesn't grant are left alone,
    /// so an unrelated buff doesn't wipe them.
    /// </summary>
    /// <returns>Whether <paramref name="target"/> was left with no effects at all, so it should be removed (vanilla would have replaced it outright).</returns>
    private static bool ClearDisabledEffects(Buff target, Buff incoming, HashSet<string> disabled)
    {
        if (incoming.effects is null || target.effects is null) return false;
        bool cleared = false;
        foreach (var (existing, incomingField) in DisabledFields(target.effects, incoming.effects, disabled))
        {
            if (incomingField.Value == 0 || existing.Value == 0) continue;
            existing.Value = 0;
            cleared = true;
        }
        return cleared && !target.effects.HasAnyValue();
    }

    /// <summary>
    /// Called when the stacking mode changes mid-game. Only Independent mode uses <see cref="StackedBuff"/>
    /// slots, so when leaving it, fold each one into a single plain buff that the other modes can stack onto.
    /// </summary>
    internal static void OnModeChanged(BuffManager manager)
    {
        if (!manager.IsLocallyControlled()) return;
        if ((Config?.Mode ?? StackingMode.Additive) == StackingMode.Independent) return;

        foreach (var (id, buff) in manager.AppliedBuffs.ToList())
        {
            if (buff is StackedBuff stacked)
                CollapseContainer(manager, id, stacked);
        }
    }

    /// <summary>Replace a <see cref="StackedBuff"/> slot with its oldest copy, with the other copies' effects merged in and the longest timer.</summary>
    private static void CollapseContainer(BuffManager manager, string id, StackedBuff stacked)
    {
        List<Buff> copies = stacked.Copies;
        if (copies.Count == 0)
        {
            manager.Remove(id);
            return;
        }

        Buff merged = copies[0];
        for (int i = 1; i < copies.Count; i++)
        {
            Buff other = copies[i];
            MergeEffects(merged.effects, other.effects, Config?.DisabledEffects);
            if (merged.millisecondsDuration != Buff.ENDLESS &&
                (other.millisecondsDuration == Buff.ENDLESS || other.millisecondsDuration > merged.millisecondsDuration))
            {
                merged.millisecondsDuration = other.millisecondsDuration;
                merged.totalMillisecondsDuration = other.millisecondsDuration;
            }
            other.OnRemoved();
        }
        copies.Clear();
        manager.AppliedBuffs[id] = merged;
        manager.Dirty = true;
    }

    /// <summary>Remove buffs made endless by the whole-day options, unless the buff's own data says it's endless anyway.</summary>
    internal static void RemoveWholeDayBuffs(BuffManager manager)
    {
        var dataBuffs = DataLoader.Buffs(Game1.content);
        bool IsWholeDay(Buff buff) =>
            buff.millisecondsDuration == Buff.ENDLESS
            && (!dataBuffs.TryGetValue(buff.id, out var d) || d.Duration != Buff.ENDLESS);

        foreach (var (id, buff) in manager.AppliedBuffs.ToList())
        {
            if (buff is StackedBuff stacked)
                stacked.RemoveCopies(IsWholeDay);
            else if (IsWholeDay(buff))
                manager.Remove(id);
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
