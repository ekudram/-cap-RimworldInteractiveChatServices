// File: AnomalyStaging.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Gates Anomaly events, weather, and store items by monolith stage.
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    /// <summary>
    /// Purchase checks for Anomaly events, weather, and store items.
    /// Editor Enabled is handled by the callers. This only answers the Anomaly rules.
    /// </summary>
    public static class AnomalyStaging
    {
        /// <summary>
        /// Official Anomaly incidents with no vanilla tier use Stirring.
        /// </summary>
        public const int UnsetAnomalyPackTier = 1;

        /// <summary>
        /// Official Anomaly weather with no matching incident uses Waking.
        /// </summary>
        public const int AnomalyWeatherFallbackTier = 2;

        private const int FullyUnlockedTier = int.MaxValue;

        /// <summary>True when this incident is Anomaly content.</summary>
        public static bool IsAnomaly(IncidentDef def)
        {
            if (def == null)
                return false;

            try
            {
                if (def.IsAnomalyIncident)
                    return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Could not read incident tier for {def.defName}: {ex.Message}");
            }

            return IsOfficialAnomalyPack(def.modContentPack);
        }

        /// <summary>
        /// False when the incident cannot be bought yet. <paramref name="reason"/> is a keyed chat line.
        /// </summary>
        public static bool CanPurchase(BuyableIncident incident, out string reason)
        {
            reason = null;
            if (incident == null)
                return true;

            try
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);
                if (def == null || !IsAnomaly(def))
                    return true;

                return AllowEventOrWeather(LabelOf(incident.Label, incident.DefName), RequiredTier(def), out reason);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Event check failed for {incident.DefName}: {ex.Message}");
                reason = "RICS.Anomaly.Blocked.NoDlc".Translate(LabelOf(incident.Label, incident.DefName));
                return false;
            }
        }

        /// <summary>
        /// False when the weather cannot be bought yet. <paramref name="reason"/> is a keyed chat line.
        /// </summary>
        public static bool CanPurchase(BuyableWeather weather, out string reason)
        {
            reason = null;
            if (weather == null)
                return true;

            try
            {
                if (!TryGetWeatherTier(weather, out int requiredTier))
                    return true;

                return AllowEventOrWeather(LabelOf(weather.Label, weather.DefName), requiredTier, out reason);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Weather check failed for {weather.DefName}: {ex.Message}");
                reason = "RICS.Anomaly.Blocked.NoDlc".Translate(LabelOf(weather.Label, weather.DefName));
                return false;
            }
        }

        /// <summary>
        /// False when an Anomaly store item is still sealed by staging.
        /// The "incidents turned off" setting does not apply to store items.
        /// </summary>
        public static bool CanPurchaseItem(ThingDef def, string label, out string reason)
        {
            reason = null;
            if (def == null || !IsOfficialAnomalyPack(def.modContentPack))
                return true;

            try
            {
                return AllowItem(LabelOf(label, def.defName), RequiredItemTier(def), out reason);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Item check failed for {def.defName}: {ex.Message}");
                reason = "RICS.Anomaly.Blocked.NoDlc".Translate(LabelOf(label, def.defName));
                return false;
            }
        }

        /// <summary>Monolith stage line for <c>!anomaly</c>. Does not include the setting line.</summary>
        public static string StageLine()
        {
            if (!ModsConfig.AnomalyActive)
                return "RICS.Anomaly.Stage.NoDlc".Translate();

            try
            {
                AnomalyPlaystyleDef playstyle = CurrentPlaystyle();
                if (playstyle != null && !playstyle.enableAnomalyContent)
                    return "RICS.Anomaly.Stage.Disabled".Translate();

                if (playstyle != null && !playstyle.generateMonolith)
                    return "RICS.Anomaly.Stage.AmbientHorror".Translate();

                string defName = Find.Anomaly?.LevelDef?.defName ?? string.Empty;
                switch (defName)
                {
                    case "Inactive":
                        return "RICS.Anomaly.Stage.Inactive".Translate();
                    case "Stirring":
                        return "RICS.Anomaly.Stage.Stirring".Translate();
                    case "Waking":
                        return "RICS.Anomaly.Stage.Waking".Translate();
                    case "VoidAwakened":
                        return "RICS.Anomaly.Stage.VoidAwakened".Translate();
                    case "Gleaming":
                        return "RICS.Anomaly.Stage.Gleaming".Translate();
                    case "Embraced":
                        return "RICS.Anomaly.Stage.Embraced".Translate();
                    case "Disrupted":
                        return "RICS.Anomaly.Stage.Disrupted".Translate();
                    default:
                        string shown = CurrentMonolithLabel();
                        if (string.IsNullOrEmpty(shown))
                            shown = string.IsNullOrEmpty(defName) ? "Inactive" : defName;
                        return "RICS.Anomaly.Stage.Unknown".Translate(shown);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Stage line failed: {ex.Message}");
                return "RICS.Anomaly.Stage.NoDlc".Translate();
            }
        }

        /// <summary>Second <c>!anomaly</c> line. Always shown.</summary>
        public static string StagingSettingLine()
        {
            return StagingOn()
                ? "RICS.Anomaly.StagingOn".Translate()
                : "RICS.Anomaly.StagingOff".Translate();
        }

        private static bool AllowEventOrWeather(string label, int requiredTier, out string reason)
        {
            reason = null;
            if (!ModsConfig.AnomalyActive)
            {
                reason = "RICS.Anomaly.Blocked.NoDlc".Translate(label);
                return false;
            }

            if (BlockEventsWhenDisabled() && !ContentEnabled())
            {
                reason = "RICS.Anomaly.Blocked.Disabled".Translate(label);
                return false;
            }

            if (!StagingOn())
                return true;

            if (UnlockedTier() >= requiredTier)
                return true;

            reason = BlockedStageLine(label, requiredTier);
            return false;
        }

        private static bool AllowItem(string label, int requiredTier, out string reason)
        {
            reason = null;
            if (!ModsConfig.AnomalyActive)
            {
                reason = "RICS.Anomaly.Blocked.NoDlc".Translate(label);
                return false;
            }

            if (!StagingOn())
                return true;

            if (UnlockedTier() >= requiredTier)
                return true;

            reason = BlockedStageLine(label, requiredTier);
            return false;
        }

        private static int RequiredTier(IncidentDef def)
        {
            if (def == null)
                return UnsetAnomalyPackTier;

            if (def.minAnomalyThreatLevel >= 0)
                return def.minAnomalyThreatLevel;

            return UnsetAnomalyPackTier;
        }

        private static int RequiredItemTier(ThingDef def)
        {
            if (def != null && def.defName == "GoldenCube")
            {
                IncidentDef cubeEvent = DefDatabase<IncidentDef>.GetNamedSilentFail("GoldenCubeArrival");
                if (cubeEvent != null)
                    return RequiredTier(cubeEvent);
                return AnomalyWeatherFallbackTier;
            }

            int tier = UnsetAnomalyPackTier;
            RecipeMakerProperties maker = def?.recipeMaker;
            if (maker == null)
                return tier;

            if (IsAdvancedResearch(maker.researchPrerequisite))
                tier = AnomalyWeatherFallbackTier;

            List<ResearchProjectDef> extras = maker.researchPrerequisites;
            if (extras == null)
                return tier;

            for (int i = 0; i < extras.Count; i++)
            {
                if (IsAdvancedResearch(extras[i]))
                    return AnomalyWeatherFallbackTier;
            }

            return tier;
        }

        private static bool TryGetWeatherTier(BuyableWeather weather, out int requiredTier)
        {
            requiredTier = 0;
            if (weather == null || string.IsNullOrEmpty(weather.DefName))
                return false;

            IncidentDef sameName = DefDatabase<IncidentDef>.GetNamedSilentFail(weather.DefName);
            if (sameName != null && IsAnomaly(sameName))
            {
                requiredTier = RequiredTier(sameName);
                return true;
            }

            WeatherDef weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(weather.DefName);
            if (weatherDef == null)
                return false;

            foreach (GameConditionDef condition in DefDatabase<GameConditionDef>.AllDefsListForReading)
            {
                if (condition?.weatherDef != weatherDef)
                    continue;

                foreach (IncidentDef incident in DefDatabase<IncidentDef>.AllDefsListForReading)
                {
                    if (incident?.gameCondition != condition || !IsAnomaly(incident))
                        continue;

                    requiredTier = RequiredTier(incident);
                    return true;
                }
            }

            if (!IsOfficialAnomalyPack(weatherDef.modContentPack))
                return false;

            requiredTier = AnomalyWeatherFallbackTier;
            return true;
        }

        private static bool IsAdvancedResearch(ResearchProjectDef research)
        {
            return research?.knowledgeCategory != null
                && string.Equals(research.knowledgeCategory.defName, "Advanced", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOfficialAnomalyPack(ModContentPack pack)
        {
            if (!ModsConfig.AnomalyActive || pack == null)
                return false;

            try
            {
                if (!pack.IsOfficialMod || ExpansionDefOf.Anomaly == null)
                    return false;

                string linked = ExpansionDefOf.Anomaly.linkedMod;
                return !string.IsNullOrEmpty(linked)
                    && string.Equals(pack.PackageId, linked, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Pack check failed: {ex.Message}");
                return false;
            }
        }

        private static int UnlockedTier()
        {
            if (!ModsConfig.AnomalyActive)
                return 0;

            AnomalyPlaystyleDef playstyle = CurrentPlaystyle();
            if (playstyle != null && playstyle.enableAnomalyContent && !playstyle.generateMonolith)
                return FullyUnlockedTier;

            GameComponent_Anomaly anomaly = Find.Anomaly;
            if (anomaly == null)
                return 0;

            int fromLevel = 0;
            try
            {
                if (anomaly.LevelDef != null)
                    fromLevel = anomaly.LevelDef.anomalyThreatTier;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Level read failed: {ex.Message}");
            }

            int fromHistory = 0;
            int highestReached = anomaly.HighestLevelReached;
            foreach (MonolithLevelDef level in DefDatabase<MonolithLevelDef>.AllDefsListForReading)
            {
                if (level == null || level.level > highestReached)
                    continue;
                if (level.anomalyThreatTier > fromHistory)
                    fromHistory = level.anomalyThreatTier;
            }

            return Math.Max(fromLevel, fromHistory);
        }

        private static string StageLabelForTier(int requiredTier)
        {
            MonolithLevelDef best = null;
            foreach (MonolithLevelDef level in DefDatabase<MonolithLevelDef>.AllDefsListForReading)
            {
                if (level == null || level.anomalyThreatTier < requiredTier)
                    continue;
                if (best == null || level.level < best.level)
                    best = level;
            }

            if (best == null)
                return requiredTier.ToString();

            return MonolithStageName(best);
        }

        /// <summary>
        /// Monolith levels have no def label. The names players know are the def names.
        /// </summary>
        private static string MonolithStageName(MonolithLevelDef level)
        {
            if (level == null)
                return "Inactive";

            switch (level.defName)
            {
                case "Inactive":
                case "Stirring":
                case "Waking":
                case "Gleaming":
                case "Embraced":
                case "Disrupted":
                    return level.defName;
                case "VoidAwakened":
                    return "Void awakened";
                default:
                    string label = level.LabelCap.ToString();
                    return string.IsNullOrEmpty(label) ? level.defName : label;
            }
        }

        private static string BlockedStageLine(string label, int requiredTier)
        {
            return "RICS.Anomaly.Blocked.Stage".Translate(
                label,
                StageLabelForTier(requiredTier),
                CurrentMonolithLabel());
        }

        private static string CurrentMonolithLabel()
        {
            try
            {
                return MonolithStageName(Find.Anomaly?.LevelDef);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Anomaly] Monolith label failed: {ex.Message}");
                return "Inactive";
            }
        }

        private static AnomalyPlaystyleDef CurrentPlaystyle()
        {
            return Find.Storyteller?.difficulty?.AnomalyPlaystyleDef;
        }

        private static bool ContentEnabled()
        {
            AnomalyPlaystyleDef playstyle = CurrentPlaystyle();
            return playstyle == null || playstyle.enableAnomalyContent;
        }

        private static bool StagingOn()
        {
            return CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.AnomalyStagingEnabled ?? true;
        }

        private static bool BlockEventsWhenDisabled()
        {
            return CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.AnomalyBlockEventsWhenDisabled ?? true;
        }

        private static string LabelOf(string label, string fallback)
        {
            string name = string.IsNullOrEmpty(label) ? fallback : label;
            if (string.IsNullOrEmpty(name))
                return "That";

            return name.CapitalizeFirst();
        }
    }
}
