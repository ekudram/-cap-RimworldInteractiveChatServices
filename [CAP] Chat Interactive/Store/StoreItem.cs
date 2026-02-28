// StoreItem.cs 
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
// Represents an item available in the chat interactive store
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Store
{
    public class StoreItem
    {
        public string DefName { get; set; }
        public string CustomName { get; set; }
        public int BasePrice { get; set; }
        public bool HasQuantityLimit { get; set; } = true;
        public int QuantityLimit { get; set; } = 1;
        public QuantityLimitMode LimitMode { get; set; } = QuantityLimitMode.OneStack; // Changed from Each to OneStack
        public bool IsUsable { get; set; } = true;
        public bool IsEquippable { get; set; }
        public bool IsWearable { get; set; }
        // public bool IsWeapon { get; set; }  // Not needed for store json 4 ref but we can really just pull this from def
        // public bool IsMelee { get; set; }// Not needed for store json
        // public bool IsRanged { get; set; }// Not needed for store json
        // public bool IsStuffAllowed { get; set; } // Not needed for store json
        // public List<string> ResearchOverrides { get; set; }// Not needed for store json
        public string Category { get; set; }
        public string ModSource { get; set; }
        public bool modactive { get; set; } = false;
        public bool Enabled { get; set; } = true;

        // REMOVE the entire ExposeData method

        public StoreItem() { }

        public StoreItem(ThingDef thingDef)
        {
            DefName = thingDef.defName;
            CustomName = thingDef.label.CapitalizeFirst();
            // CustomName = thingDef.label.CapitalizeFirst().Replace("(", "").Replace(")", "");
            BasePrice = CalculateBasePrice(thingDef);
            Category = GetCategoryFromThingDef(thingDef) ?? "Uncategorized";  // Handle null here
            ModSource = thingDef.modContentPack?.Name ?? "RimWorld";

            // Set default properties based on thing type - IMPROVED LOGIC
            // IsWeapon = thingDef.IsWeapon;
            // IsMelee = thingDef.IsMeleeWeapon;
            // IsRanged = thingDef.IsRangedWeapon;
            IsUsable = IsItemUsable(thingDef);
            IsEquippable = !IsUsable && thingDef.IsWeapon;
            IsWearable = !IsUsable && !IsEquippable && IsItemWearable(thingDef);
            // IsStuffAllowed = thingDef.IsStuff;

            // FIX: Set default quantity limit to 1 stack instead of 0
            HasQuantityLimit = true;
            int baseStack = Mathf.Max(1, thingDef.stackLimit);
            QuantityLimit = baseStack;
            LimitMode = QuantityLimitMode.OneStack;
        }
        public static bool IsItemUsable(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            // Explicitly exclude things that are clearly NOT consumable/usable
            // if (thingDef.IsWeapon) return false; // If you do this Beer and other items will not be useable
            if (thingDef.IsBuildingArtificial) return false;

            // NEW: Apparel is NEVER usable (wear instead). This fixes all DLC/modded belt packs consistently.
            // (Rare modded "apparel implants" with CompUsableImplant would still be caught by name/comp below if truly needed, but none exist in vanilla/DLC.)
            if (thingDef.IsApparel) return false;

            // Core usable categories
            if (thingDef.IsIngestible) return true;
            if (thingDef.IsMedicine) return true;
            if (thingDef.IsDrug || thingDef.IsPleasureDrug) return true;

            // Check for CompUsableImplant / CompUsable
            if (thingDef.HasComp<CompUsableImplant>()) return true;
            if (thingDef.HasComp<CompUsable>()) return true;

            // Name-based fallbacks (only as last resort — all are non-apparel)
            string defName = thingDef.defName?.ToLowerInvariant() ?? "";
            if (defName.Contains("neurotrainer") ||
                defName.Contains("psytrainer") ||
                defName.Contains("psychicamplifier") ||
                defName.Contains("serum"))
            {
                return true;
            }

            // Catch remaining non-apparel "Pack" items that are actually usable
            if (defName.Contains("pack") && !thingDef.IsApparel)
            {
                return true;
            }

            return false;
        }

        public static bool IsItemWearable(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            // All normal apparel = wearable
            if (thingDef.IsApparel) return true;

            // Special handling for DLC/modded utility packs that sometimes slip (Biotech/Anomaly/Odyssey etc.)
            // These are the exact ones you listed as "not being caught at all"
            string defNameLower = thingDef.defName?.ToLowerInvariant() ?? "";
            Logger.Debug($"Checking if '{thingDef.defName}' is wearable based on name for DLC/modded utility packs.");
            if (defNameLower.Contains("bandwidth") ||
                defNameLower.Contains("control") ||
                defNameLower.Contains("tox") ||
                defNameLower.Contains("disruptor") ||
                defNameLower.Contains("firefoam") ||
                defNameLower.Contains("hunter"))
            {
                return true;
            }

            return false;
        }

        private int CalculateBasePrice(ThingDef thingDef)
        {
            // Price floor only - based on community discussion about minimum values
            // Round properly to handle values like 0.9
            return Math.Max(1, (int)Math.Round(thingDef.BaseMarketValue));
        }

        public static string GetCategoryFromThingDef(ThingDef thingDef)
        {
            // 1. Detect and separate children's clothing (Biotech / modded)
            if (thingDef.IsApparel && thingDef.apparel != null)
            {
                var stageFilter = thingDef.apparel.developmentalStageFilter;
                if (stageFilter == DevelopmentalStage.Child)
                    return "Children's Apparel";
            }

            // 2. Check for mechanoids first (they have race but are not biological)
            if (thingDef.race != null)
            {
                if (thingDef.race.IsMechanoid)
                    return "Mechs";
                else if (thingDef.race.Animal)
                    return "Animals";
                else
                    return "Misc";
            }

            // 3. PRIORITIZE: Check specific item types before generic categories
            if (thingDef.IsDrug || thingDef.IsPleasureDrug)
                return "Drugs";
            if (thingDef.IsMedicine)
                return "Medicine";
            if (thingDef.IsWeapon)
                return "Weapon";
            if (thingDef.IsApparel)
                return "Apparel";

            // 4. Use the def's assigned ThingCategory if available
            if (thingDef.FirstThingCategory != null)
                return thingDef.FirstThingCategory.LabelCap;

            // 5. Fallback
            return "Misc";
        }
    }
}