// Patch_RICS_BillCountOwned.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Bill details "Count owned". Off (default) leaves RICS-owned weapons and clothes
// out of "do until you have X", including Everybody Gets One per-person totals.
// That mod reuses Dialog_BillConfig and RecipeWorkerCounter, so this patch covers it
// without a reference to its assembly.
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace CAP_ChatInteractive.Ownership.Harmony
{
    /// <summary>
    /// Per-bill "count owned items" flag for weapon and apparel target-count bills.
    /// Missing entry means false: owned items are not counted.
    /// </summary>
    public static class RICS_BillCountOwned
    {
        private static readonly Dictionary<Bill_Production, bool> CountOwnedByBill = new Dictionary<Bill_Production, bool>();

        /// <summary>Listing checkbox buffer. The flag is not a field on <see cref="Bill_Production"/>.</summary>
        private static bool checkboxBuffer;

        /// <summary>True when this bill should count weapons and clothes that already have an owner.</summary>
        public static bool Get(Bill_Production bill)
        {
            if (bill == null)
                return false;
            return CountOwnedByBill.TryGetValue(bill, out bool value) && value;
        }

        /// <summary>Stores the flag. False removes the entry so the default stays off.</summary>
        public static void Set(Bill_Production bill, bool value)
        {
            if (bill == null)
                return;
            if (value)
                CountOwnedByBill[bill] = true;
            else
                CountOwnedByBill.Remove(bill);
        }

        public static void Forget(Bill bill)
        {
            if (bill is Bill_Production production)
                CountOwnedByBill.Remove(production);
        }

        /// <summary>Drop flags from the previous colony. Bill objects do not survive a load.</summary>
        public static void Clear() => CountOwnedByBill.Clear();

        /// <summary>
        /// Draws Count owned under Count equipped. No-op when RICS ownership is off,
        /// so the row only appears for players using ownership.
        /// </summary>
        public static void DrawCheckbox(Listing_Standard listing, Bill_Production bill)
        {
            if (listing == null || bill == null)
                return;
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return;

            try
            {
                checkboxBuffer = Get(bill);
                listing.CheckboxLabeled(
                    "RICS.Ownership.Bill.CountOwned".Translate(),
                    ref checkboxBuffer,
                    "RICS.Ownership.Bill.CountOwnedTip".Translate());
                Set(bill, checkboxBuffer);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Count owned checkbox failed: {ex.Message}");
            }
        }
    }

    /// <summary>Saves the flag on the bill. Unfinished-thing bills call this through base.ExposeData.</summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ExposeData))]
    public static class Patch_RICS_BillCountOwned_Expose
    {
        [HarmonyPostfix]
        public static void Postfix(Bill_Production __instance)
        {
            if (__instance == null)
                return;

            try
            {
                bool value = RICS_BillCountOwned.Get(__instance);
                Scribe_Values.Look(ref value, "ricsCountOwned", false);
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                    RICS_BillCountOwned.Set(__instance, value);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Could not save Count owned on a bill: {ex.Message}");
            }
        }
    }

    /// <summary>Copy and paste keep the checkbox.</summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Clone))]
    public static class Patch_RICS_BillCountOwned_Clone
    {
        [HarmonyPostfix]
        public static void Postfix(Bill_Production __instance, Bill __result)
        {
            if (__instance == null || !(__result is Bill_Production copy))
                return;
            RICS_BillCountOwned.Set(copy, RICS_BillCountOwned.Get(__instance));
        }
    }

    [HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.Notify_BillDeleted))]
    public static class Patch_RICS_BillCountOwned_Delete
    {
        [HarmonyPostfix]
        public static void Postfix(Bill bill)
        {
            RICS_BillCountOwned.Forget(bill);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Patch_RICS_BillCountOwned_LoadGame
    {
        [HarmonyPrefix]
        public static void Prefix() => RICS_BillCountOwned.Clear();
    }

    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class Patch_RICS_BillCountOwned_NewGame
    {
        [HarmonyPrefix]
        public static void Prefix() => RICS_BillCountOwned.Clear();
    }

    /// <summary>
    /// Drops owned weapons and clothes from the bill product count when Count owned is off.
    /// Stockpiles, carried items, minified items, and equipped gear all use this method.
    /// </summary>
    [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountValidThing))]
    public static class Patch_RICS_BillCountOwned_Count
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Thing thing, Bill_Production bill)
        {
            if (!__result || thing == null || bill == null)
                return;

            try
            {
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return;
                if (RICS_BillCountOwned.Get(bill))
                    return;
                if (RICS_OwnershipUtility.GetOwner(thing) != null)
                    __result = false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Count owned filter failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Inserts Count owned immediately after Count equipped, still inside that weapon/apparel block.
    /// Does not touch the repeat-mode compare Everybody Gets One rewrites.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
    public static class Patch_RICS_BillConfig_CountOwned
    {
        /// <summary>One checkbox row. The details section is a fixed height and would otherwise cover the next box.</summary>
        private const int ExtraSectionHeight = 30;

        static Patch_RICS_BillConfig_CountOwned()
        {
            try
            {
                FieldInfo field = AccessTools.Field(typeof(Dialog_BillConfig), "RepeatModeSubdialogHeight");
                if (field == null)
                {
                    Logger.Warning("[RICS Ownership] Bill details height field was not found.");
                    return;
                }

                int current = (int)field.GetValue(null);
                field.SetValue(null, current + ExtraSectionHeight);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Could not grow bill details: {ex.Message}");
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            try
            {
                if (!TryInsertCheckbox(list))
                    Logger.Warning("[RICS Ownership] Could not add Count owned to the bill details window.");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Bill details patch failed: {ex.Message}");
            }

            return list;
        }

        private static bool TryInsertCheckbox(List<CodeInstruction> list)
        {
            MethodInfo checkbox = AccessTools.Method(
                typeof(Listing_Standard),
                nameof(Listing_Standard.CheckboxLabeled),
                new[] { typeof(string), typeof(bool).MakeByRefType(), typeof(string), typeof(float), typeof(float) });
            FieldInfo includeEquipped = AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.includeEquipped));
            FieldInfo billField = AccessTools.Field(typeof(Dialog_BillConfig), "bill");
            MethodInfo draw = AccessTools.Method(typeof(RICS_BillCountOwned), nameof(RICS_BillCountOwned.DrawCheckbox));
            if (checkbox == null || includeEquipped == null || billField == null || draw == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Calls(checkbox))
                    continue;

                bool equipped = false;
                CodeInstruction listingLoad = null;
                for (int j = i - 1; j >= 0 && i - j <= 16; j--)
                {
                    if (list[j].opcode == OpCodes.Ldflda && list[j].operand is FieldInfo field && field == includeEquipped)
                        equipped = true;
                    if (listingLoad == null && IsLdloc(list[j]))
                        listingLoad = list[j];
                }

                if (!equipped || listingLoad == null)
                    continue;

                list.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(listingLoad.opcode, listingLoad.operand),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, billField),
                    new CodeInstruction(OpCodes.Call, draw)
                });
                return true;
            }

            return false;
        }

        private static bool IsLdloc(CodeInstruction instruction)
        {
            OpCode op = instruction.opcode;
            return op == OpCodes.Ldloc || op == OpCodes.Ldloc_S
                || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1
                || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3;
        }
    }
}
