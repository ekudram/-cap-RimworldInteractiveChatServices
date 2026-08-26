// File: StoreCommandHelper.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Helper methods for store command handling
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Logger = CAP_ChatInteractive.Logger;

namespace _CAP__Chat_Interactive.Command.CommandHelpers
{
    public static class StoreCommandHelper
    {
        public static StoreItem GetStoreItemByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return null;

            // Clean the item name
            string cleanItemName = itemName.Trim();
            cleanItemName = cleanItemName.TrimEnd('(', '[', '{').TrimStart(')', ']', '}').Trim();
            // Check if this is a banned race first
            if (IsRaceBannedByName(cleanItemName))
            {
                return null;
            }
            // Try exact matches first
            var exactMatch = StoreInventory.AllStoreItems.Values
                .FirstOrDefault(item =>
                    item.DefName.Equals(cleanItemName, StringComparison.OrdinalIgnoreCase) ||
                    item.CustomName?.Equals(cleanItemName, StringComparison.OrdinalIgnoreCase) == true);

            if (exactMatch != null)
            {
                return exactMatch;
            }

            // Try partial match on thingDef label (case insensitive, whole word)
            var thingDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def =>
                    def.label != null &&
                    def.label.Equals(cleanItemName, StringComparison.OrdinalIgnoreCase));

            if (thingDef != null)
            {
                return StoreInventory.GetStoreItem(thingDef.defName);
            }

            // Try label without spaces
            thingDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def =>
                {
                    if (def.label == null) return false;

                    string labelWithoutSpaces = def.label.Replace(" ", "");
                    return labelWithoutSpaces.Equals(cleanItemName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
                });

            if (thingDef != null)
            {
                return StoreInventory.GetStoreItem(thingDef.defName);
            }

            // Try contains match as last resort, but only if we have at least 3 characters
            if (cleanItemName.Length >= 3)
            {
                thingDef = DefDatabase<ThingDef>.AllDefs
                    .FirstOrDefault(def => def.label?.ToLower().Contains(cleanItemName.ToLower()) == true);

                if (thingDef != null)
                {
                    return StoreInventory.GetStoreItem(thingDef.defName);
                }
            }
            return null;
        }

        public struct ResearchGateResult
        {
            public readonly bool Allowed;
            public readonly string BlockingResearchLabel; // LabelCap of the first missing research (or joined list if multiple)

            public ResearchGateResult(bool allowed, string blockingLabel = null)
            {
                Allowed = allowed;
                BlockingResearchLabel = blockingLabel;
            }
        }

        public static bool CanUserAfford(ChatMessageWrapper user, int price)
        {
            if (user == null || price <= 0)
                return price <= 0;

            var viewer = Viewers.GetViewer(user);
            return viewer != null && viewer.Coins >= price;
        }

        /// <summary>
        /// Research gate for store purchases. Requires at least one unlocked craft/build path
        /// when RequireResearch is on. Unique weapons gate via base weapon def when possible.
        /// </summary>
        public static ResearchGateResult HasRequiredResearch(StoreItem storeItem)
        {
            if (storeItem == null)
                return new ResearchGateResult(true);

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.RequireResearch)
            {
                return new ResearchGateResult(true);
            }

            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (thingDef == null)
            {
                return new ResearchGateResult(true);
            }

            ThingDef gateDef = thingDef;
            if (StoreItem.IsUniqueWeapon(thingDef))
            {
                if (TryResolveBaseWeaponForUnique(thingDef, out ThingDef baseWeapon) && baseWeapon != null)
                {
                    gateDef = baseWeapon;
                }
                else
                {
                    return new ResearchGateResult(true);
                }
            }

            return HasRequiredResearchForThingDef(gateDef, storeItem.DefName);
        }

        /// <summary>
        /// Resolve base weapon for a unique (traits) weapon.
        /// 1) DefName ending in _Unique → strip suffix.
        /// 2) Label match against non-unique weapons.
        /// </summary>
        public static bool TryResolveBaseWeaponForUnique(ThingDef uniqueDef, out ThingDef baseDef)
        {
            baseDef = null;
            if (uniqueDef == null)
                return false;

            // Primary: Gun_AssaultRifle_Unique → Gun_AssaultRifle
            string dn = uniqueDef.defName ?? "";
            if (dn.EndsWith("_Unique", StringComparison.OrdinalIgnoreCase) && dn.Length > "_Unique".Length)
            {
                string baseName = dn.Substring(0, dn.Length - "_Unique".Length);
                var byName = DefDatabase<ThingDef>.GetNamedSilentFail(baseName);
                if (byName != null && !StoreItem.IsUniqueWeapon(byName))
                {
                    baseDef = byName;
                    return true;
                }
            }

            // Secondary: label contains base weapon label
            string uniqueLabel = NormalizeWeaponLabel(uniqueDef.label ?? uniqueDef.LabelCap.ToString());
            if (string.IsNullOrEmpty(uniqueLabel))
                return false;

            ThingDef best = null;
            int bestScore = 0;

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || def == uniqueDef)
                    continue;
                if (!def.IsWeapon)
                    continue;
                if (StoreItem.IsUniqueWeapon(def))
                    continue;

                string baseLabel = NormalizeWeaponLabel(def.label ?? def.LabelCap.ToString());
                if (string.IsNullOrEmpty(baseLabel) || baseLabel.Length < 3)
                    continue;

                int score = 0;
                if (uniqueLabel == baseLabel)
                    score = 1000 + baseLabel.Length;
                else if (uniqueLabel.Contains(baseLabel))
                    score = 100 + baseLabel.Length; // longer base phrase wins
                else if (baseLabel.Contains(uniqueLabel) && uniqueLabel.Length >= 5)
                    score = 10 + uniqueLabel.Length;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = def;
                }
            }

            if (best != null && bestScore >= 100)
            {
                baseDef = best;
                return true;
            }

            return false;
        }

        private static string NormalizeWeaponLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "";
            string s = label.Trim().ToLowerInvariant();
            // collapse whitespace
            while (s.Contains("  "))
                s = s.Replace("  ", " ");
            if (s.StartsWith("unique "))
                s = s.Substring("unique ".Length).Trim();
            return s;
        }

        /// <summary>Core research path check for a concrete ThingDef (base or normal item).</summary>
        public static ResearchGateResult HasRequiredResearchForThingDef(ThingDef thingDef, string logName = null)
        {
            if (thingDef == null)
                return new ResearchGateResult(true);

            string name = logName ?? thingDef.defName;

            // 1. Direct ThingDef prereqs (buildings, turrets, etc.)
            if (thingDef.researchPrerequisites != null && thingDef.researchPrerequisites.Count > 0)
            {
                foreach (var research in thingDef.researchPrerequisites)
                {
                    if (research != null && !research.IsFinished)
                    {
                        return new ResearchGateResult(false, research.LabelCap);
                    }
                }
                return new ResearchGateResult(true);
            }

            // 2. Craftables: recipeMaker prereq
            if (thingDef.recipeMaker != null && thingDef.recipeMaker.researchPrerequisite != null)
            {
                var req = thingDef.recipeMaker.researchPrerequisite;
                if (!req.IsFinished)
                {
                    return new ResearchGateResult(false, req.LabelCap);
                }
                return new ResearchGateResult(true);
            }

            // 3. Fallback: scan recipes for at least one valid crafting path
            bool foundAnyProducingRecipe = false;
            string firstBlockingResearch = null;

            foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                bool producesThis = recipe.ProducedThingDef == thingDef ||
                                   (recipe.products != null && recipe.products.Any(p => p.thingDef == thingDef));
                if (!producesThis) continue;

                foundAnyProducingRecipe = true;

                bool recipePrereqsMet = true;
                string thisRecipeBlocking = null;

                if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
                {
                    recipePrereqsMet = false;
                    thisRecipeBlocking = recipe.researchPrerequisite.LabelCap;
                }

                if (recipePrereqsMet && recipe.researchPrerequisites != null)
                {
                    foreach (var prereq in recipe.researchPrerequisites)
                    {
                        if (prereq != null && !prereq.IsFinished)
                        {
                            recipePrereqsMet = false;
                            thisRecipeBlocking = prereq.LabelCap;
                            break;
                        }
                    }
                }

                if (!recipePrereqsMet)
                {
                    if (firstBlockingResearch == null)
                        firstBlockingResearch = thisRecipeBlocking;
                    continue;
                }

                bool hasValidBench = false;
                if (recipe.recipeUsers == null || recipe.recipeUsers.Count == 0)
                {
                    hasValidBench = true;
                }
                else
                {
                    foreach (var userDef in recipe.recipeUsers)
                    {
                        if (userDef == null) continue;

                        bool benchPrereqsMet = true;
                        if (userDef.researchPrerequisites != null)
                        {
                            foreach (var benchPrereq in userDef.researchPrerequisites)
                            {
                                if (benchPrereq != null && !benchPrereq.IsFinished)
                                {
                                    benchPrereqsMet = false;
                                    if (firstBlockingResearch == null)
                                        firstBlockingResearch = benchPrereq.LabelCap;
                                    break;
                                }
                            }
                        }

                        if (benchPrereqsMet)
                        {
                            hasValidBench = true;
                            break;
                        }
                    }
                }

                if (hasValidBench)
                {
                    return new ResearchGateResult(true);
                }
            }

            if (foundAnyProducingRecipe)
            {
                if (firstBlockingResearch != null)
                {
                    return new ResearchGateResult(false, firstBlockingResearch);
                }
                return new ResearchGateResult(false);
            }
            return new ResearchGateResult(true);
        }

        public static bool IsItemTypeValid(StoreItem storeItem, bool requireEquippable, bool requireWearable, bool requireUsable)
        {
            if (storeItem == null)
                return false;

            if (requireEquippable && !storeItem.IsEquippable)
                return false;

            if (requireWearable && !storeItem.IsWearable)
                return false;

            if (requireUsable && !storeItem.IsUsable)
                return false;

            return true;
        }

        public static string GetItemTypeDescription(StoreItem storeItem)
        {
            if (storeItem == null)
                return "item";

            if (storeItem.IsEquippable) return "equippable";
            if (storeItem.IsWearable) return "wearable";
            if (storeItem.IsUsable) return "usable";
            return "item";
        }

        public static bool IsRaceBanned(ThingDef thingDef)
        {
            if (thingDef?.race == null)
                return false;

            // Ban humanlike races
            if (thingDef.race.Humanlike)
            {
                return true;
            }

            // Add other banned race conditions here if needed
            string[] bannedRaces = {
        "Human", "Colonist", "Slave", "Refugee", "Prisoner",
        "Spacer", "Tribal", "Pirate", "Outlander", "Villager"
    };

            if (bannedRaces.Any(race => thingDef.defName.Contains(race) ||
                                       (thingDef.label?.Contains(race) == true)))
            {
                return true;
            }

            return false;
        }

        public static bool IsRaceBannedByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            // Clean the item name first (using the same logic as GetStoreItemByName)
            string cleanItemName = itemName.Trim();
            cleanItemName = cleanItemName.TrimEnd('(', '[', '{').TrimStart(')', ']', '}').Trim();

            // Try to find if this matches any humanlike race
            var raceDef = RaceUtils.FindRaceByName(cleanItemName);
            if (raceDef != null)
            {
                return true;
            }

            return false;
        }

        public static string FormatCurrencyMessage(int amount, string currencySymbol)
        {
            string symbol = string.IsNullOrEmpty(currencySymbol) ? "¢" : currencySymbol;
            return $"{amount:N0} {symbol}";
        }
    }
}