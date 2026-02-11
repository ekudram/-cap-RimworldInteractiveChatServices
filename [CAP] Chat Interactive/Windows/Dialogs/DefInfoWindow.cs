// DefInfoWindow.cs
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
// A dialog window for editing store items in the Chat Interactive mod

using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive
{
    // This class represents a window that displays detailed information about a specific ThingDef and its corresponding StoreItem.
    public class DefInfoWindow : Window
    {
        private ThingDef thingDef;
        private StoreItem storeItem;
        private Vector2 scrollPosition = Vector2.zero;
        private string customNameBuffer = "";

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public DefInfoWindow(ThingDef thingDef, StoreItem storeItem)
        {
            this.thingDef = thingDef;
            this.storeItem = storeItem;
            this.customNameBuffer = storeItem.CustomName ?? "";
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Rect titleRect = new Rect(0f, 0f, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, $"Def Information: {thingDef.LabelCap}");
            Text.Font = GameFont.Small;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawDefInfo(contentRect);
        }

        private void DrawDefInfo(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            // Always show at top
            sb.AppendLine($"DefName: {thingDef.defName}");
            sb.AppendLine($"BaseMarketValue: {thingDef.BaseMarketValue}");
            sb.AppendLine($"");

            // ThingDef properties
            sb.AppendLine($"thingClass: {thingDef.thingClass?.Name ?? "null"}");
            sb.AppendLine($"stackLimit: {thingDef.stackLimit}");
            sb.AppendLine($"Size: {thingDef.size}");
            sb.AppendLine($"TechLevel: {thingDef.techLevel}");
            sb.AppendLine($"Tradeability: {thingDef.tradeability}");

            // Boolean properties - only show if true
            if (thingDef.IsIngestible) sb.AppendLine($"IsIngestible: {thingDef.IsIngestible}");
            if (thingDef.IsMedicine) sb.AppendLine($"IsMedicine: {thingDef.IsMedicine}");
            if (thingDef.IsStuff) sb.AppendLine($"IsStuff: {thingDef.IsStuff}");
            if (thingDef.IsDrug) sb.AppendLine($"IsDrug: {thingDef.IsDrug}");
            if (thingDef.IsPleasureDrug) sb.AppendLine($"IsPleasureDrug: {thingDef.IsPleasureDrug}");
            if (thingDef.IsNonMedicalDrug) sb.AppendLine($"IsNonMedicalDrug: {thingDef.IsNonMedicalDrug}");
            if (thingDef.IsApparel) sb.AppendLine($"IsApparel: {thingDef.IsApparel}");
            if (thingDef.Claimable) sb.AppendLine($"Claimable: {thingDef.Claimable}");
            if (thingDef.IsWeapon) sb.AppendLine($"IsWeapon: {thingDef.IsWeapon}");
            if (thingDef.IsBuildingArtificial) sb.AppendLine($"IsBuildingArtificial: {thingDef.IsBuildingArtificial}");

            // MINIFICATION PROPERTIES - Always show these
            sb.AppendLine($"Minifiable: {thingDef.Minifiable}");
            if (thingDef.Minifiable)
            {
                sb.AppendLine($"  - Will be delivered as minified crate");
            }

            if (thingDef.smeltable) sb.AppendLine($"Smeltable: {thingDef.smeltable}");

            sb.AppendLine($"");

            // Delivery Information
            sb.AppendLine($"--- Delivery Information ---");
            sb.AppendLine($"Will Minify: {ShouldMinifyForDelivery(thingDef)}");
            sb.AppendLine($"Stack Limit: {thingDef.stackLimit}");
            sb.AppendLine($"Size: {thingDef.size}");
            sb.AppendLine($"");

            // List comps if the thing has them
            if (thingDef.comps != null && thingDef.comps.Count > 0)
            {
                sb.AppendLine($"--- Comp Properties ({thingDef.comps.Count}) ---");
                foreach (var compProps in thingDef.comps)
                {
                    sb.AppendLine($"• {compProps.compClass?.Name ?? "null"}");

                    // Show detailed CompProperties_UseEffect information
                    if (compProps is CompProperties_UseEffect compUseEffect)
                    {
                        sb.AppendLine($"  - UseEffect Type: {compUseEffect.GetType().Name}");
                        if (compUseEffect.doCameraShake) sb.AppendLine($"  - Camera Shake: Yes");
                        if (compUseEffect.moteOnUsed != null) sb.AppendLine($"  - Mote On Used: {compUseEffect.moteOnUsed.defName}");
                        if (compUseEffect.moteOnUsedScale != 1f) sb.AppendLine($"  - Mote Scale: {compUseEffect.moteOnUsedScale}");
                        if (compUseEffect.fleckOnUsed != null) sb.AppendLine($"  - Fleck On Used: {compUseEffect.fleckOnUsed.defName}");
                        if (compUseEffect.fleckOnUsedScale != 1f) sb.AppendLine($"  - Fleck Scale: {compUseEffect.fleckOnUsedScale}");
                        if (compUseEffect.effecterOnUsed != null) sb.AppendLine($"  - Effecter On Used: {compUseEffect.effecterOnUsed.defName}");
                        if (compUseEffect.warmupEffecter != null) sb.AppendLine($"  - Warmup Effecter: {compUseEffect.warmupEffecter.defName}");
                    }
                    else if (compProps is CompProperties_FoodPoisonable compFoodPoison)
                    {
                        sb.AppendLine($"  - FoodPoisonable");
                    }
                    // Add more comp type checks as needed
                }
                sb.AppendLine($"");
            }

            // Ingestible properties if exists
            if (thingDef.ingestible != null)
            {
                sb.AppendLine($"--- Ingestible Properties ---");
                sb.AppendLine($"Nutrition: {thingDef.ingestible.CachedNutrition}");
                sb.AppendLine($"FoodType: {thingDef.ingestible.foodType}");
                sb.AppendLine($"Preferability: {thingDef.ingestible.preferability}");
                sb.AppendLine($"");
            }

            // Apparel properties if exists
            if (thingDef.apparel != null)
            {
                sb.AppendLine($"--- Apparel Properties ---");
                sb.AppendLine($"Layers: {string.Join(", ", thingDef.apparel.layers)}");
                sb.AppendLine($"BodyPartGroups: {string.Join(", ", thingDef.apparel.bodyPartGroups?.Select(g => g.defName) ?? new List<string>())}");
                sb.AppendLine($"");
            }

            // Weapon properties if exists
            if (thingDef.weaponTags != null && thingDef.weaponTags.Count > 0)
            {
                sb.AppendLine($"--- Weapon Properties ---");
                sb.AppendLine($"WeaponTags: {string.Join(", ", thingDef.weaponTags)}");
                sb.AppendLine($"");
            }

            // StoreItem information
            sb.AppendLine($"--- Store Item Data ---");
            sb.AppendLine($"Custom Name: {storeItem.CustomName}");
            sb.AppendLine($"Base Price: {storeItem.BasePrice}");
            sb.AppendLine($"Enabled: {storeItem.Enabled}");
            sb.AppendLine($"Category: {storeItem.Category}");
            sb.AppendLine($"Mod Source: {storeItem.ModSource}");
            sb.AppendLine($"IsUsable: {storeItem.IsUsable}");
            sb.AppendLine($"IsWearable: {storeItem.IsWearable}");
            sb.AppendLine($"IsEquippable: {storeItem.IsEquippable}");
            sb.AppendLine($"HasQuantityLimit: {storeItem.HasQuantityLimit}");
            sb.AppendLine($"QuantityLimit: {storeItem.QuantityLimit}");

            string fullText = sb.ToString();

            // Custom name editor - positioned at the top
            Rect customNameRect = new Rect(rect.x, rect.y, rect.width, 30f);

            // Label
            Rect labelRect = new Rect(customNameRect.x, customNameRect.y, 100f, 30f);
            Widgets.Label(labelRect, "Custom Name:");

            // Text input
            Rect inputRect = new Rect(customNameRect.x + 105f, customNameRect.y, rect.width - 215f, 30f);
            customNameBuffer = Widgets.TextField(inputRect, customNameBuffer);

            // Clear button (only show if there's a custom name)
            if (!string.IsNullOrEmpty(storeItem.CustomName))
            {
                Rect clearButtonRect = new Rect(customNameRect.x + rect.width - 210f, customNameRect.y, 100f, 30f);
                if (Widgets.ButtonText(clearButtonRect, "Clear"))
                {
                    storeItem.CustomName = null;
                    customNameBuffer = "";
                    StoreInventory.SaveStoreToJson();
                    Messages.Message("Custom name cleared", MessageTypeDefOf.PositiveEvent);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            // Save button
            Rect saveButtonRect = new Rect(customNameRect.x + rect.width - 105f, customNameRect.y, 100f, 30f);
            bool canSave = !string.IsNullOrWhiteSpace(customNameBuffer) && customNameBuffer != storeItem.CustomName;

            // Disable save button if name is duplicate
            if (IsCustomNameDuplicate(customNameBuffer))
            {
                GUI.color = Color.gray;
                if (Widgets.ButtonText(saveButtonRect, "Save"))
                {
                    Messages.Message("Cannot save: Custom name is already in use by another item",
                        MessageTypeDefOf.RejectInput);
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(saveButtonRect, "Cannot save: This name is already in use by another item");
            }
            else if (Widgets.ButtonText(saveButtonRect, "Save") && canSave)
            {
                storeItem.CustomName = customNameBuffer.Trim();
                StoreInventory.SaveStoreToJson();
                Messages.Message($"Custom name updated to '{storeItem.CustomName}'", MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else if (!canSave)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(saveButtonRect, "Save");
                GUI.color = Color.white;
                TooltipHandler.TipRegion(saveButtonRect, "No changes to save");
            }

            // Warning message below the input field (if duplicate)
            if (!string.IsNullOrWhiteSpace(customNameBuffer) && IsCustomNameDuplicate(customNameBuffer))
            {
                Rect warningRect = new Rect(rect.x + 105f, rect.y + 35f, rect.width - 110f, 20f);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = ColorLibrary.HeaderAccent;
                Widgets.Label(warningRect, "⚠ Warning: This name is already in use by another item");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                // Adjust scroll view position to account for warning message
                Rect scrollRect = new Rect(rect.x, rect.y + 60f, rect.width, rect.height - 60f);

                // Calculate text height
                float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
                Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

                // Scroll view
                Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
                Widgets.EndScrollView();
            }
            else
            {
                // Adjust scroll view to account for custom name editor
                Rect scrollRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);

                // Calculate text height
                float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
                Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

                // Scroll view
                Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
                Widgets.EndScrollView();
            }
        }

        // Helper method to check minification (same logic as StoreCommandHelper)
        private bool ShouldMinifyForDelivery(ThingDef thingDef)
        {
            return thingDef != null && thingDef.Minifiable;
        }
        // Method to check for duplicate custom names
        private HashSet<string> GetAllExistingNames()
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                // Add custom names
                if (!string.IsNullOrEmpty(item.CustomName))
                    existingNames.Add(item.CustomName);

                // Add def names
                existingNames.Add(item.DefName);

                // Add label caps
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                    existingNames.Add(thingDef.LabelCap.ToString());
            }

            return existingNames;
        }

        private bool IsCustomNameDuplicate(string customName)
        {
            if (string.IsNullOrWhiteSpace(customName))
                return false;

            var existingNames = GetAllExistingNames();

            // Remove current item's names from the set to allow editing
            if (!string.IsNullOrEmpty(storeItem.CustomName))
                existingNames.Remove(storeItem.CustomName);

            existingNames.Remove(storeItem.DefName);

            var currentThingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (currentThingDef != null)
            {
                string currentLabelCap = currentThingDef.LabelCap.ToString();
                existingNames.Remove(currentLabelCap);

                // Also remove the custom name if it's the same as LabelCap (default assignment)
                if (storeItem.CustomName == currentLabelCap)
                    existingNames.Remove(storeItem.CustomName);
            }

            return existingNames.Contains(customName);
        }
    }
}