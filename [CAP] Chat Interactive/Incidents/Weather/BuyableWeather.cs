// BuyableWeather.cs
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
// Represents a weather event that can be purchased and triggered in the game.
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class BuyableWeather
    {
        public string DefName { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }

        public int BaseCost { get; set; } = 200;
        public string KarmaType { get; set; } = "Neutral";
        public int EventCap { get; set; } = 3;

        /// <summary>
        /// Default false (safe). Constructor enables Core/DLC only; third-party mod weather stays off until manually enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        public string ModSource { get; set; } = "RimWorld";

        /// <summary>
        /// Whether this weather belongs to a currently active (loaded) mod.
        /// Used by external RICS-Pricelist GitHub exporter. False when the mod is no longer active.
        /// </summary>
        public bool modactive { get; set; } = false;

        public int Version { get; set; } = 1;

        public BuyableWeather() { }

        public BuyableWeather(WeatherDef weatherDef)
        {
            if (weatherDef == null)
                throw new ArgumentNullException(nameof(weatherDef));

            DefName = weatherDef.defName;
            Label = weatherDef.label;
            Description = weatherDef.description;
            ModSource = weatherDef.modContentPack?.Name
                        ?? weatherDef.modContentPack?.PackageId
                        ?? "Unknown";

            SetDefaultPricing(weatherDef);

            // Match events store: Core + official DLC weather on; third-party mod weather off until enabled manually.
            // Unknown / null modContentPack is treated as third-party (disabled) — never assume Core.
            if (ShouldAutoDisableModWeather(weatherDef))
                Enabled = false;
            else
                Enabled = true;
        }

        /// <summary>
        /// Same policy as BuyableIncident.ShouldAutoDisableModEvent:
        /// Core/RimWorld and official DLC stay available; third-party mod weather defaults off on first discovery.
        /// </summary>
        public static bool ShouldAutoDisableModWeather(WeatherDef weatherDef)
        {
            var pack = weatherDef?.modContentPack;

            // No pack metadata (broken/injected defs, some SoS edge cases) → treat as mod content, stay off
            if (pack == null)
                return true;

            string packageId = pack.PackageId ?? string.Empty;
            if (packageId.StartsWith("Ludeon.", StringComparison.OrdinalIgnoreCase))
                return false;

            // Official expansion content (package ids / names)
            if (packageId.IndexOf("Royalty", StringComparison.OrdinalIgnoreCase) >= 0 ||
                packageId.IndexOf("Ideology", StringComparison.OrdinalIgnoreCase) >= 0 ||
                packageId.IndexOf("Biotech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                packageId.IndexOf("Odyssey", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            string modSource = pack.Name ?? string.Empty;
            if (modSource == "RimWorld" || modSource == "Core")
                return false;

            // Official DLC display names (Anomaly intentionally not auto-enabled — same as events)
            string[] officialDLCs = { "Royalty", "Ideology", "Biotech", "Odyssey" };
            if (officialDLCs.Any(dlc => modSource.IndexOf(dlc, StringComparison.OrdinalIgnoreCase) >= 0))
                return false;

            // Everything else (Save Our Ship, VE, etc.) → disabled on first discovery
            return true;
        }

        private void SetDefaultPricing(WeatherDef weatherDef)
        {
            string defName = weatherDef.defName?.ToLowerInvariant() ?? string.Empty;

            if (defName.Contains("tox") || defName.Contains("blood") || defName.Contains("vomit") ||
                defName.Contains("doom") || defName.Contains("cataclysm"))
            {
                BaseCost = 600;
                KarmaType = "Doom";
            }
            else if (defName.Contains("hurricane") || defName.Contains("tornado") ||
                     defName.Contains("catastrophe") || defName.Contains("blizzard") ||
                     defName.Contains("torrential") || defName.Contains("storm"))
            {
                BaseCost = 300;
                KarmaType = "Bad";
            }
            else if (defName.Contains("snow"))
            {
                BaseCost = 200;
                KarmaType = IsHeavySnow(defName) ? "Bad" : "Neutral";
            }
            else if (defName.Contains("rain") || defName.Contains("fog"))
            {
                BaseCost = 150;
                KarmaType = "Neutral";
            }
            else if (defName.Contains("clear") || defName.Contains("sunny"))
            {
                BaseCost = 100;
                KarmaType = "Good";
            }
            else
            {
                BaseCost = 175;
                KarmaType = "Neutral";
            }
        }

        private static bool IsHeavySnow(string defName)
        {
            return defName.Contains("hard") || defName.Contains("heavy");
        }
    }

    public class TemperatureVariant
    {
        public string BaseWeatherDefName { get; set; }
        public string ColdVariantDefName { get; set; }
        public float ThresholdTemperature { get; set; } = 0f;
    }
}
