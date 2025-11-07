// StoreItem.cs 
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Represents an item available in the chat interactive store
using RimWorld;
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
        public float Weight { get; set; } = 1.0f;
        public bool IsUsable { get; set; } = true;
        public bool IsEquippable { get; set; }
        public bool IsWearable { get; set; }
        public bool IsWeapon { get; set; }
        public bool IsMelee { get; set; }
        public bool IsRanged { get; set; }
        public bool IsStuffAllowed { get; set; }
        public string KarmaType { get; set; }
        public string KarmaTypeForUsing { get; set; }
        public string KarmaTypeForWearing { get; set; }
        public string KarmaTypeForEquipping { get; set; }
        public List<string> ResearchOverrides { get; set; }
        public string Category { get; set; }
        public string ModSource { get; set; }
        public int Version { get; set; } = 2;
        public bool Enabled { get; set; } = true;

        // REMOVE the entire ExposeData method

        public StoreItem() { }

        public StoreItem(ThingDef thingDef)
        {
            DefName = thingDef.defName;
            CustomName = thingDef.label.CapitalizeFirst();
            BasePrice = CalculateBasePrice(thingDef);
            Category = GetCategoryFromThingDef(thingDef);  // This needs fixing
            ModSource = thingDef.modContentPack?.Name ?? "RimWorld";

            // Set default properties based on thing type - IMPROVED LOGIC
            IsWeapon = thingDef.IsWeapon;
            IsMelee = thingDef.IsMeleeWeapon;
            IsRanged = thingDef.IsRangedWeapon;
            IsUsable = IsItemUsable(thingDef);
            IsEquippable = !IsUsable && thingDef.IsWeapon;
            IsWearable = !IsUsable && !IsEquippable && thingDef.IsApparel;
            IsStuffAllowed = thingDef.IsStuff;

            // FIX: Set default quantity limit to 1 stack instead of 0
            HasQuantityLimit = true;
            int baseStack = Mathf.Max(1, thingDef.stackLimit);
            QuantityLimit = baseStack;
            LimitMode = QuantityLimitMode.OneStack;
        }
        public static bool IsItemUsable(ThingDef thingDef)
        {
            // Items that can be consumed/used up when used
            return thingDef.IsIngestible ||
                   thingDef.IsMedicine ||
                   thingDef.IsDrug ||
                   thingDef.IsPleasureDrug ||
                   thingDef.defName.Contains("Neurotrainer") ||
                   thingDef.defName.Contains("Psytrainer") ||
                   thingDef.defName.Contains("Neuroformer") ||
                   thingDef.defName.Contains("Serum") ||
                   thingDef.defName.Contains("Pack");
        }

        private int CalculateBasePrice(ThingDef thingDef)
        {
            return (int)(thingDef.BaseMarketValue * 1.67f);
        }

        private string GetCategoryFromThingDef(ThingDef thingDef)
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

            // 4. REMOVE the special drug detection here - it's redundant
            // Items that are properly flagged as drugs will be caught above

            // 5. Use the def's assigned ThingCategory if available
            if (thingDef.FirstThingCategory != null)
                return thingDef.FirstThingCategory.LabelCap;

            // 6. Fallback
            return "Misc";
        }
    }
}