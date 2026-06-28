// EventsDefInfoWindow.cs
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
// A window showing detailed information about an incident/event
using CAP_ChatInteractive.Incidents;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class EventsDefInfoWindow : Window
    {
        private BuyableIncident incident;
        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<string, string> textBuffers = new Dictionary<string, string>();

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public EventsDefInfoWindow(BuyableIncident incident)
        {
            this.incident = incident;
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
            Widgets.Label(titleRect, $"Incident Information: {incident.Label}");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Label edit box with spacing
            float labelEditHeight = 40f;
            Rect labelEditRect = new Rect(0f, 40f, inRect.width, labelEditHeight);
            DrawLabelEditBox(labelEditRect);

            // Content area (moved down to accommodate label edit box)
            float contentStartY = 40f + labelEditHeight + 10f; // 10f spacing
            Rect contentRect = new Rect(0f, contentStartY, inRect.width, inRect.height - contentStartY - CloseButSize.y);
            DrawIncidentInfo(contentRect);
        }

        private void DrawIncidentInfo(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            // Always show at top
            sb.AppendLine($"DefName: {incident.DefName}");
            sb.AppendLine($"Label: {incident.Label}");
            sb.AppendLine($"Mod Source: {GetDisplayModName(incident.ModSource)}");
            sb.AppendLine($"Category: {GetDisplayCategoryName(incident.CategoryName)}");
            sb.AppendLine($"");

            // BuyableIncident properties
            sb.AppendLine($"--- Buyable Incident Properties ---");
            sb.AppendLine($"Base Cost: {incident.BaseCost}");
            sb.AppendLine($"Karma Type: {incident.KarmaType}");
            sb.AppendLine($"Enabled: {incident.Enabled}");
            sb.AppendLine($"Event Cap: {incident.EventCap}");
            sb.AppendLine($"Is Available For Commands: {incident.IsAvailableForCommands}");
            sb.AppendLine($"Should Be In Store: {incident.ShouldBeInStore}");
            sb.AppendLine($"Worker Class: {incident.WorkerClassName}");
            sb.AppendLine($"");

            // Incident type analysis
            sb.AppendLine($"--- Incident Type Analysis ---");
            sb.AppendLine($"Is Weather Incident: {incident.IsWeatherIncident}");
            sb.AppendLine($"Is Raid Incident: {incident.IsRaidIncident}");
            sb.AppendLine($"Is Disease Incident: {incident.IsDiseaseIncident}");
            sb.AppendLine($"Is Quest Incident: {incident.IsQuestIncident}");
            sb.AppendLine($"Points Scaleable: {incident.PointsScaleable}");
            sb.AppendLine($"Base Chance: {incident.BaseChance}");
            sb.AppendLine($"Min Threat Points: {incident.MinThreatPoints}");
            sb.AppendLine($"Max Threat Points: {incident.MaxThreatPoints}");
            sb.AppendLine($"");

            // Get the actual IncidentDef for more detailed information
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);
            if (incidentDef != null)
            {
                sb.AppendLine($"--- IncidentDef Properties ---");
                sb.AppendLine($"Category: {incidentDef.category?.defName ?? "null"}");
                sb.AppendLine($"Category Label: {incidentDef.category?.LabelCap ?? "null"}");
                sb.AppendLine($"Base Chance: {incidentDef.baseChance}");
                sb.AppendLine($"Base Chance With Royalty: {incidentDef.baseChanceWithRoyalty}");
                sb.AppendLine($"Earliest Day: {incidentDef.earliestDay}");
                sb.AppendLine($"Min Population: {incidentDef.minPopulation}");
                sb.AppendLine($"Points Scaleable: {incidentDef.pointsScaleable}");
                sb.AppendLine($"Min Threat Points: {incidentDef.minThreatPoints}");
                sb.AppendLine($"Max Threat Points: {incidentDef.maxThreatPoints}");
                sb.AppendLine($"Min Refire Days: {incidentDef.minRefireDays}");
                sb.AppendLine($"Hidden: {incidentDef.hidden}");
                sb.AppendLine($"Is Anomaly Incident: {incidentDef.IsAnomalyIncident}");

                // Target tags
                if (incidentDef.targetTags != null && incidentDef.targetTags.Count > 0)
                {
                    sb.AppendLine($"Target Tags: {string.Join(", ", incidentDef.targetTags.Select(t => t.defName))}");
                }

                // Letter information with karma analysis
                if (incidentDef.letterDef != null)
                {
                    sb.AppendLine($"Letter Type: {incidentDef.letterDef.defName}");
                    string letterDefName = incidentDef.letterDef.defName.ToLower();
                    if (letterDefName.Contains("positive") || letterDefName.Contains("good"))
                        sb.AppendLine($"Letter Karma: Good (based on letter type)");
                    else if (letterDefName.Contains("negative") || letterDefName.Contains("bad") || letterDefName.Contains("threat"))
                        sb.AppendLine($"Letter Karma: Bad (based on letter type)");
                    else
                        sb.AppendLine($"Letter Karma: Neutral (based on letter type)");
                }

                // Game condition if applicable
                if (incidentDef.gameCondition != null)
                {
                    sb.AppendLine($"Game Condition: {incidentDef.gameCondition.defName}");
                    sb.AppendLine($"Duration Days: {incidentDef.durationDays}");
                }

                // Disease incident if applicable
                if (incidentDef.diseaseIncident != null)
                {
                    sb.AppendLine($"Disease: {incidentDef.diseaseIncident.defName}");
                    sb.AppendLine($"Disease Max Victims: {incidentDef.diseaseMaxVictims}");
                }

                // Quest incident if applicable
                if (incidentDef.questScriptDef != null)
                {
                    sb.AppendLine($"--- Quest Information ---");
                    sb.AppendLine($"Quest Script: {incidentDef.questScriptDef.defName}");
                    sb.AppendLine($"Auto Accept: {incidentDef.questScriptDef.autoAccept}");
                    sb.AppendLine($"Randomly Selectable: {incidentDef.questScriptDef.randomlySelectable}");
                    sb.AppendLine($"Root Min Points: {incidentDef.questScriptDef.rootMinPoints}");
                    sb.AppendLine($"Root Earliest Day: {incidentDef.questScriptDef.rootEarliestDay}");
                }
            }
            else
            {
                sb.AppendLine($"--- IncidentDef Not Found ---");
                sb.AppendLine($"Could not find IncidentDef with name: {incident.DefName}");
            }

            string fullText = sb.ToString();

            // Calculate text height
            float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

            // Scroll view
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
            Widgets.EndScrollView();
        }

        private void DrawLabelEditBox(Rect rect)
        {
            Widgets.BeginGroup(rect);

            try
            {
                // Background for the edit area
                Widgets.DrawMenuSection(new Rect(0f, 0f, rect.width, rect.height));

                float padding = 10f;
                float labelWidth = 60f;
                float inputWidth = 200f; // Reduced from dynamic width to fixed 200f (about 1/3 of previous)
                float buttonWidth = 60f;

                // Label
                Rect labelRect = new Rect(padding, padding, labelWidth, 30f);
                Widgets.Label(labelRect, "RICS.EventsDefInfo.Label".Translate());

                // Text input field - narrower
                Rect inputRect = new Rect(labelRect.xMax + padding, padding, inputWidth, 30f);


                // Use buffer for text field
                string bufferKey = $"Label_{incident.DefName}";
                if (!textBuffers.ContainsKey(bufferKey))
                {
                    textBuffers[bufferKey] = incident.Label ?? string.Empty;
                }

                string buffer = textBuffers[bufferKey];
                buffer = Widgets.TextField(inputRect, buffer);
                textBuffers[bufferKey] = buffer;

                string newLabel = buffer;

                // Only update if changed
                if (newLabel != incident.Label)
                {
                    // Save button
                    Rect saveButtonRect = new Rect(inputRect.xMax + padding, padding, buttonWidth, 30f);
                    if (Widgets.ButtonText(saveButtonRect, "RICS.EventsDefInfo.Save".Translate()))
                    {
                        incident.Label = newLabel;
                        IncidentsManager.SaveIncidentsToJson();
                        Messages.Message($"Label updated for {incident.DefName}", MessageTypeDefOf.TaskCompletion);
                    }

                    // Optional: Cancel/reset button
                    Rect cancelButtonRect = new Rect(saveButtonRect.xMax + 5f, padding, buttonWidth, 30f);
                    if (Widgets.ButtonText(cancelButtonRect, "RICS.EventsDefInfo.Reset".Translate()))
                    {
                        // Reset to original label from IncidentDef if available
                        IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);
                        if (incidentDef != null)
                        {
                            incident.Label = incidentDef.label;
                            IncidentsManager.SaveIncidentsToJson();
                            Messages.Message($"Label reset to default for {incident.DefName}", MessageTypeDefOf.TaskCompletion);
                        }
                    }
                }
                else
                {
                    // Show info when no changes - now has enough space
                    Rect infoRect = new Rect(inputRect.xMax + padding, padding, 120f, 30f);
                    GUI.color = Color.gray;
                    Widgets.Label(infoRect, "RICS.EventsDefInfo.EditToSave".Translate());
                    GUI.color = Color.white;
                }

                // Tooltip explaining this feature
                TooltipHandler.TipRegion(new Rect(padding, padding, rect.width - padding * 2, 30f),
                    "RICS.EventsDefInfo.LabelTooltip".Translate());
            }
            finally
            {
                Widgets.EndGroup();
            }
        }

        // Helper methods to match the main dialog
        private string GetDisplayModName(string modSource)
        {
            if (modSource == "Core") return "RICS.EventsDefInfo.RimWorld".Translate();
            if (modSource.Contains(".")) return modSource.Split('.')[0];
            return modSource;
        }

        private string GetDisplayCategoryName(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return "RICS.EventsDefInfo.Uncategorized".Translate();
            return categoryName;
        }
    }
}