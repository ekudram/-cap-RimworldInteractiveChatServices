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
            GUI.color = ColorLibrary.HeaderAccent;
            // Widgets.Label(titleRect, $"Def Information: {thingDef.LabelCap}");
            Widgets.Label(titleRect, $"RICS.DIW.Header".Translate(thingDef.LabelCap));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawDefInfo(contentRect);
        }

        private void DrawDefInfo(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            // Always show at top
            sb.AppendLine("RICS.DIW.DefName".Translate(thingDef.defName.Named("0")));
            sb.AppendLine("RICS.DIW.BaseMarketValue".Translate(thingDef.BaseMarketValue.ToStringMoney("F2").Named("0")));
            sb.AppendLine($"");

            // ThingDef properties
            sb.AppendLine("RICS.DIW.ThingClass".Translate((thingDef.thingClass?.Name ?? "null").Named("0")));
            sb.AppendLine("RICS.DIW.StackLimit".Translate(thingDef.stackLimit.Named("0")));
            sb.AppendLine("RICS.DIW.Size".Translate(
                thingDef.size.x.Named("width"),
                thingDef.size.z.Named("height")   // RimWorld uses x/z for width/depth
            ));
            sb.AppendLine("RICS.DIW.TechLevel".Translate(thingDef.techLevel.ToStringHuman().Named("0")));
            sb.AppendLine("RICS.DIW.Tradeability".Translate(thingDef.tradeability.ToString().Named("0")));

            // Boolean properties - only show if true
            if (thingDef.IsIngestible)
                sb.AppendLine("RICS.DIW.IsIngestible".Translate(thingDef.IsIngestible.ToStringYesNo().Named("0")));
            if (thingDef.IsMedicine)
                sb.AppendLine("RICS.DIW.IsMedicine".Translate(thingDef.IsMedicine.ToStringYesNo().Named("0")));
            if (thingDef.IsStuff)
                sb.AppendLine("RICS.DIW.IsStuff".Translate(thingDef.IsStuff.ToStringYesNo().Named("0")));
            if (thingDef.IsDrug)
                sb.AppendLine("RICS.DIW.IsDrug".Translate(thingDef.IsDrug.ToStringYesNo().Named("0")));
            if (thingDef.IsPleasureDrug)
                sb.AppendLine("RICS.DIW.IsPleasureDrug".Translate(thingDef.IsPleasureDrug.ToStringYesNo().Named("0")));
            if (thingDef.IsNonMedicalDrug)
                sb.AppendLine("RICS.DIW.IsNonMedicalDrug".Translate(thingDef.IsNonMedicalDrug.ToStringYesNo().Named("0")));
            if (thingDef.IsApparel)
                sb.AppendLine("RICS.DIW.IsApparel".Translate(thingDef.IsApparel.ToStringYesNo().Named("0")));
            if (thingDef.Claimable)
                sb.AppendLine("RICS.DIW.Claimable".Translate(thingDef.Claimable.ToStringYesNo().Named("0")));
            if (thingDef.IsWeapon)
                sb.AppendLine("RICS.DIW.IsWeapon".Translate(thingDef.IsWeapon.ToStringYesNo().Named("0")));
            if (thingDef.IsBuildingArtificial)
                sb.AppendLine("RICS.DIW.IsBuildingArtificial".Translate(thingDef.IsBuildingArtificial.ToStringYesNo().Named("0")));

            // MINIFICATION PROPERTIES - Always show these
            sb.AppendLine("RICS.DIW.Minifiable".Translate(thingDef.Minifiable.ToStringYesNo().Named("0")));

            if (thingDef.Minifiable)
            {
                sb.AppendLine("RICS.DIW.MinifiableNote".Translate());
            }
            if (thingDef.smeltable)
            {
                sb.AppendLine("RICS.DIW.Smeltable".Translate(thingDef.smeltable.ToStringYesNo().Named("0")));
            }
            sb.AppendLine($"");
            sb.AppendLine("RICS.DIW.DeliverySection".Translate());
            sb.AppendLine("RICS.DIW.WillMinify".Translate(
                ShouldMinifyForDelivery(thingDef).ToStringYesNo().Named("0")
            ));
            sb.AppendLine("RICS.DIW.StackLimit".Translate(
                thingDef.stackLimit.Named("0")
            ));
            sb.AppendLine("RICS.DIW.Size".Translate(
                thingDef.size.x.Named("width"),
                thingDef.size.z.Named("height")
            ));

            sb.AppendLine("");

            // List comps if the thing has them
            if (thingDef.comps != null && thingDef.comps.Count > 0)
            {
                sb.AppendLine("RICS.DIW.CompSection".Translate(thingDef.comps.Count.Named("0")));

                foreach (var compProps in thingDef.comps)
                {
                    string compName = compProps.compClass?.Name ?? "null";
                    sb.AppendLine("RICS.DIW.CompBullet".Translate(compName.Named("0")));

                    // Show detailed CompProperties_UseEffect information
                    if (compProps is CompProperties_UseEffect compUseEffect)
                    {
                        sb.AppendLine("RICS.DIW.UseEffectType".Translate(compUseEffect.GetType().Name.Named("0")));
                        if (compUseEffect.doCameraShake)
                            sb.AppendLine("RICS.DIW.CameraShake".Translate());
                        if (compUseEffect.moteOnUsed != null)
                            sb.AppendLine("RICS.DIW.MoteOnUsed".Translate(compUseEffect.moteOnUsed.defName.Named("0")));
                        if (compUseEffect.moteOnUsedScale != 1f)
                            sb.AppendLine("RICS.DIW.MoteScale".Translate(compUseEffect.moteOnUsedScale.ToString("F2").Named("0")));
                        if (compUseEffect.fleckOnUsed != null)
                            sb.AppendLine("RICS.DIW.FleckOnUsed".Translate(compUseEffect.fleckOnUsed.defName.Named("0")));
                        if (compUseEffect.fleckOnUsedScale != 1f)
                            sb.AppendLine("RICS.DIW.FleckScale".Translate(compUseEffect.fleckOnUsedScale.ToString("F2").Named("0")));
                        if (compUseEffect.effecterOnUsed != null)
                            sb.AppendLine("RICS.DIW.EffecterOnUsed".Translate(compUseEffect.effecterOnUsed.defName.Named("0")));
                        if (compUseEffect.warmupEffecter != null)
                            sb.AppendLine("RICS.DIW.WarmupEffecter".Translate(compUseEffect.warmupEffecter.defName.Named("0")));
                    }
                    else if (compProps is CompProperties_FoodPoisonable)
                    {
                        sb.AppendLine("RICS.DIW.FoodPoisonable".Translate());
                    }
                    // Add more comp type checks as needed (same pattern)
                }

                sb.AppendLine("");  // empty line separator
            }

            // Ingestible properties if exists
            if (thingDef.ingestible != null)
            {
                sb.AppendLine("RICS.DIW.IngestibleSection".Translate());
                sb.AppendLine("RICS.DIW.Nutrition".Translate(
                    thingDef.ingestible.CachedNutrition.ToString("F2").Named("0")
                ));
                sb.AppendLine("RICS.DIW.FoodType".Translate(
                    thingDef.ingestible.foodType.ToString().Named("0")
                ));
                sb.AppendLine("RICS.DIW.Preferability".Translate(
                    thingDef.ingestible.preferability.ToString().Named("0")
                ));
                sb.AppendLine("");  // empty line separator
            }

            // Apparel properties if exists
            if (thingDef.apparel != null)
            {
                sb.AppendLine("RICS.DIW.ApparelSection".Translate());
                sb.AppendLine("RICS.DIW.ApparelLayers".Translate(
                    string.Join(", ", thingDef.apparel.layers).Named("layers")
                ));
                sb.AppendLine("RICS.DIW.ApparelBodyPartGroups".Translate(
                    string.Join(", ",
                        thingDef.apparel.bodyPartGroups?.Select(g => g.defName) ?? Enumerable.Empty<string>()
                    ).Named("bodyParts")
                ));
                sb.AppendLine("");
            }

            // Weapon properties if exists
            if (thingDef.weaponTags != null && thingDef.weaponTags.Count > 0)
            {
                sb.AppendLine("RICS.DIW.WeaponSection".Translate());

                sb.AppendLine("RICS.DIW.WeaponTags".Translate(
                    string.Join(", ", thingDef.weaponTags).Named("tags")
                ));

                sb.AppendLine("");
            }

            // StoreItem information
            sb.AppendLine("RICS.DIW.StoreItemSection".Translate());
            sb.AppendLine("RICS.DIW.CustomName".Translate(
                (storeItem.CustomName ?? "None").Named("0")
            ));
            sb.AppendLine("RICS.DIW.BasePrice".Translate(
                storeItem.BasePrice.ToString("F2").Named("0")
            ));
            sb.AppendLine("RICS.DIW.Enabled".Translate(
                storeItem.Enabled.ToStringYesNo().Named("0")
            ));
            sb.AppendLine("RICS.DIW.Category".Translate(
                storeItem.Category.Named("0")
            ));
            sb.AppendLine("RICS.DIW.ModSource".Translate(
                storeItem.ModSource.Named("0")
            ));
            sb.AppendLine("RICS.DIW.IsUsable".Translate(
                storeItem.IsUsable.ToStringYesNo().Named("0")
            ));
            sb.AppendLine("RICS.DIW.IsWearable".Translate(
                storeItem.IsWearable.ToStringYesNo().Named("0")
            ));
            sb.AppendLine("RICS.DIW.IsEquippable".Translate(
                storeItem.IsEquippable.ToStringYesNo().Named("0")
            ));
            sb.AppendLine("RICS.DIW.HasQuantityLimit".Translate(
                storeItem.HasQuantityLimit.ToStringYesNo().Named("0")
            ));
            sb.AppendLine("RICS.DIW.QuantityLimit".Translate(
                storeItem.QuantityLimit.Named("0")
            ));
            string fullText = sb.ToString();

            // Custom name editor - positioned at the top
            Rect customNameRect = new Rect(rect.x, rect.y, rect.width, 30f);

            // Label
            Rect labelRect = new Rect(customNameRect.x, customNameRect.y, 100f, 30f);
            // Widgets.Label(labelRect, "Custom Name:");
            Widgets.Label(labelRect, "RICS.DIW.CustomNameLabel".Translate());   

            // Text input
            Rect inputRect = new Rect(customNameRect.x + 105f, customNameRect.y, rect.width - 215f, 30f);
            customNameBuffer = Widgets.TextField(inputRect, customNameBuffer);

            // Clear button (only show if there's a custom name)
            if (!string.IsNullOrEmpty(storeItem.CustomName))
            {
                Rect clearButtonRect = new Rect(customNameRect.x + rect.width - 210f, customNameRect.y, 100f, 30f);
                if (Widgets.ButtonText(clearButtonRect, "RICS.DIW.ClearButton".Translate()))
                {
                    storeItem.CustomName = null;
                    customNameBuffer = "";
                    StoreInventory.SaveStoreToJson();

                    Messages.Message(
                        "RICS.DIW.CustomNameCleared".Translate(),
                        MessageTypeDefOf.PositiveEvent
                    );

                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            // Save button
            Rect saveButtonRect = new Rect(customNameRect.x + rect.width - 105f, customNameRect.y, 100f, 30f);
            bool canSave = !string.IsNullOrWhiteSpace(customNameBuffer) && customNameBuffer != storeItem.CustomName;

            string saveButtonText = "RICS.DIW.SaveButton".Translate();

            // Disable save button if name is duplicate
            if (IsCustomNameDuplicate(customNameBuffer))
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(saveButtonRect, saveButtonText);
                GUI.color = Color.white;

                if (Widgets.ButtonText(saveButtonRect, saveButtonText)) // still clickable for feedback
                {
                    Messages.Message(
                        "RICS.DIW.CannotSaveDuplicateMessage".Translate(),
                        MessageTypeDefOf.RejectInput
                    );
                }

                TooltipHandler.TipRegion(saveButtonRect,
                    "RICS.DIW.CannotSaveDuplicateTooltip".Translate());
            }
            else if (Widgets.ButtonText(saveButtonRect, saveButtonText) && canSave)
            {
                storeItem.CustomName = customNameBuffer.Trim();
                StoreInventory.SaveStoreToJson();

                Messages.Message(
                    "RICS.DIW.CustomNameUpdated".Translate(
                        storeItem.CustomName.Named("0")
                    ),
                    MessageTypeDefOf.PositiveEvent
                );

                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else if (!canSave)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(saveButtonRect, saveButtonText);
                GUI.color = Color.white;

                TooltipHandler.TipRegion(saveButtonRect,
                    "RICS.DIW.NoChangesTooltip".Translate());
            }

            // Warning message below the input field (if duplicate)
            if (!string.IsNullOrWhiteSpace(customNameBuffer) && IsCustomNameDuplicate(customNameBuffer))
            {
                Rect warningRect = new Rect(rect.x + 105f, rect.y + 35f, rect.width - 110f, 20f);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = ColorLibrary.HeaderAccent;
                // Widgets.Label(warningRect, "⚠ Warning: This name is already in use by another item");
                Widgets.Label(warningRect, "RICS.DIW.DuplicateNameWarning".Translate());    
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