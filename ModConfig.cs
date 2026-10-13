namespace StackBuffs;

public enum StackingMode { Additive, Independent, RefreshAndStack, TimerOnly }

public class ModConfig
{
    public StackingMode Mode { get; set; } = StackingMode.Additive;
    public bool WholeDay { get; set; } = false;
    public HashSet<string> DisabledEffects { get; set; } = new();
    public HashSet<string> WholeDayEffects { get; set; } = new();
    public bool EnableStatueStacking { get; set; } = true;

    public bool DwarfAutoPick { get; set; } = false;
    public bool DwarfRandomizeOffering { get; set; } = false;
    public bool DwarfCalicoEggScaling  { get; set; } = true;
    public bool DwarfGemNodeScaling    { get; set; } = true;
    public bool DwarfMusselNodeScaling { get; set; } = true;

    /// <summary>Highest priority rank a Dwarf King blessing can have (picked first).</summary>
    public const int MinPriorityRank = 1;
    /// <summary>Lowest priority rank a Dwarf King blessing can have (picked last).</summary>
    public const int MaxPriorityRank = 5;

    // Priority rank for each Dwarf King blessing (1 = picked first, 5 = last).
    // When auto-pick is on, the lower-ranked offering is selected automatically.
    public int DwarfPriority_ExtraOre      { get; set; } = 1; // idx 0: +1 ore
    public int DwarfPriority_Ladders       { get; set; } = 2; // idx 1: ladders/shafts
    public int DwarfPriority_Coal          { get; set; } = 3; // idx 2: coal
    public int DwarfPriority_BombImmunity  { get; set; } = 4; // idx 3: bomb immunity
    public int DwarfPriority_Geodes        { get; set; } = 5; // idx 4: geodes

    internal int DwarfBlessingRank(int idx) => idx switch
    {
        0 => DwarfPriority_ExtraOre,
        1 => DwarfPriority_Ladders,
        2 => DwarfPriority_Coal,
        3 => DwarfPriority_BombImmunity,
        4 => DwarfPriority_Geodes,
        _ => int.MaxValue
    };
}
