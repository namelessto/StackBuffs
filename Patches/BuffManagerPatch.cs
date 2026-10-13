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
        if (isWholeDay && buff.millisecondsDuration != Buff.ENDLESS)
        {
            buff.millisecondsDuration = Buff.ENDLESS;
            buff.totalMillisecondsDuration = Buff.ENDLESS;
        }

        switch (Config?.Mode ?? StackingMode.Additive)
        {
            case StackingMode.Independent:
            {
                // Blessings of the same type always combine into one instance regardless of source
                // (multiple statues stacking together), same as before - they already have unique
                // per-blessing-type ids, so no key changes are needed here.
                if (buff.id.StartsWith("statue_of_blessings_"))
                {
                    if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingBlessing))
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
                        __instance.Dirty = true;
                        return false;
                    }
                    return true;
                }

                // Everything else: key by (id, source) instead of just id, so buffs sharing a
                // generic id (e.g. "food"/"drink") from different sources each get their own
                // entry - their own icon and their own independently-ticking timer - instead of
                // being merged into a single shared instance. The combined stat effect still adds
                // up normally, since BuffManager.GetValues() sums every applied buff's effects
                // regardless of what key it's stored under.
                string key = ComposeIndependentKey(buff.id, buff.source);
                if (__instance.AppliedBuffs.TryGetValue(key, out var existingSameSource))
                {
                    // Re-applying the same source while its buff is still active: just refresh the timer.
                    if (existingSameSource.millisecondsDuration != Buff.ENDLESS)
                    {
                        existingSameSource.millisecondsDuration = buff.millisecondsDuration;
                        existingSameSource.totalMillisecondsDuration = buff.millisecondsDuration;
                    }
                    FireActionsOnApply(buff);
                    __instance.Dirty = true;
                    return false;
                }

                // A disabled effect uses vanilla replacement: if the incoming buff has that effect, it
                // wins for it, so strip it from every other entry sharing the id before adding ours.
                if (Config?.DisabledEffects is { Count: > 0 } disabled)
                    ReplaceDisabledEffects(__instance, buff, disabled);

                __instance.AppliedBuffs[key] = buff;
                __instance.AppliedBuffIds.Add(key);
                Game1.buffsDisplay.updatedIDs.Add(buff.id);
                FireActionsOnApply(buff);
                __instance.Dirty = true;
                buff.OnAdded();
                return false;
            }

            case StackingMode.Additive:
                if (__instance.AppliedBuffs.TryGetValue(buff.id, out var existingAdd))
                {
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

    /// <summary>Build a per-source dictionary key for Independent mode, so buffs that share a generic id (like "food") but come from different sources don't collide in <see cref="BuffManager.AppliedBuffs"/>.</summary>
    private static string ComposeIndependentKey(string id, string? source) => id + IndependentKeySeparator + (source ?? "");

    /// <summary>Separates id and source in an Independent-mode key. A control character, so it can't appear in a real buff id.</summary>
    private const char IndependentKeySeparator = '\u001F';

    /// <summary>Whether <paramref name="key"/> is an Independent-mode composed key for <paramref name="id"/>. Allocation-free, since it runs on every <see cref="Farmer.hasBuff"/> call.</summary>
    private static bool IsComposedKeyFor(string key, string id) =>
        key.Length > id.Length
        && key[id.Length] == IndependentKeySeparator
        && key.StartsWith(id, StringComparison.Ordinal);

    /// <summary>
    /// Independent mode stores buffs under a composed id+source key instead of the plain id
    /// (see <see cref="ComposeIndependentKey"/>), so vanilla <see cref="BuffManager.IsApplied"/> — and
    /// everything built on it, like <see cref="Farmer.hasBuff"/> and the PLAYER_HAS_BUFF game state query —
    /// would otherwise never find them via a literal id match. This patch makes those lookups also match
    /// a composed key by its id prefix, so id-based checks keep working regardless of stacking mode.
    /// </summary>
    [HarmonyPatch(typeof(BuffManager), nameof(BuffManager.IsApplied))]
    internal static class IsAppliedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(BuffManager __instance, string id, ref bool __result)
        {
            if (__instance.AppliedBuffIds.Contains(id))
            {
                __result = true;
                return false;
            }
            var appliedIds = __instance.AppliedBuffIds;
            for (int i = 0; i < appliedIds.Count; i++)
            {
                if (IsComposedKeyFor(appliedIds[i], id))
                {
                    __result = true;
                    return false;
                }
            }
            __result = false;
            return false;
        }
    }

    /// <summary>Add <paramref name="incoming"/> into <paramref name="target"/>, field by field. A field named in <paramref name="disabledEffects"/> is overwritten (vanilla replacement) instead of added, so disabling one effect no longer discards unrelated effects already stacked onto the same buff.</summary>
    private static void MergeEffects(BuffEffects target, BuffEffects incoming, HashSet<string>? disabledEffects)
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
            // doesn't wipe it (same rule as ReplaceDisabledEffects in Independent mode).
            if (incoming.Value != 0)
                target.Value = incoming.Value;
        }
        else
            target.Value += incoming.Value;
    }

    /// <summary>Find an applied buff by its plain id, whether it's stored under that id or under an Independent-mode composed key.</summary>
    internal static Buff? FindApplied(BuffManager manager, string id)
    {
        if (manager.AppliedBuffs.TryGetValue(id, out var buff)) return buff;
        foreach (var (key, value) in manager.AppliedBuffs)
        {
            if (KeyMatchesId(key, id)) return value;
        }
        return null;
    }

    /// <summary>
    /// Vanilla <see cref="BuffManager.Remove"/> only matches the exact key, so removing by plain id (e.g.
    /// MineShaft.OnLeftMines removing "CalicoStatueSpeed", or the RemoveBuff trigger action) would miss
    /// buffs Independent mode stored under a composed id+source key. This also removes those entries.
    /// </summary>
    [HarmonyPatch(typeof(BuffManager), nameof(BuffManager.Remove))]
    internal static class RemovePatch
    {
        [HarmonyPostfix]
        static void Postfix(BuffManager __instance, string id)
        {
            if (!__instance.IsLocallyControlled()) return;
            // No key is a composed key for another composed key, so the nested Remove(key) calls below
            // find nothing more to remove and don't recurse further.
            foreach (string key in __instance.AppliedBuffs.Keys.Where(k => IsComposedKeyFor(k, id)).ToList())
                __instance.Remove(key);
        }
    }

    /// <summary>Whether <paramref name="key"/> is the plain id or an Independent-mode composed key for <paramref name="id"/>.</summary>
    private static bool KeyMatchesId(string key, string id) =>
        key == id || IsComposedKeyFor(key, id);

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
    /// disabled effect the incoming buff actually grants, zero it on every other applied buff sharing its id,
    /// so only the incoming buff's value for it counts. Disabled effects the incoming buff doesn't grant are
    /// left alone, so an unrelated buff doesn't wipe them. A buff left with no effects at all by this is
    /// removed, as vanilla would have replaced it outright.
    /// </summary>
    private static void ReplaceDisabledEffects(BuffManager manager, Buff incoming, HashSet<string> disabled)
    {
        if (incoming.effects is null) return;
        foreach (string key in manager.AppliedBuffs.Keys.Where(k => KeyMatchesId(k, incoming.id)).ToList())
        {
            var effects = manager.AppliedBuffs[key].effects;
            if (effects is null) continue;
            bool cleared = false;
            foreach (var (existing, incomingField) in DisabledFields(effects, incoming.effects, disabled))
            {
                if (incomingField.Value == 0 || existing.Value == 0) continue;
                existing.Value = 0;
                cleared = true;
            }
            if (!cleared) continue;
            if (effects.HasAnyValue())
                manager.Dirty = true;
            else
                manager.Remove(key);
        }
    }

    /// <summary>
    /// Re-key the local player's applied buffs to match the current stacking mode, so a mode change made
    /// mid-game doesn't leave entries under the other mode's key scheme (plain id vs. Independent's
    /// id+source key), where the new mode's lookups would miss them and stack a duplicate alongside.
    /// </summary>
    internal static void NormalizeKeys(BuffManager manager)
    {
        if (!manager.IsLocallyControlled()) return;
        var dataBuffs = DataLoader.Buffs(Game1.content);
        bool independent = (Config?.Mode ?? StackingMode.Additive) == StackingMode.Independent;
        bool changed = false;

        foreach (var (key, buff) in manager.AppliedBuffs.ToList())
        {
            // Same exclusions as Prefix: debuffs and blessings always stay under their plain id.
            if (buff.id.StartsWith("statue_of_blessings_")) continue;
            if (dataBuffs.TryGetValue(buff.id, out var data) && data.IsDebuff) continue;

            string wantKey = independent ? ComposeIndependentKey(buff.id, buff.source) : buff.id;
            if (key == wantKey) continue;

            manager.AppliedBuffs.Remove(key);
            if (manager.AppliedBuffs.TryGetValue(wantKey, out var target))
            {
                // Two entries collapse into one key: combine them, keeping the longer timer.
                MergeEffects(target.effects, buff.effects, Config?.DisabledEffects);
                if (target.millisecondsDuration != Buff.ENDLESS &&
                    (buff.millisecondsDuration == Buff.ENDLESS || buff.millisecondsDuration > target.millisecondsDuration))
                {
                    target.millisecondsDuration = buff.millisecondsDuration;
                    target.totalMillisecondsDuration = buff.millisecondsDuration;
                }
                buff.OnRemoved();
            }
            else
            {
                manager.AppliedBuffs[wantKey] = buff;
            }
            changed = true;
        }

        if (!changed) return;
        manager.AppliedBuffIds.Clear();
        foreach (string key in manager.AppliedBuffs.Keys)
            manager.AppliedBuffIds.Add(key);
        manager.Dirty = true;
    }

    /// <summary>Remove buffs made endless by the whole-day options, unless the buff's own data says it's endless anyway.</summary>
    internal static void RemoveWholeDayBuffs(BuffManager manager)
    {
        var dataBuffs = DataLoader.Buffs(Game1.content);
        var toRemove = manager.AppliedBuffs
            .Where(kv => kv.Value.millisecondsDuration == Buff.ENDLESS
                      && (!dataBuffs.TryGetValue(kv.Value.id, out var d) || d.Duration != Buff.ENDLESS))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in toRemove)
            manager.Remove(key);
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
