// File: Patch_ResearchManager_FinishProject.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// One fact notice when a project finishes. Masie writes her own line.
// Priority.Last so a research-vote addon can fill the slot first.

using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.AI
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ResearchProjectDef proj, Pawn researcher)
        {
            try
            {
                if (proj == null)
                    return;

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.AIChatBotActive)
                    return;

                string botName = string.IsNullOrEmpty(settings.AIChatBotName) ? "Masie" : settings.AIChatBotName;
                string label = proj.LabelCap.ToString();
                string researcherName = "none";
                if (researcher != null)
                {
                    if (!researcher.LabelShort.NullOrEmpty())
                        researcherName = researcher.LabelShort;
                    else if (researcher.Name != null)
                        researcherName = researcher.Name.ToStringShort;
                }

                string blurb = "";
                if (!proj.description.NullOrEmpty())
                {
                    blurb = ChatCommandProcessor.RemoveMarkupTags(proj.description);
                    blurb = Regex.Replace(blurb, @"\s+", " ").Trim();
                    if (blurb.Length > 180)
                        blurb = blurb.Substring(0, 180).Trim() + "...";
                }

                string slot = "empty";
                ResearchProjectDef current = Find.ResearchManager?.GetProject();
                if (current != null && current != proj)
                    slot = current.LabelCap.ToString();

                string message =
                    botName + " this has occurred in the colony: RESEARCH_FINISHED\n"
                    + "Finished: " + label + "\n"
                    + "Def: " + proj.defName + "\n"
                    + "Researcher: " + researcherName + "\n"
                    + "Blurb: " + blurb + "\n"
                    + "Slot: " + slot;

                Current.Game?.GetComponent<CAPChatInteractive_GameComponent>()
                    ?._aiChatBotService?.NotifyColonyEvent(message);
            }
            catch (Exception ex)
            {
                Logger.Warning("Research finished notice failed: " + ex.Message);
            }
        }
    }
}
