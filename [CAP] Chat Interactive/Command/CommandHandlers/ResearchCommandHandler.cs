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
                return "RICS.Research.Error".Translate();
            }
        }

        private static string GetCurrentResearchStatus()
        {
            var researchManager = Find.ResearchManager;
            var currentProject = researchManager.GetProject();  // or .CurrentProject if 1.5+

            if (currentProject == null)
            {
                return "RICS.Research.NoArgsCurrent".Translate();
            }

            float progress = Math.Max(0f, currentProject.ProgressApparent);
            float cost = Math.Max(1f, currentProject.CostApparent);

            if (float.IsNaN(progress) || float.IsInfinity(progress)) progress = 0f;
            if (float.IsNaN(cost) || float.IsInfinity(cost)) cost = 0f;

            float percent = (progress / cost) * 100f;

            // Log as before
            Logger.Debug($"Current: {currentProject.LabelCap} - raw progress {currentProject.ProgressApparent} / {currentProject.CostApparent} → clamped {progress}/{cost} → {percent:F1}%");

            // Format numbers manually with .ToString("F0") or "0"
            string progStr = progress.ToString("F0");   // or just progress > 0.1f ? progress.ToString("F0") : "0"
            string costStr = cost.ToString("F0");
            string percStr = percent.ToString("F1");

            return "RICS.Research.CurrentStatus".Translate(
                currentProject.LabelCap,
                progStr,
                costStr,
                percStr
            );
        }

        private static string GetSpecificResearchStatus(string researchName)
        {
            var allResearch = DefDatabase<ResearchProjectDef>.AllDefs;
            string inputLower = researchName.ToLower().Trim();

            // 1. Try exact matches first (case-insensitive)
            var exactMatches = allResearch
                .Where(r =>
                    string.Equals(r.LabelCap.ToString(), inputLower, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.defName, inputLower, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exactMatches.Count == 1)
            {
                return GetProjectStatusString(exactMatches[0]);
            }

            if (exactMatches.Count > 1)
            {
                var names = string.Join(", ", exactMatches.Select(p => p.LabelCap));
                return $"Multiple exact matches for '{researchName}': {names}";
            }

            // 2. No exact → fall back to partial contains
            var partialMatches = allResearch
                .Where(r =>
                    r.LabelCap.ToString().ToLower().Contains(inputLower) ||
                    r.defName.ToLower().Contains(inputLower))
                .ToList();

            if (partialMatches.Count == 0)
            {
                return "RICS.Research.NoMatch".Translate(researchName);
            }

            if (partialMatches.Count > 1)
            {
                var names = string.Join(", ", partialMatches.Take(3).Select(p => p.LabelCap));
                string ellipsis = partialMatches.Count > 3 ? "RICS.Research.MultipleEllipsis".Translate() : "";
                return "RICS.Research.MultipleMatches".Translate(researchName, names, ellipsis);
            }

            // Single partial match
            return GetProjectStatusString(partialMatches[0]);
        }

        private static string GetProjectStatusString(ResearchProjectDef project)
        {
            var researchManager = Find.ResearchManager;

            if (project.IsFinished)
            {
                return "RICS.Research.Completed".Translate(project.LabelCap);
            }

            // Defensive handling
            float rawProgress = project.ProgressApparent;
            float rawCost = project.CostApparent;

            float progress = Math.Max(0f, rawProgress);
            float cost = Math.Max(1f, rawCost);

            if (float.IsNaN(progress) || float.IsInfinity(progress)) progress = 0f;
            if (float.IsNaN(cost) || float.IsInfinity(cost)) cost = 1f;

            float percent = (progress / cost) * 100f;

            // Pre-format as strings (same as current research)
            string progStr = progress.ToString("F0");
            string costStr = cost.ToString("F0");
            string percStr = percent.ToString("F1");  // keeps one decimal like 5.6

            string status = project.CanStartNow
                ? "RICS.Research.StatusAvailable".Translate()
                : "RICS.Research.StatusLocked".Translate();

            return "RICS.Research.SpecificStatus".Translate(
                project.LabelCap,
                progStr,
                costStr,
                percStr,
                status
            );
        }

        internal static string HandleStudyCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var research = Find.ResearchManager;
            if (research == null)
                return "RICS.Research.NoResearchManager".Translate();

            var projects = research.CurrentAnomalyKnowledgeProjects
                ?.Select(a => a.project)
                .Where(p => p != null && p.knowledgeCategory != null)
                .ToList();

            if (projects == null || projects.Count == 0)
                return "RICS.Research.NoActiveAnomaly".Translate();

            var basic = projects
                .FirstOrDefault(p => p.knowledgeCategory.overflowCategory == null);

            var advanced = projects
                .FirstOrDefault(p => p.knowledgeCategory.overflowCategory != null);

            string bas = basic != null
                ? FormatStudyProject(basic)
                : "RICS.Research.StudyNone".Translate();

            string adv = advanced != null
                ? FormatStudyProject(advanced)
                : "RICS.Research.StudyNone".Translate();

            return "RICS.Research.StudyStatus".Translate(bas, adv);
        }

        // New helper to avoid duplication and keep formatting consistent
        private static string FormatStudyProject(ResearchProjectDef project)
        {
            // Use same defensive logic
            float rawProg = project.ProgressApparent;
            float rawCost = project.CostApparent;

            float progress = Math.Max(0f, rawProg);
            float cost = Math.Max(1f, rawCost);

            if (float.IsNaN(progress) || float.IsInfinity(progress)) progress = 0f;
            if (float.IsNaN(cost) || float.IsInfinity(cost)) cost = 1f;

            float percent = (progress / cost) * 100f;

            string progStr = progress.ToString("F2");
            string costStr = cost.ToString("F0");
            string percStr = percent.ToString("F1");

            return "RICS.Research.StudyFormat".Translate(
                project.LabelCap,
                progStr,
                costStr,
                percStr
            );
        }
    }
}