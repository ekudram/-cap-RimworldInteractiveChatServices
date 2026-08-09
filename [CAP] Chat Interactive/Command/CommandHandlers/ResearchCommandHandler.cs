// ResearchCommandHandler.cs
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
// !research [project] — current or named research status; !study for Anomaly
using System;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class ResearchCommandHandler
    {
        internal static string HandleResearchCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                    return GetCurrentResearchStatus();

                string researchName = string.Join(" ", args).Trim();
                return GetSpecificResearchStatus(researchName);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Research] Error in research command: {ex}");
                return "RICS.Research.Error".Translate();
            }
        }

        private static string GetCurrentResearchStatus()
        {
            var researchManager = Find.ResearchManager;
            var currentProject = researchManager?.GetProject();
            if (currentProject == null)
                return "RICS.Research.NoArgsCurrent".Translate();

            FormatProgress(currentProject, out string progStr, out string costStr, out string percStr);

            return "RICS.Research.CurrentStatus".Translate(
                currentProject.LabelCap,
                progStr,
                costStr,
                percStr);
        }

        private static string GetSpecificResearchStatus(string researchName)
        {
            if (string.IsNullOrWhiteSpace(researchName))
                return GetCurrentResearchStatus();

            var allResearch = DefDatabase<ResearchProjectDef>.AllDefs;
            string inputLower = researchName.Trim().ToLowerInvariant();

            var exactMatches = allResearch
                .Where(r =>
                    string.Equals(r.LabelCap.ToString(), researchName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.defName, researchName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exactMatches.Count == 1)
                return GetProjectStatusString(exactMatches[0]);

            if (exactMatches.Count > 1)
            {
                var names = string.Join(", ", exactMatches.Select(p => p.LabelCap));
                return "RICS.Research.MultipleExactMatches".Translate(researchName, names);
            }

            var partialMatches = allResearch
                .Where(r =>
                    r.LabelCap.ToString().IndexOf(inputLower, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.defName.IndexOf(inputLower, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (partialMatches.Count == 0)
                return "RICS.Research.NoMatch".Translate(researchName);

            if (partialMatches.Count > 1)
            {
                var names = string.Join(", ", partialMatches.Take(3).Select(p => p.LabelCap));
                string ellipsis = partialMatches.Count > 3 ? "RICS.Research.MultipleEllipsis".Translate() : "";
                return "RICS.Research.MultipleMatches".Translate(researchName, names, ellipsis);
            }

            return GetProjectStatusString(partialMatches[0]);
        }

        private static string GetProjectStatusString(ResearchProjectDef project)
        {
            if (project.IsFinished)
                return "RICS.Research.Completed".Translate(project.LabelCap);

            FormatProgress(project, out string progStr, out string costStr, out string percStr);

            string status = project.CanStartNow
                ? "RICS.Research.StatusAvailable".Translate()
                : "RICS.Research.StatusLocked".Translate();

            return "RICS.Research.SpecificStatus".Translate(
                project.LabelCap,
                progStr,
                costStr,
                percStr,
                status);
        }

        internal static string HandleStudyCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
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

                var basic = projects.FirstOrDefault(p => p.knowledgeCategory.overflowCategory == null);
                var advanced = projects.FirstOrDefault(p => p.knowledgeCategory.overflowCategory != null);

                string bas = basic != null
                    ? FormatStudyProject(basic)
                    : "RICS.Research.StudyNone".Translate();

                string adv = advanced != null
                    ? FormatStudyProject(advanced)
                    : "RICS.Research.StudyNone".Translate();

                return "RICS.Research.StudyStatus".Translate(bas, adv);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Research] Error in study command: {ex}");
                return "RICS.Research.Error".Translate();
            }
        }

        private static string FormatStudyProject(ResearchProjectDef project)
        {
            FormatProgress(project, out string progStr, out string costStr, out string percStr, progressDecimals: 2);
            return "RICS.Research.StudyFormat".Translate(
                project.LabelCap,
                progStr,
                costStr,
                percStr);
        }

        private static void FormatProgress(
            ResearchProjectDef project,
            out string progStr,
            out string costStr,
            out string percStr,
            int progressDecimals = 0)
        {
            float progress = Math.Max(0f, project.ProgressApparent);
            float cost = Math.Max(1f, project.CostApparent);

            if (float.IsNaN(progress) || float.IsInfinity(progress)) progress = 0f;
            if (float.IsNaN(cost) || float.IsInfinity(cost)) cost = 1f;

            float percent = (progress / cost) * 100f;
            progStr = progress.ToString(progressDecimals > 0 ? "F" + progressDecimals : "F0");
            costStr = cost.ToString("F0");
            percStr = percent.ToString("F1");
        }
    }
}
