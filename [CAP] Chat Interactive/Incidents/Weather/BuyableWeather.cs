// File: BuyableWeather.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
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

        /// <summary>
        /// 1 = saved before Anomaly and modded weather were forced off once.
        /// 2 = that one-time pass has run, so a later manual enable stays on.
        /// The parameterless constructor stays at 1 so old JSON that omitted this field still gets the pass.
        /// </summary>
        public const int DefaultPolicyVersion = 2;

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

            // Match events store: Core + official DLC weather on; Anomaly and third-party mod weather off.
            // Unknown / null modContentPack is treated as third-party (disabled) — never assume Core.
            if (ShouldAutoDisableModWeather(weatherDef))
                Enabled = false;
            else
                Enabled = true;

            // New discoveries are already on the current default. Do not turn them off again if the streamer enables them.
            Version = DefaultPolicyVersion;
        }

        /// <summary>
        /// Same policy as BuyableIncident.ShouldAutoDisableModEvent:
        /// Core and Royalty, Ideology, Biotech, and Odyssey stay available.
        /// Anomaly (blood rain) and third-party mod weather default off.
        /// </summary>
        public static bool ShouldAutoDisableModWeather(WeatherDef weatherDef)
        {
            var pack = weatherDef?.modContentPack;

            // No pack metadata (broken/injected defs, some SoS edge cases) → treat as mod content, stay off
            if (pack == null)
                return true;

            string packageId = pack.PackageId ?? string.Empty;
            string modSource = pack.Name ?? string.Empty;

            // Anomaly is Ludeon.RimWorld.Anomaly. Check it before the Ludeon allow or blood rain starts on.
            if (packageId.IndexOf("Anomaly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                modSource.IndexOf("Anomaly", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

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

            if (modSource == "RimWorld" || modSource == "Core")
                return false;

            // Official DLC display names (Anomaly intentionally not auto-enabled — same as events)
            string[] officialDLCs = { "Royalty", "Ideology", "Biotech", "Odyssey" };
            if (officialDLCs.Any(dlc => modSource.IndexOf(dlc, StringComparison.OrdinalIgnoreCase) >= 0))
                return false;

            // Everything else (Save Our Ship, VE, etc.) → disabled on first discovery
            return true;
        }

        /// <summary>
        /// A weather can be listed and sold only when the live def has a name, a label, and a description.
        /// </summary>
        public static bool HasRequiredText(WeatherDef weatherDef)
        {
            return weatherDef != null
                && !string.IsNullOrWhiteSpace(weatherDef.defName)
                && !string.IsNullOrWhiteSpace(weatherDef.label)
                && !string.IsNullOrWhiteSpace(weatherDef.description);
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
