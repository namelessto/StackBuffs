using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;

namespace StackBuffs.Patches;

/// <summary>
/// Tells whether a buff is being applied by the game or by another C# mod.
/// <para>
/// Vanilla <see cref="BuffManager.Apply"/> replaces a buff with the same id, and mods rely on that to update
/// their own buff: Horse Master and Movement Mod re-apply with a new speed, CJB Cheats renews its buff before
/// it runs out, Daleao's Professions re-applies with its own running total. Stacking those re-applies gives
/// wrong or runaway values. Buffs the game hands out (eating, statues, rings, Content Patcher data and trigger
/// actions) all come from game code, so "who is calling" separates the two without any per-mod list.
/// </para>
/// </summary>
internal static class BuffCaller
{
    private static readonly Assembly OwnAssembly = typeof(BuffCaller).Assembly;
    private static readonly Assembly GameAssembly = typeof(Game1).Assembly;
    private static readonly Assembly SmapiAssembly = typeof(IModHelper).Assembly;

    /// <summary>
    /// Whether <paramref name="buff"/> is being applied by a C# mod rather than by the game. Call only from
    /// inside <see cref="BuffManager.Apply"/>.
    /// <para>
    /// Not cached: the game and a mod can apply the same id with the same source (e.g. <c>applyBuff("20")</c>
    /// leaves the source null), so only the call stack tells them apart. The walk only runs when a buff
    /// conflicts with an active one or Buffs Last All Day would apply, not on every apply.
    /// </para>
    /// </summary>
    internal static bool IsFromMod(Buff buff) => IsCalledFromMod();

    /// <summary>
    /// Walk the call stack outward past StackBuffs, Harmony and the buff plumbing itself
    /// (<see cref="BuffManager"/>, <see cref="Farmer.applyBuff(Buff)"/>), and check whose code the first
    /// remaining frame belongs to.
    /// </summary>
    private static bool IsCalledFromMod()
    {
        var trace = new StackTrace(skipFrames: 1, fNeedFileInfo: false);
        for (int i = 0; i < trace.FrameCount; i++)
        {
            StackFrame? frame = trace.GetFrame(i);
            if (frame is null) continue;
            // A Harmony-patched method shows up as a generated replacement; map it back to the original.
            MethodBase? method = Harmony.GetOriginalMethodFromStackframe(frame) ?? frame.GetMethod();
            Type? type = method?.DeclaringType;
            if (type is null) continue;
            if (type == typeof(BuffManager)) continue;
            if (type == typeof(Farmer) && method!.Name == nameof(Farmer.applyBuff)) continue;

            Assembly assembly = type.Assembly;
            if (assembly == OwnAssembly || IsInfrastructure(assembly)) continue;
            return assembly != GameAssembly && assembly != SmapiAssembly;
        }
        return false;
    }

    /// <summary>Whether an assembly is runtime or patching infrastructure rather than game or mod code.</summary>
    private static bool IsInfrastructure(Assembly assembly)
    {
        string name = assembly.GetName().Name ?? "";
        return name.StartsWith("0Harmony", StringComparison.Ordinal)
            || name.StartsWith("MonoMod", StringComparison.Ordinal)
            || name.StartsWith("System", StringComparison.Ordinal)
            || name.Equals("mscorlib", StringComparison.Ordinal)
            || name.Equals("netstandard", StringComparison.Ordinal);
    }
}
