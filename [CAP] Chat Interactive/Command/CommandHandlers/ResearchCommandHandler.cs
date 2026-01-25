// ViewerCommands.cs
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
// Commands that viewers can use to interact with the game
using System;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal class ResearchCommandHandler
    {
        internal static string HandleResearchCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                // If no arguments, show current research
                if (args.Length == 0)
                {
                    return GetCurrentResearchStatus();
                }

                // If arguments provided, search for specific research project
                string researchName = string.Join(" ", args).Trim();
                return GetSpecificResearchStatus(researchName);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in research command: {ex}");
                return "An error occurred while processing the research command.";
            }
        }

        private static string GetCurrentResearchStatus()
        {
            var researchManager = Find.ResearchManager;
            var currentProject = researchManager.GetProject();

            if (currentProject == null)
            {
                return "No research project is currently selected.";
            }

            float progressPercent = currentProject.ProgressApparent;
            float totalCost = currentProject.CostApparent;
            float remainingCost = totalCost - progressPercent;

            return $"Current research: {currentProject.LabelCap} - {progressPercent:F0}/{totalCost:F0} ({progressPercent / totalCost * 100:F1}% complete)";
        }

        private static string GetSpecificResearchStatus(string researchName)
        {
            // Find research project by name
            var allResearch = DefDatabase<ResearchProjectDef>.AllDefs;
            var matchingProjects = allResearch.Where(r =>
                r.LabelCap.ToString().ToLower().Contains(researchName.ToLower()) ||
                r.defName.ToLower().Contains(researchName.ToLower())
            ).ToList();

            if (matchingProjects.Count == 0)
            {
                return $"No research project found matching '{researchName}'. Use !research without arguments to see current project.";
            }

            if (matchingProjects.Count > 1)
            {
                var projectNames = string.Join(", ", matchingProjects.Take(3).Select(p => p.LabelCap));
                return $"Multiple projects match '{researchName}': {projectNames}" + (matchingProjects.Count > 3 ? "..." : "");
            }

            var project = matchingProjects[0];
            var researchManager = Find.ResearchManager;

            if (project.IsFinished)
            {
                return $"{project.LabelCap} - COMPLETED";
            }

            float progress = researchManager.GetProgress(project);
            float totalCost = project.CostApparent;
            float progressPercent = totalCost > 0 ? (progress / totalCost) * 100 : 0;

            string status = project.CanStartNow ? "Available" : "Locked (prerequisites missing)";

            return $"{project.LabelCap} - {progress:F0}/{totalCost:F0} ({progressPercent:F1}% complete) - {status}";
        }

        internal static string HandleStudyCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var research = Find.ResearchManager;
            if (research == null)
                return "No research manager.";

            var projects = research.CurrentAnomalyKnowledgeProjects
                ?.Select(a => a.project)
                .Where(p => p != null && p.knowledgeCategory != null)
                .ToList();

            if (projects == null || projects.Count == 0)
                return "No active anomaly research.";

            var basic = projects
                .FirstOrDefault(p => p.knowledgeCategory.overflowCategory == null);

            var advanced = projects
                .FirstOrDefault(p => p.knowledgeCategory.overflowCategory != null);

            string bas = basic != null
                ? $"{basic.LabelCap} - {basic.ProgressApparent}/{basic.CostApparent} ({basic.ProgressPercent}%)"
                : "none";

            string adv = advanced != null
                ? $"{advanced.LabelCap} - {advanced.ProgressApparent}/{advanced.CostApparent} ({advanced.ProgressPercent}%)"
                : "none";

            return $"Basic: {bas} | Advanced: {adv}";
        }
    }
}
