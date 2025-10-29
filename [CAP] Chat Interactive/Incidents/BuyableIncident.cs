// BuyableIncident.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class BuyableIncident
    {
        // Core incident properties
        public string DefName { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string WorkerClassName { get; set; }
        public string CategoryName { get; set; }

        // Purchase settings
        public int BaseCost { get; set; } = 500;
        public string KarmaType { get; set; } = "Neutral";
        public int EventCap { get; set; } = 2;
        public bool Enabled { get; set; } = true;

        // Additional data
        public string ModSource { get; set; } = "RimWorld";
        public int Version { get; set; } = 1;

        // Incident-specific properties
        public bool IsWeatherIncident { get; set; }
        public bool IsRaidIncident { get; set; }
        public bool IsDiseaseIncident { get; set; }
        public bool IsQuestIncident { get; set; }
        public float BaseChance { get; set; }
        public string MinTechLevel { get; set; }
        public bool PointsScaleable { get; set; }
        public float MinThreatPoints { get; set; }
        public float MaxThreatPoints { get; set; }

        public BuyableIncident() { }

        public BuyableIncident(IncidentDef incidentDef)
        {
            DefName = incidentDef.defName;
            Label = incidentDef.label;
            Description = incidentDef.description;
            WorkerClassName = incidentDef.Worker?.GetType()?.Name;
            CategoryName = incidentDef.category?.defName;
            ModSource = incidentDef.modContentPack?.Name ?? "RimWorld";
            BaseChance = incidentDef.baseChance;
            PointsScaleable = incidentDef.pointsScaleable;
            MinThreatPoints = incidentDef.minThreatPoints;
            MaxThreatPoints = incidentDef.maxThreatPoints;

            // Determine incident type
            AnalyzeIncidentType(incidentDef);
            SetDefaultPricing(incidentDef);

            Logger.Debug($"Created incident: {DefName}, Category: {CategoryName}, Worker: {WorkerClassName}");
        }

        private void AnalyzeIncidentType(IncidentDef incidentDef)
        {
            if (incidentDef.Worker == null) return;

            string workerName = incidentDef.Worker.GetType().Name.ToLower();
            string defName = incidentDef.defName.ToLower();
            string categoryName = incidentDef.category?.defName?.ToLower() ?? "";

            IsWeatherIncident = workerName.Contains("weather") || defName.Contains("weather") || categoryName.Contains("weather");
            IsRaidIncident = workerName.Contains("raid") || defName.Contains("raid") || categoryName.Contains("raid");
            IsDiseaseIncident = workerName.Contains("disease") || defName.Contains("sickness") || categoryName.Contains("disease");
            IsQuestIncident = workerName.Contains("quest") || defName.Contains("quest") || categoryName.Contains("quest");

            // Additional checks for specific incident types
            if (incidentDef.diseaseIncident != null)
                IsDiseaseIncident = true;

            if (incidentDef.questScriptDef != null)
                IsQuestIncident = true;
        }

        private void SetDefaultPricing(IncidentDef incidentDef)
        {
            int basePrice = 500;
            float impactFactor = 1.0f;

            // Adjust price based on incident type
            if (IsRaidIncident)
            {
                impactFactor *= 3.0f;
                KarmaType = "Bad";
                EventCap = 1; // Lower cap for raids
            }
            else if (IsWeatherIncident)
            {
                impactFactor *= 1.5f;
                // Determine if weather is good or bad
                if (incidentDef.defName.Contains("Toxic") || incidentDef.defName.Contains("Volcanic") ||
                    incidentDef.defName.Contains("ColdSnap") || incidentDef.defName.Contains("HeatWave"))
                {
                    KarmaType = "Bad";
                    impactFactor *= 2.0f;
                }
                else
                {
                    KarmaType = "Neutral";
                }
            }
            else if (IsDiseaseIncident)
            {
                impactFactor *= 2.0f;
                KarmaType = "Bad";
            }
            else if (IsQuestIncident)
            {
                impactFactor *= 1.2f;
                KarmaType = "Good";
            }

            // Adjust based on threat points for scaleable incidents
            if (PointsScaleable && MaxThreatPoints > 0)
            {
                impactFactor *= (MaxThreatPoints / 1000f);
            }

            // Adjust based on base chance (rarer events = more expensive)
            if (BaseChance > 0)
            {
                impactFactor *= (1.0f / BaseChance) * 0.1f;
            }

            BaseCost = (int)(basePrice * impactFactor);
            BaseCost = Math.Max(100, Math.Min(10000, BaseCost)); // Clamp between 100-10000
        }
    }
}