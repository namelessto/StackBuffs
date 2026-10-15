using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Menus;

namespace StackBuffs.Patches;

/// <summary>
/// Independent mode's slot for a buff id that has two or more copies from different sources (e.g. Coffee and
/// Triple Shot Espresso, both id "drink"). It sits in <see cref="BuffManager.AppliedBuffs"/> under the plain id,
/// so every vanilla and mod lookup by id still finds a buff there, while each copy keeps its own timer and icon.
/// <para>
/// A single copy is never wrapped: the slot holds the plain buff itself, and <see cref="update"/> puts the last
/// remaining copy back in the slot. Mods that edit the buff they look up (Love of Cooking changes its
/// <see cref="Buff.millisecondsDuration"/> and effects directly) would otherwise be editing a sum that gets
/// recalculated, so such edits are passed on to the newest copy.
/// </para>
/// </summary>
internal sealed class StackedBuff : Buff
{
    /// <summary>The number of effect fields reachable through <see cref="EffectField"/>.</summary>
    internal const int EffectFieldCount = 18;

    /// <summary>The manager this slot belongs to.</summary>
    private readonly BuffManager Manager;

    /// <summary>The copies in this slot, oldest first.</summary>
    internal readonly List<Buff> Copies = new();

    /// <summary>The duration this container last set on itself, to detect another mod changing it.</summary>
    private int LastWrittenDuration;

    /// <summary>The effect values this container last set on itself, to detect another mod changing them.</summary>
    private readonly float[] LastWrittenEffects = new float[EffectFieldCount];

    /// <summary>Wrap an applied buff and a newly applied buff with the same id into one slot.</summary>
    internal StackedBuff(BuffManager manager, Buff existing, Buff incoming)
        : base(existing.id, existing.source, existing.displaySource, Buff.ENDLESS, isDebuff: false,
               displayName: existing.displayName, description: existing.description)
    {
        Manager = manager;
        // No own icon: if the buff bar patch below ever stops applying, vanilla shows one combined
        // icon per effect from the summed effects instead of a misleading single icon.
        iconTexture = null;
        Copies.Add(existing);
        Copies.Add(incoming);
        Recompute();
    }

    /// <summary>The index of the copy applied by <paramref name="source"/>, or -1.</summary>
    internal int IndexOfSource(string? source)
    {
        for (int i = 0; i < Copies.Count; i++)
        {
            if (Copies[i].source == source) return i;
        }
        return -1;
    }

    /// <summary>Add a copy from a new source.</summary>
    internal void AddCopy(Buff buff)
    {
        Copies.Add(buff);
        Recompute();
    }

    /// <summary>Replace a copy with a newly applied buff from the same source (vanilla replacement for that copy only).</summary>
    internal void ReplaceCopy(int index, Buff buff)
    {
        Copies[index].OnRemoved();
        Copies.RemoveAt(index);
        Copies.Add(buff);
        buff.OnAdded();
        Recompute();
    }

    /// <summary>Remove every copy matching <paramref name="shouldRemove"/>, then settle the slot.</summary>
    internal void RemoveCopies(Func<Buff, bool> shouldRemove)
    {
        bool removedAny = false;
        for (int i = Copies.Count - 1; i >= 0; i--)
        {
            if (!shouldRemove(Copies[i])) continue;
            Copies[i].OnRemoved();
            Copies.RemoveAt(i);
            removedAny = true;
        }
        if (!removedAny) return;
        Recompute();
        Manager.Dirty = true;
        SettleSlot();
    }

    /// <summary>Put a single remaining copy back in the slot as itself, or remove the slot when no copies are left.</summary>
    internal void SettleSlot()
    {
        if (Copies.Count == 1)
        {
            Manager.AppliedBuffs[id] = Copies[0];
            Copies.Clear();
            Manager.Dirty = true;
        }
        else if (Copies.Count == 0)
        {
            Manager.Remove(id);
        }
    }

    /// <summary>Recalculate the summed effects, the longest timer and the glow from the copies.</summary>
    internal void Recompute()
    {
        effects.Clear();
        glow = default;
        foreach (Buff copy in Copies)
        {
            effects.Add(copy.effects);
            if (glow.A == 0 && copy.glow != Color.White && copy.glow.A > 0)
                glow = copy.glow;
        }
        RecomputeDuration();
        for (int i = 0; i < EffectFieldCount; i++)
            LastWrittenEffects[i] = EffectField(effects, i).Value;
    }

    /// <summary>Set this slot's timer to the longest copy's timer, or <see cref="Buff.ENDLESS"/> if any copy is endless.</summary>
    private void RecomputeDuration()
    {
        int longest = 0;
        foreach (Buff copy in Copies)
        {
            if (copy.millisecondsDuration == ENDLESS)
            {
                longest = ENDLESS;
                break;
            }
            longest = Math.Max(longest, copy.millisecondsDuration);
        }
        millisecondsDuration = longest;
        totalMillisecondsDuration = longest;
        LastWrittenDuration = longest;
    }

    /// <inheritdoc />
    public override bool update(GameTime time)
    {
        bool changed = PassOnOutsideEdits();

        for (int i = Copies.Count - 1; i >= 0; i--)
        {
            if (!Copies[i].update(time)) continue;
            Copies[i].OnRemoved();
            Copies.RemoveAt(i);
            changed = true;
        }

        if (Copies.Count == 0) return true;
        if (Copies.Count == 1)
        {
            // Safe during BuffManager.Update: it loops over AppliedBuffIds by index, not over this dictionary.
            SettleSlot();
            return false;
        }

        if (changed)
        {
            Recompute();
            Manager.Dirty = true;
        }
        else
        {
            RecomputeDuration();
        }
        return false;
    }

    /// <summary>Apply any change another mod made to this slot's timer or effects since the last update to the newest copy, so it isn't lost when the slot is recalculated.</summary>
    /// <returns>Whether any effect was changed.</returns>
    private bool PassOnOutsideEdits()
    {
        Buff newest = Copies[^1];

        if (millisecondsDuration != LastWrittenDuration)
        {
            if (millisecondsDuration == ENDLESS || LastWrittenDuration == ENDLESS || newest.millisecondsDuration == ENDLESS)
                newest.millisecondsDuration = millisecondsDuration;
            else
                newest.millisecondsDuration += millisecondsDuration - LastWrittenDuration;
            if (newest.millisecondsDuration != ENDLESS)
                newest.totalMillisecondsDuration = Math.Max(newest.totalMillisecondsDuration, newest.millisecondsDuration);
        }

        bool effectsChanged = false;
        for (int i = 0; i < EffectFieldCount; i++)
        {
            float current = EffectField(effects, i).Value;
            if (current == LastWrittenEffects[i]) continue;
            EffectField(newest.effects, i).Value += current - LastWrittenEffects[i];
            effectsChanged = true;
        }
        return effectsChanged;
    }

    /// <inheritdoc />
    public override void OnRemoved()
    {
        foreach (Buff copy in Copies)
            copy.OnRemoved();
        Copies.Clear();
    }

    /// <summary>Get an effect field by index, without allocating. Indexes 0-11 are the per-effect options in <see cref="BuffManagerPatch.MergeEffects"/> order, 12-17 the multipliers.</summary>
    internal static NetFloat EffectField(BuffEffects e, int index) => index switch
    {
        0  => e.FarmingLevel,
        1  => e.FishingLevel,
        2  => e.MiningLevel,
        3  => e.LuckLevel,
        4  => e.ForagingLevel,
        5  => e.MaxStamina,
        6  => e.MagneticRadius,
        7  => e.Speed,
        8  => e.Defense,
        9  => e.Attack,
        10 => e.CombatLevel,
        11 => e.Immunity,
        12 => e.AttackMultiplier,
        13 => e.KnockbackMultiplier,
        14 => e.WeaponSpeedMultiplier,
        15 => e.CriticalChanceMultiplier,
        16 => e.CriticalPowerMultiplier,
        17 => e.WeaponPrecisionMultiplier,
        _  => throw new ArgumentOutOfRangeException(nameof(index))
    };

    /// <summary>
    /// The buff bar builds its icons from <see cref="BuffsDisplay.GetSortedBuffs"/>. Show each copy in a slot
    /// as its own buff there, so each keeps its own icon, hover time and expiry blink. This only runs when the
    /// buff bar is rebuilt (when buffs change), not every tick.
    /// </summary>
    [HarmonyPatch(typeof(BuffsDisplay), nameof(BuffsDisplay.GetSortedBuffs))]
    internal static class BuffsDisplayPatch
    {
        [HarmonyPostfix]
        static void Postfix(ref IEnumerable<Buff> __result)
        {
            __result = ExpandCopies(__result);
        }

        private static IEnumerable<Buff> ExpandCopies(IEnumerable<Buff> buffs)
        {
            foreach (Buff buff in buffs)
            {
                if (buff is StackedBuff stacked)
                {
                    foreach (Buff copy in stacked.Copies)
                        yield return copy;
                }
                else
                {
                    yield return buff;
                }
            }
        }
    }
}
