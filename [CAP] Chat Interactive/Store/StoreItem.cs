// StoreItem.cs 
using System.Collections.Generic;
using Verse;
using UnityEngine;

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

        // StoreItem.cs - In the constructor, change these lines:
        public StoreItem(ThingDef thingDef)
        {
            DefName = thingDef.defName;
            BasePrice = CalculateBasePrice(thingDef);
            Category = GetCategoryFromThingDef(thingDef);
            ModSource = thingDef.modContentPack?.Name ?? "RimWorld";

            // Set default properties based on thing type
            IsWeapon = thingDef.IsWeapon;
            IsMelee = thingDef.IsMeleeWeapon;
            IsRanged = thingDef.IsRangedWeapon;
            IsEquippable = thingDef.IsWeapon;
            IsWearable = thingDef.IsApparel;
            IsUsable = IsItemUsable(thingDef);
            IsStuffAllowed = thingDef.IsStuff;

            // FIX: Set default quantity limit to 1 stack instead of 0
            HasQuantityLimit = true;
            int baseStack = Mathf.Max(1, thingDef.stackLimit);
            QuantityLimit = baseStack;
            LimitMode = QuantityLimitMode.OneStack;
        }
        private bool IsItemUsable(ThingDef thingDef)
        {
            // Items that can be consumed or used
            return thingDef.IsIngestible ||
                   thingDef.IsMedicine ||
                   thingDef.IsDrug ||
                   thingDef.defName.Contains("Psytrainer") ||
                   thingDef.defName.Contains("Neuroformer") ||
                   thingDef.defName.Contains("Serum") ||
                   thingDef.defName.Contains("Pack") ||
                   thingDef.IsPleasureDrug;
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
                    return "Mechs"; // Changed from "Animal" to "Mechs"
                else if (thingDef.race.Animal)
                    return "Animals"; // Biological animals
                else
                    return "Misc"; // Fallback for other race types
            }

            // 3. Use the def's assigned ThingCategory if available
            if (thingDef.FirstThingCategory != null)
                return thingDef.FirstThingCategory.LabelCap;

            // 4. Handle major built-in types
            if (thingDef.IsWeapon)
                return "Weapon";
            if (thingDef.IsApparel)
                return "Apparel";
            if (thingDef.IsMedicine)
                return "Medicine";
            if (thingDef.IsDrug)
                return "Drug";

            // 5. Fallback
            return "Misc";
        }
    }
}