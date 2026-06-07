// ItemConfigHelper.cs
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
//
// Helper methods for store command handling

using CAP_ChatInteractive;
using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
namespace _CAP__Chat_Interactive.Command.CommandHelpers
{
    public static class ItemConfigHelper
    {
        /// <summary>
        /// Calculates the final price of an item based on its base price, quantity, quality, and material.
        /// </summary>
        /// <param name="storeItem"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <returns>The final price of the item.</returns>
        public static int CalculateFinalPrice(StoreItem storeItem, int quantity, QualityCategory? quality, ThingDef material)
        {
            try
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                    return storeItem.BasePrice * quantity;

                // For normal items use fast path
                if (!StoreItem.IsUniqueWeapon(thingDef))
                {
                    float baseCost = storeItem.BasePrice;

                    if (thingDef.MadeFromStuff && material != null)
                        baseCost *= (material.BaseMarketValue > 0 ? material.BaseMarketValue : 1f);

                    if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)))
                        baseCost *= GetQualityMultiplier(quality.Value);

                    return Math.Max(1, (int)(baseCost * quantity));
                }

                // Unique weapons → handled in BuyItemCommandHandler after real spawn
                return storeItem.BasePrice * quantity; // temporary placeholder
            }
            catch
            {
                return storeItem.BasePrice * quantity;
            }
        }

        public static bool IsQualityAllowed(QualityCategory? quality)
        {
            if (!quality.HasValue)
            {
                Logger.Debug($"IsQualityAllowed: No quality specified, allowing");
                return true;
            }

            // Get settings from the mod instance
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                Logger.Debug($"IsQualityAllowed: No settings found, allowing quality {quality.Value}");
                return true;
            }

            bool isAllowed = quality.Value switch
            {
                QualityCategory.Awful => settings.AllowAwfulQuality,
                QualityCategory.Poor => settings.AllowPoorQuality,
                QualityCategory.Normal => settings.AllowNormalQuality,
                QualityCategory.Good => settings.AllowGoodQuality,
                QualityCategory.Excellent => settings.AllowExcellentQuality,
                QualityCategory.Masterwork => settings.AllowMasterworkQuality,
                QualityCategory.Legendary => settings.AllowLegendaryQuality,
                _ => true
            };

            Logger.Debug($"IsQualityAllowed: Quality {quality.Value} - Allowed: {isAllowed}");
            return isAllowed;
        }

        public static ThingDef ParseMaterial(string materialStr, ThingDef thingDef)
        {
            if (string.IsNullOrEmpty(materialStr) || materialStr.Equals("random", StringComparison.OrdinalIgnoreCase))
                return null;

            // If the thing doesn't use materials, return null
            if (!thingDef.MadeFromStuff)
                return null;

            // Try to find the material def
            var materialDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def => def.IsStuff &&
                    (def.defName.Equals(materialStr, StringComparison.OrdinalIgnoreCase) ||
                     def.label?.Equals(materialStr, StringComparison.OrdinalIgnoreCase) == true));

            // Check if this material can be used for the thing
            if (materialDef != null && thingDef.stuffCategories != null)
            {
                foreach (var stuffCategory in thingDef.stuffCategories)
                {
                    if (materialDef.stuffProps?.categories?.Contains(stuffCategory) == true)
                        return materialDef;
                }
            }

            return null;
        }

        // === ItemConfigHelper
        public static QualityCategory? ParseQuality(string qualityStr)
        {
            if (string.IsNullOrEmpty(qualityStr) || qualityStr.Equals("random", StringComparison.OrdinalIgnoreCase))
                return null;

            return qualityStr.ToLower() switch
            {
                "awful" => QualityCategory.Awful,
                "poor" => QualityCategory.Poor,
                "normal" => QualityCategory.Normal,
                "good" => QualityCategory.Good,
                "excellent" => QualityCategory.Excellent,
                "masterwork" => QualityCategory.Masterwork,
                "legendary" => QualityCategory.Legendary,
                _ => null
            };
        }

        public static float GetQualityMultiplier(QualityCategory quality)
        {
            // Get settings from the mod instance
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                // Logger.Debug($"GetQualityMultiplier: No settings found, using default values");
                // Fallback to default values if settings aren't available
                return quality switch
                {
                    QualityCategory.Awful => 0.5f,
                    QualityCategory.Poor => 0.75f,
                    QualityCategory.Normal => 1.0f,
                    QualityCategory.Good => 1.5f,
                    QualityCategory.Excellent => 2.0f,
                    QualityCategory.Masterwork => 3.0f,
                    QualityCategory.Legendary => 5.0f,
                    _ => 1.0f
                };
            }

            // Use the configurable settings from GlobalSettings
            return quality switch
            {
                QualityCategory.Awful => settings.AwfulQuality,
                QualityCategory.Poor => settings.PoorQuality,
                QualityCategory.Normal => settings.NormalQuality,
                QualityCategory.Good => settings.GoodQuality,
                QualityCategory.Excellent => settings.ExcellentQuality,
                QualityCategory.Masterwork => settings.MasterworkQuality,
                QualityCategory.Legendary => settings.LegendaryQuality,
                _ => 1.0f
            };
        }
    }
}