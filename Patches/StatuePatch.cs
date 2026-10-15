using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buffs;
using StardewValley.Constants;
using StardewValley.Locations;
using StardewValley.Menus;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using SObject = StardewValley.Object;

namespace StackBuffs.Patches;

internal static class StatuePatch
{
    internal static ModConfig Config = null!;
    internal static IMonitor? Monitor;
    internal static ITranslationHelper? Translation;

    // Vanilla per-buff bonus values, scaled by stack count in the transpilers below. Copied from the
    // decompiled methods named on each one; re-check them after a game update.
    /// <summary>dwarfStatue_1: extra ladder chance on monster kill (<c>MineShaft.monsterDrop</c>).</summary>
    private const double DwarfLadderChanceFromMonsters = 0.07;
    /// <summary>dwarfStatue_1: ladder chance multiplier when breaking stones (<c>MineShaft.checkStoneForItems</c>).</summary>
    private const double DwarfLadderChanceMultiplier = 1.25;
    /// <summary>dwarfStatue_2: extra coal chance from stones outside the mines (<c>GameLocation.OnStoneDestroyed</c>).</summary>
    private const double DwarfCoalChanceOnStoneDestroyed = 0.03;
    /// <summary>dwarfStatue_2: extra coal chance from <c>GameLocation.breakStone</c>.</summary>
    private const double DwarfCoalChanceOnBreakStone = 0.025;
    /// <summary>dwarfStatue_2: extra coal chance from stones in the mines (<c>MineShaft.checkStoneForItems</c>).</summary>
    private const double DwarfCoalChanceInMines = 0.1;
    /// <summary>dwarfStatue_4: geode chance multiplier (<c>GameLocation.OnStoneDestroyed</c>, <c>MineShaft.checkStoneForItems</c>).</summary>
    private const double DwarfGeodeChanceMultiplier = 1.25;
    /// <summary>statue_of_blessings_5: crit chance bonus (<c>GameLocation.damageMonster</c>).</summary>
    private const float BlessingCritChanceBonus = 0.1f;
    /// <summary>statue_of_blessings_4: friendship per conversation (<c>NPC.grantConversationFriendship</c>).</summary>
    private const int BlessingConversationFriendship = 60;

    /// <summary>The private field holding which icon menu this is; null if a game update renamed it.</summary>
    private static readonly FieldInfo? ChooseFromIconsMenuWhichField = AccessTools.Field(typeof(ChooseFromIconsMenu), "which");

    /// <summary>The stack-count suffix appended to each statue buff's description, so the next update can strip exactly what was added.</summary>
    private static readonly ConditionalWeakTable<Buff, string> AppliedStackSuffixes = new();

    private static string PerPlayerKey(SObject statue)
        => $"StackBuffs.usedDay.{(int)statue.TileLocation.X}_{(int)statue.TileLocation.Y}";

    // ── Stack-count helpers ──────────────────────────────────────────────────

    private static void IncrementStatueBuffCount(Farmer who, string buffId)
    {
        string countKey = $"StackBuffs.{buffId}.count";
        string dayKey   = $"StackBuffs.{buffId}.day";
        string today    = Game1.stats.DaysPlayed.ToString();
        int current = 0;
        if (who.modData.TryGetValue(dayKey, out string? day) && day == today
            && who.modData.TryGetValue(countKey, out string? s))
            int.TryParse(s, out current);
        who.modData[countKey] = (current + 1).ToString();
        who.modData[dayKey]   = today;
    }

    // Returns 0 when buff absent; stack count (≥1) when buff is active.
    public static int DwarfBuffCount(Farmer? who, string buffId)
    {
        if (who == null || !who.hasBuff(buffId)) return 0;
        string today = Game1.stats.DaysPlayed.ToString();
        if (!who.modData.TryGetValue($"StackBuffs.{buffId}.day", out string? day) || day != today)
            return 1;
        if (!who.modData.TryGetValue($"StackBuffs.{buffId}.count", out string? s))
            return 1;
        return int.TryParse(s, out int n) ? Math.Max(1, n) : 1;
    }

    // Replaces `hasBuff ? bonus : 0.0` — returns count × bonus (0.0 if absent).
    public static double DwarfBuffBonus(Farmer? who, string buffId, double bonusPerStack)
        => DwarfBuffCount(who, buffId) * bonusPerStack;

    // Replaces `hasBuff ? mult : 1.0` — returns mult^count (1.0 if absent since Math.Pow(X,0)=1).
    public static double DwarfBuffMultiplier(Farmer? who, string buffId, double multiplierPerStack)
        => Math.Pow(multiplierPerStack, DwarfBuffCount(who, buffId));

    public static int BlessingBuffCount(Farmer? who, string buffId)
        => DwarfBuffCount(who, buffId);

    // Called instead of buffs.Remove("statue_of_blessings_6") when a prismatic butterfly is caught.
    // Decrements the stack count; only removes the buff when the last stack is consumed.
    public static void DecrementButterflyStack()
    {
        const string buffId = "statue_of_blessings_6";
        var who = Game1.player;

        if (!Config.EnableStatueStacking)
        {
            who.buffs.Remove(buffId);
            return;
        }

        int count = BlessingBuffCount(who, buffId);
        if (count <= 1)
        {
            if (count > 0)
                who.modData[$"StackBuffs.{buffId}.count"] = "0";
            who.buffs.Remove(buffId);
            return;
        }

        who.modData[$"StackBuffs.{buffId}.count"] = (count - 1).ToString();
        UpdateStatueBuffDescription(who, buffId);
    }

    // Sets the "(×N stacks)" suffix on the description of an already-applied statue buff, replacing
    // any suffix added before, and dropping it once only one stack is left.
    // Called after applyBuff so hasBuff returns true and DwarfBuffCount is valid.
    private static void UpdateStatueBuffDescription(Farmer who, string buffId)
    {
        if (!who.buffs.AppliedBuffs.TryGetValue(buffId, out Buff? buff)) return;
        int count = DwarfBuffCount(who, buffId);

        string desc = buff.description ?? "";
        if (AppliedStackSuffixes.TryGetValue(buff, out string? oldSuffix) && desc.EndsWith(oldSuffix, StringComparison.Ordinal))
            desc = desc[..^oldSuffix.Length];
        AppliedStackSuffixes.Remove(buff);

        if (count > 1)
        {
            string stacksText = Translation?.Get("buff.statue-stacks", new { count }) ?? $"(×{count})";
            string suffix = string.IsNullOrEmpty(desc) ? stacksText : "\n" + stacksText;
            desc += suffix;
            AppliedStackSuffixes.Add(buff, suffix);
        }
        buff.description = desc;
    }

    // Logs a warning when a transpiler's IL pattern isn't found, so a game update that changes the
    // patched method shows up in the SMAPI log instead of stacking silently turning off.
    private static void WarnIfNotFound(bool found, string buffId, string method)
    {
        if (!found)
            Monitor?.Log($"[StackBuffs] {buffId} pattern not found in {method}; its stacking is inactive", LogLevel.Warn);
    }

    // ── Transpiler utilities ─────────────────────────────────────────────────

    // Finds the first occurrence of `ldstr buffId` + `callvirt hasBuff` in codes,
    // then replaces the instruction at (ldstr_index + valueOffset) with makeReplacement.
    // `whoLoad` passed to makeReplacement is the instruction at (ldstr_index - 1).
    private static bool ReplaceFirstBuffValue(
        List<CodeInstruction> codes,
        string buffId,
        int valueOffset,
        Func<CodeInstruction, List<CodeInstruction>> makeReplacement)
    {
        var hasBuff = AccessTools.Method(typeof(Farmer), nameof(Farmer.hasBuff), new[] { typeof(string) });
        for (int i = 1; i < codes.Count - valueOffset; i++)
        {
            if (codes[i].opcode == OpCodes.Ldstr
                && (string)codes[i].operand == buffId
                && codes[i + 1].Calls(hasBuff))
            {
                int idx = i + valueOffset;
                var whoLoad = codes[i - 1];
                var replacement = makeReplacement(whoLoad);
                if (replacement.Count > 0)
                    replacement[0].labels.AddRange(codes[idx].labels);
                codes.RemoveAt(idx);
                codes.InsertRange(idx, replacement);
                return true;
            }
        }
        return false;
    }

    // Replacement builders — each produces instructions that push the scaled value.

    // For `? bonus : 0.0` / `+= bonus`: pushes count × bonus (double)
    private static List<CodeInstruction> AdditiveDouble(CodeInstruction who, string buffId, double bonus)
        => new()
        {
            who.Clone(),
            new CodeInstruction(OpCodes.Ldstr, buffId),
            new CodeInstruction(OpCodes.Ldc_R8, bonus),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatuePatch), nameof(DwarfBuffBonus)))
        };

    // For `? mult : 1.0` / `*= mult`: pushes mult^count (double)
    private static List<CodeInstruction> MultiplicativeDouble(CodeInstruction who, string buffId, double mult)
        => new()
        {
            who.Clone(),
            new CodeInstruction(OpCodes.Ldstr, buffId),
            new CodeInstruction(OpCodes.Ldc_R8, mult),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatuePatch), nameof(DwarfBuffMultiplier)))
        };

    // For `num++` (replaces ldc.i4.1): pushes count (int)
    private static List<CodeInstruction> CountInt(CodeInstruction who, string buffId)
        => new()
        {
            who.Clone(),
            new CodeInstruction(OpCodes.Ldstr, buffId),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatuePatch), nameof(DwarfBuffCount)))
        };

    // For `critChance += 0.1f` (replaces ldc.r4): pushes count × bonus (float)
    private static List<CodeInstruction> AdditiveFloat(CodeInstruction who, string buffId, float bonus)
        => new()
        {
            who.Clone(),
            new CodeInstruction(OpCodes.Ldstr, buffId),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatuePatch), nameof(BlessingBuffCount))),
            new CodeInstruction(OpCodes.Conv_R4),
            new CodeInstruction(OpCodes.Ldc_R4, bonus),
            new CodeInstruction(OpCodes.Mul)
        };

    // For `amount = 60` (replaces ldc.i4 60): pushes 60 × count (int)
    private static List<CodeInstruction> IntMultiply(CodeInstruction who, string buffId, int baseVal)
        => new()
        {
            new CodeInstruction(OpCodes.Ldc_I4, baseVal),
            who.Clone(),
            new CodeInstruction(OpCodes.Ldstr, buffId),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatuePatch), nameof(BlessingBuffCount))),
            new CodeInstruction(OpCodes.Mul)
        };

    // ── Statue of the Dwarf King ─────────────────────────────────────────────

    [HarmonyPatch(typeof(SObject), nameof(SObject.checkForAction))]
    internal static class DwarfKingStatuePatch
    {
        [HarmonyPrefix]
        static bool Prefix(SObject __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (!Config.EnableStatueStacking) return true;
            if (__instance.QualifiedItemId != "(BC)StatueOfTheDwarfKing") return true;
            if (justCheckingForActivity) return true;
            if (who.stats.Get(StatKeys.Mastery(3)) < 1) return true;

            string perPlayerKey = PerPlayerKey(__instance);
            string today = Game1.stats.DaysPlayed.ToString();

            // Already picked today → shake and block.
            if (who.modData.TryGetValue(perPlayerKey, out string? usedDay) && usedDay == today)
            {
                __instance.shakeTimer = 400;
                Game1.playSound("cancel");
                __result = true;
                return false;
            }

            // Reuse the stored offer for today so closing and reopening shows the same pair.
            string offerDayKey = $"{perPlayerKey}.offerDay";
            string offer0Key   = $"{perPlayerKey}.offer0";
            string offer1Key   = $"{perPlayerKey}.offer1";
            int idx0 = 0, idx1 = 1;

            bool hasStoredOffer =
                who.modData.TryGetValue(offerDayKey, out string? offerDay) && offerDay == today &&
                who.modData.TryGetValue(offer0Key, out string? s0) && int.TryParse(s0, out idx0) &&
                who.modData.TryGetValue(offer1Key, out string? s1) && int.TryParse(s1, out idx1);

            if (!hasStoredOffer)
            {
                // Derive a fresh pair — either seeded to the day or fully random.
                Random rng = Config.DwarfRandomizeOffering
                    ? Game1.random
                    : Utility.CreateRandom(
                        Game1.stats.DaysPlayed * 77,
                        Game1.uniqueIDForThisGame,
                        __instance.TileLocation.X * 100 + __instance.TileLocation.Y);

                idx0 = rng.Next(5);
                // Re-roll bomb immunity (idx 3) if already active — no benefit to stack.
                for (int a = 0; a < 4 && idx0 == 3 && who.hasBuff("dwarfStatue_3"); a++)
                    idx0 = rng.Next(5);

                int att = 0;
                do { idx1 = rng.Next(5); att++; }
                while ((idx1 == idx0 || (idx1 == 3 && who.hasBuff("dwarfStatue_3"))) && att < 10);
                if (idx1 == idx0 || (idx1 == 3 && who.hasBuff("dwarfStatue_3")))
                {
                    for (int k = 0; k < 5; k++)
                    {
                        if (k != idx0 && !(k == 3 && who.hasBuff("dwarfStatue_3")))
                        {
                            idx1 = k;
                            break;
                        }
                    }
                }

                // Persist so re-opening the statue shows the same pair until a choice is made.
                who.modData[offerDayKey] = today;
                who.modData[offer0Key]   = idx0.ToString();
                who.modData[offer1Key]   = idx1.ToString();
            }

            if (Config.DwarfAutoPick)
            {
                int pick = Config.DwarfBlessingRank(idx0) <= Config.DwarfBlessingRank(idx1) ? idx0 : idx1;
                string autoBuffId = "dwarfStatue_" + pick;
                // Auto-pick commits the choice immediately — record usage and clear the offer.
                who.modData[perPlayerKey] = today;
                who.modData.Remove(offerDayKey);
                who.modData.Remove(offer0Key);
                who.modData.Remove(offer1Key);
                IncrementStatueBuffCount(who, autoBuffId);
                who.applyBuff(autoBuffId);
                UpdateStatueBuffDescription(who, autoBuffId);
                __result = true;
                return false;
            }

            Game1.activeClickableMenu = new ChooseFromIconsMenu("dwarfStatue");
            var menu = (ChooseFromIconsMenu)Game1.activeClickableMenu;
            menu.sourceObject = __instance;

            for (int i = 0; i < 2; i++)
            {
                int idx = (i == 0) ? idx0 : idx1;
                menu.icons[i].name = idx.ToString();
                menu.icons[i].hoverText = Game1.parseText(
                    Game1.content.LoadString("Strings\\1_6_Strings:DwarfStatue_" + idx),
                    Game1.smallFont, 208);
                var frontRect = new Rectangle(148 + idx * 17, 123, 17, 17);
                menu.iconFronts[i].sourceRect = frontRect;
                menu.iconFronts[i].startingSourceRect = frontRect;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ChooseFromIconsMenu), "doIconAction")]
    internal static class DwarfKingMenuPatch
    {
        [HarmonyPrefix]
        static bool Prefix(ChooseFromIconsMenu __instance, string iconName)
        {
            if (!Config.EnableStatueStacking) return true;
            if (ChooseFromIconsMenuWhichField is null)
            {
                Monitor?.LogOnce("[StackBuffs] ChooseFromIconsMenu.which field not found; Dwarf King stacking from the choice menu is inactive", LogLevel.Warn);
                return true;
            }
            if (ChooseFromIconsMenuWhichField.GetValue(__instance) as string != "dwarfStatue") return true;

            var who = Game1.player;
            var statue = __instance.sourceObject;
            string today = Game1.stats.DaysPlayed.ToString();

            // Record usage and clear the pending offer now that a choice is committed.
            if (statue != null)
            {
                string key = PerPlayerKey(statue);
                who.modData[key] = today;
                who.modData.Remove($"{key}.offerDay");
                who.modData.Remove($"{key}.offer0");
                who.modData.Remove($"{key}.offer1");
            }

            string buffId = "dwarfStatue_" + iconName;
            IncrementStatueBuffCount(who, buffId);
            who.applyBuff(buffId);
            UpdateStatueBuffDescription(who, buffId);
            return false;
        }
    }

    // ── Statue of Blessings ──────────────────────────────────────────────────

    [HarmonyPatch(typeof(SObject), "CheckForActionOnBlessedStatue")]
    internal static class BlessingsStatuePatch
    {
        [HarmonyPrefix]
        static bool Prefix(SObject __instance, Farmer who, GameLocation location, bool justCheckingForActivitiy, ref bool __result)
        {
            if (!Config.EnableStatueStacking) return true;
            if (justCheckingForActivitiy) return true;
            if (who.stats.Get(StatKeys.Mastery(0)) < 1) return true;

            string perPlayerKey = PerPlayerKey(__instance);
            if (who.modData.TryGetValue(perPlayerKey, out string? usedDay)
                && usedDay == Game1.stats.DaysPlayed.ToString())
            {
                __instance.shakeTimer = 400;
                Game1.playSound("cancel");
                __result = false;
                return false;
            }

            who.modData[perPlayerKey] = Game1.stats.DaysPlayed.ToString();

            // Per-statue seed: different statues apply different blessings.
            Random random = Utility.CreateDaySaveRandom(
                Game1.stats.DaysPlayed * 777,
                __instance.TileLocation.X * 100 + __instance.TileLocation.Y);
            for (int i = 0; i < 8; i++) random.Next();
            int maxRange = (Game1.isRaining || Utility.isFestivalDay()) ? 6 : 7;
            string buffId = "statue_of_blessings_" + random.Next(maxRange);
            // Re-roll unlimited stamina (non-stackable) if already active.
            // Prismatic butterfly (_6) is stackable (multiple butterflies spawn), so no re-roll for it.
            for (int a = 0; a < 7 && buffId == "statue_of_blessings_2" && who.hasBuff(buffId); a++)
                buffId = "statue_of_blessings_" + random.Next(maxRange);
            IncrementStatueBuffCount(who, buffId);
            who.applyBuff(buffId);
            UpdateStatueBuffDescription(who, buffId);
            Game1.playSound("statue_of_blessings");
            __instance.showNextIndex.Value = true;

            if (location.critters == null)
                location.critters = new List<Critter>();
            location.critters.Add(new Butterfly(location, __instance.TileLocation + new Vector2(1f, 0f), islandButterfly: false, forceSummerButterfly: false, 163));
            location.critters.Add(new Butterfly(location, __instance.TileLocation + new Vector2(0.33f, 0.25f), islandButterfly: false, forceSummerButterfly: false, 163));
            location.critters.Add(new Butterfly(location, __instance.TileLocation + new Vector2(1.58f, 0.25f), islandButterfly: false, forceSummerButterfly: false, 163));
            location.temporarySprites.Add(new TemporaryAnimatedSprite(
                "LooseSprites\\Cursors_1_6",
                new Rectangle(221, 225, 15, 31),
                9000f, 1, 1,
                __instance.TileLocation * 64f + new Vector2(1f, -16f) * 4f,
                flicker: false, flipped: false,
                Math.Max(0f, ((__instance.TileLocation.Y + 1f) * 64f - 20f) / 10000f) + __instance.TileLocation.X * 1E-05f,
                0.02f, Color.White, 4f, 0f, 0f, 0f));
            for (int j = 0; j < 6; j++)
            {
                Utility.makeTemporarySpriteJuicier(new TemporaryAnimatedSprite(
                    "LooseSprites\\Cursors_1_6",
                    new Rectangle(144, 249, 7, 7),
                    Game1.random.Next(100, 200), 6, 1,
                    __instance.TileLocation * 64f + new Vector2(32 + Game1.random.Next(-64, 64), Game1.random.Next(-64, 64)),
                    flicker: false, flipped: false,
                    Math.Max(0f, ((__instance.TileLocation.Y + 1f) * 64f - 24f) / 10000f) + __instance.TileLocation.X * 1E-05f,
                    0f,
                    (Game1.random.NextDouble() < 0.5) ? new Color(255, 180, 210) : Color.White,
                    4f, 0f, 0f, 0f), location);
            }

            __result = true;
            return false;
        }
    }

    // Allows multiple prismatic butterflies when statue_of_blessings_6 is stacked.
    // The vanilla method returns early as soon as one prismatic butterfly exists in the location.
    // We replace that "already has one" gate with "already has N" where N = stack count.
    [HarmonyPatch(typeof(GameLocation), "tryAddPrismaticButterfly")]
    internal static class PrismaticButterflyPatch
    {
        [HarmonyPrefix]
        static bool Prefix(GameLocation __instance)
        {
            if (!Config.EnableStatueStacking) return true;

            int count = BlessingBuffCount(Game1.player, "statue_of_blessings_6");
            if (count <= 1) return true; // 0 or 1 stack: let vanilla handle it

            if (!Game1.player.hasBuff("statue_of_blessings_6")) return false;

            int existing = __instance.critters?.Count(c => c is Butterfly b && b.isPrismatic) ?? 0;
            if (existing >= count) return false;

            // Same location selection as vanilla so all butterflies target the same map today.
            var locationRng = Utility.CreateDaySaveRandom(Game1.player.UniqueMultiplayerID % 10000);
            string[] maps = { "Forest", "Town", "Beach", "Mountain", "Woods", "BusStop", "Backwoods" };
            string target = maps[locationRng.Next(maps.Length)];
            if (target == "Beach" && __instance.Name == "BeachNightMarket") target = "BeachNightMarket";
            if (!__instance.Name.Equals(target)) return false;

            // Spawn all butterflies that should be present but aren't yet.
            // tryAddPrismaticButterfly is called once on location entry, so we must
            // add all N butterflies in this single call rather than one per invocation.
            if (__instance.critters == null) __instance.critters = new List<Critter>();
            while (existing < count)
            {
                var tileRng = Utility.CreateDaySaveRandom(
                    Game1.player.UniqueMultiplayerID % 10000 + (existing + 1) * 100003L);
                Vector2 tile = __instance.getRandomTile(tileRng);
                for (int i = 0; i < 32; i++)
                {
                    if (__instance.isTileLocationOpen(tile)) break;
                    tile = __instance.getRandomTile(tileRng);
                }
                __instance.critters.Add(
                    new Butterfly(__instance, tile, islandButterfly: false, forceSummerButterfly: false, 394, prismatic: true)
                    {
                        stayInbounds = true
                    });
                existing++;
            }
            return false;
        }
    }

    // Replaces the flat buffs.Remove("statue_of_blessings_6") call in Butterfly.update
    // with a decrement so catching one butterfly only consumes one stack.
    [HarmonyPatch(typeof(Butterfly), "update")]
    internal static class ButterflyCapturePatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var removeMethod = AccessTools.Method(typeof(BuffManager), nameof(BuffManager.Remove), new[] { typeof(string) });

            for (int i = 2; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr
                    && (string)codes[i].operand == "statue_of_blessings_6"
                    && codes[i + 1].Calls(removeMethod))
                {
                    // The 4-instruction sequence: ldsfld/call player, ldfld/callvirt buffs, ldstr, callvirt Remove
                    bool patternValid = i >= 2
                        && (codes[i - 2].opcode == OpCodes.Ldsfld || codes[i - 2].opcode == OpCodes.Call)
                        && (codes[i - 1].opcode == OpCodes.Ldfld  || codes[i - 1].opcode == OpCodes.Callvirt);
                    if (!patternValid)
                    {
                        Monitor?.Log("[StackBuffs] ButterflyCapturePatch: unexpected IL layout — butterfly stack decrement inactive", LogLevel.Warn);
                        break;
                    }
                    var newCall = new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(StatuePatch), nameof(DecrementButterflyStack)));
                    newCall.labels.AddRange(codes[i - 2].labels);
                    codes.RemoveRange(i - 2, 4);
                    codes.Insert(i - 2, newCall);
                    break;
                }
            }
            return codes;
        }
    }

    // ── Dwarf King stacking transpilers ──────────────────────────────────────
    //
    // Strategy: find `ldstr "buffId"` + `callvirt hasBuff`, then replace the
    // constant value load at (ldstr_pos + offset) with a call to our helper that
    // scales the value by the number of times that buff has been applied today.
    //
    // Offset +3 = ternary pattern:   callvirt[+1] brfalse[+2] VALUE[+3]
    // Offset +4 = if-block pattern:  callvirt[+1] brfalse[+2] ldloc[+3] VALUE[+4]

    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.OnStoneDestroyed))]
    internal static class OnStonePatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // L14833: dwarfStatue_4 ? 1.25 : 1.0  (ternary, offset +3)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_4", 3, w => MultiplicativeDouble(w, "dwarfStatue_4", DwarfGeodeChanceMultiplier)),
                "dwarfStatue_4", "GameLocation.OnStoneDestroyed");
            // L14839: dwarfStatue_2 ? 0.03 : 0.0  (ternary, offset +3)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_2", 3, w => AdditiveDouble(w, "dwarfStatue_2", DwarfCoalChanceOnStoneDestroyed)),
                "dwarfStatue_2", "GameLocation.OnStoneDestroyed");
            return codes;
        }
    }

    [HarmonyPatch(typeof(GameLocation), "breakStone")]
    internal static class BreakStonePatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // L14861: if (who != null && hasBuff("dwarfStatue_0")) { num2++; }  (if-block, ldc.i4.1 at offset +4)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_0", 4, w => CountInt(w, "dwarfStatue_0")),
                "dwarfStatue_0", "GameLocation.breakStone");
            // L15072: if (who.hasBuff("dwarfStatue_2")) { num8 += 0.025; }  (if-block, ldc.r8 at offset +4)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_2", 4, w => AdditiveDouble(w, "dwarfStatue_2", DwarfCoalChanceOnBreakStone)),
                "dwarfStatue_2", "GameLocation.breakStone");
            return codes;
        }

        // Several node types never use addedOres in vanilla (gem nodes, mussel nodes, calico egg
        // nodes). This postfix drops the stacked extra count for each when their option is enabled.
        // Note: stone "44" is a random-gem node — vanilla re-rolls its ID internally before the
        // switch, so the postfix only sees "44". We mirror vanilla's 1-in-7 re-roll for the extras.
        [HarmonyPostfix]
        static void Postfix(string stoneId, int x, int y, Farmer who, GameLocation __instance)
        {
            if (!Config.EnableStatueStacking) return;
            int count = DwarfBuffCount(who, "dwarfStatue_0");
            if (count == 0) return;

            switch (stoneId)
            {
                case "CalicoEggStone_0":
                case "CalicoEggStone_1":
                case "CalicoEggStone_2":
                    if (Config.DwarfCalicoEggScaling)
                        Game1.createMultipleObjectDebris("CalicoEgg", x, y, count, who.UniqueMultiplayerID, __instance);
                    break;

                case "2":  if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)72", x, y, count, who.UniqueMultiplayerID, __instance); break; // Diamond
                case "4":  if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)64", x, y, count, who.UniqueMultiplayerID, __instance); break; // Ruby
                case "6":  if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)70", x, y, count, who.UniqueMultiplayerID, __instance); break; // Jade
                case "8":  if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)66", x, y, count, who.UniqueMultiplayerID, __instance); break; // Amethyst
                case "10": if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)68", x, y, count, who.UniqueMultiplayerID, __instance); break; // Topaz
                case "12": if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)60", x, y, count, who.UniqueMultiplayerID, __instance); break; // Emerald
                case "14": if (Config.DwarfGemNodeScaling) Game1.createMultipleObjectDebris("(O)62", x, y, count, who.UniqueMultiplayerID, __instance); break; // Aquamarine

                case "44": // Random gem node — re-roll each extra independently, matching vanilla distribution
                    if (Config.DwarfGemNodeScaling)
                    {
                        string[] gemIds = { "(O)72", "(O)64", "(O)70", "(O)66", "(O)68", "(O)60", "(O)62" };
                        for (int i = 0; i < count; i++)
                            Game1.createObjectDebris(gemIds[Game1.random.Next(gemIds.Length)], x, y, who.UniqueMultiplayerID, __instance);
                    }
                    break;

                case "25": // Mussel
                    if (Config.DwarfMusselNodeScaling)
                        Game1.createMultipleObjectDebris("(O)719", x, y, count, who.UniqueMultiplayerID, __instance);
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(MineShaft), nameof(MineShaft.monsterDrop))]
    internal static class MonsterDropPatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // L2071: dwarfStatue_1 ? 0.07 : 0.0  (ternary, offset +3)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_1", 3, w => AdditiveDouble(w, "dwarfStatue_1", DwarfLadderChanceFromMonsters)),
                "dwarfStatue_1", "MineShaft.monsterDrop");
            return codes;
        }
    }

    [HarmonyPatch(typeof(MineShaft), "checkStoneForItems")]
    internal static class CheckStonePatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // L3630: if (who != null && hasBuff("dwarfStatue_1")) { num6 *= 1.25; }  (if-block, ldc.r8 at offset +4)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_1", 4, w => MultiplicativeDouble(w, "dwarfStatue_1", DwarfLadderChanceMultiplier)),
                "dwarfStatue_1", "MineShaft.checkStoneForItems");
            // L3671: dwarfStatue_4 ? 1.25 : 1.0  (ternary, offset +3)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_4", 3, w => MultiplicativeDouble(w, "dwarfStatue_4", DwarfGeodeChanceMultiplier)),
                "dwarfStatue_4", "MineShaft.checkStoneForItems");
            // L3698: dwarfStatue_2 ? 0.1 : 0.0  (ternary, offset +3)
            WarnIfNotFound(ReplaceFirstBuffValue(codes, "dwarfStatue_2", 3, w => AdditiveDouble(w, "dwarfStatue_2", DwarfCoalChanceInMines)),
                "dwarfStatue_2", "MineShaft.checkStoneForItems");
            return codes;
        }
    }

    // ── Blessing stacking transpilers ────────────────────────────────────────

    [HarmonyPatch(typeof(GameLocation), "damageMonster",
        new Type[]
        {
            typeof(Rectangle), typeof(int), typeof(int), typeof(bool),
            typeof(float), typeof(int), typeof(float), typeof(float),
            typeof(bool), typeof(Farmer), typeof(bool)
        })]
    internal static class DamageMonsterPatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // L4683: if (who.hasBuff("statue_of_blessings_5")) { critChance += 0.1f; }
            // critChance is a float arg; ldc.r4 0.1f is at offset +4 after ldstr
            if (!ReplaceFirstBuffValue(codes, "statue_of_blessings_5", 4,
                w => AdditiveFloat(w, "statue_of_blessings_5", BlessingCritChanceBonus)))
                Monitor?.Log("[StackBuffs] statue_of_blessings_5 crit pattern not found in GameLocation.damageMonster — crit stacking inactive", StardewModdingAPI.LogLevel.Warn);
            return codes;
        }
    }

    [HarmonyPatch(typeof(NPC), nameof(NPC.grantConversationFriendship))]
    internal static class NpcGrantFriendshipPatch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // L2832: if (who.hasBuff("statue_of_blessings_4")) { amount = 60; }
            // ldc.i4.s 60 is at offset +3; replace with 60 * count
            if (!ReplaceFirstBuffValue(codes, "statue_of_blessings_4", 3,
                w => IntMultiply(w, "statue_of_blessings_4", BlessingConversationFriendship)))
                Monitor?.Log("[StackBuffs] statue_of_blessings_4 friendship pattern not found in NPC.grantConversationFriendship — friendship stacking inactive", StardewModdingAPI.LogLevel.Warn);
            return codes;
        }
    }
}
