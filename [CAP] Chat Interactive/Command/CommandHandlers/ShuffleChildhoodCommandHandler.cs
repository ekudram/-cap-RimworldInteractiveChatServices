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
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal class ShuffleChildhoodCommandHandler
    {
        internal static string HandleShuffledChildhood(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Get the viewer and their pawn (reusing existing manager pattern from other handlers)
            var viewer = Viewers.GetViewer(messageWrapper);
            if (viewer == null)
            {
                return "RICS.MPCH.NoViewerData".Translate(); // Translation key: RICS.MPCH.NoViewerData
            }

            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            if (assignmentManager == null)
            {
                return "RICS.MPCH.NoPawnAssigned".Translate(); // Translation key: RICS.MPCH.NoPawnAssigned
            }

            Verse.Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);
            if (pawn == null || pawn.story == null)
            {
                return "RICS.CHCH.NoStoryTracker".Translate(); // Translation key: RICS.CHCH.NoStoryTracker – "Pawn has no story component."
            }

            if (pawn.story.Childhood == null)
            {
                return "RICS.CHCH.NoChildhood".Translate(); // Translation key: RICS.CHCH.NoChildhood – "You have no childhood backstory to shuffle."
            }

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            int cost = settings.ChildhoodWager;

            // Coin check (Viewer.Coins is the standard economy field in RICS)
            if (viewer.Coins < cost)
            {
                return "RICS.CHCH.InsufficientCoins".Translate(cost); // Translation key: RICS.CHCH.InsufficientCoins – "You need {0} coins to shuffle your childhood backstory."
            }

            BackstoryDef current = pawn.story.Childhood;

            // Special-case Colonist backstory (born in the colony – has no disabled jobs)
            if (current.defName == "Colonist" ||
                (current.label?.ToLowerInvariant().Contains("colonist") ?? false) ||
                (current.titleShort?.ToLowerInvariant().Contains("colonist") ?? false))
            {
                return "RICS.CHCH.ColonistBackstory".Translate();
            }

            // Must have disabled jobs (core purpose of the command)
            if (current.workDisables == WorkTags.None)
            {
                return "RICS.CHCH.NoDisabledJobs".Translate();
            }

            // Get compatible childhood backstories (race + xenotype restrictions respected via DefDatabase filter)
            List<BackstoryDef> valid = GetCompatibleChildhoodBackstories(pawn);

            // Remove current to prevent no-op
            valid.RemoveAll(bs => bs == current);

            if (valid.Count == 0)
            {
                return "RICS.CHCH.NoValidAlternatives".Translate();
            }

            // Shuffle & apply (RimWorld limitation: stats/skills are not retroactively adjusted)
            BackstoryDef newBackstory = valid.RandomElement();
            pawn.story.Childhood = newBackstory;

            // Deduct cost (viewer data is persisted via GameComponent / Viewers static save)
            viewer.Coins -= cost;

            // Build viewer-friendly response with safe StripTags
            var report = new StringBuilder();
            string oldLabel = current?.label != null ? MyPawnCommandHandler.StripTags(current.label) : "Unknown";
            string newLabel = newBackstory?.label != null ? MyPawnCommandHandler.StripTags(newBackstory.label) : "Unknown";

            report.AppendLine($"🎒 Childhood backstory shuffled for {cost}{settings.CurrencyName}!");
            report.AppendLine($"Old: {oldLabel} → New: {newLabel}");

            Logger.Debug($"[ShuffleChildhood] Success - {oldLabel} → {newLabel} for viewer {viewer?.Username}");

            return report.ToString();
        }

        // Helper – uses vanilla RimWorld DefDatabase + race/xenotype filtering (now fully HAR-aware)
        private static List<BackstoryDef> GetCompatibleChildhoodBackstories(Verse.Pawn pawn)
        {
            List<BackstoryDef> result = new List<BackstoryDef>();

            foreach (BackstoryDef bs in DefDatabase<BackstoryDef>.AllDefsListForReading)
            {
                if (bs.slot != BackstorySlot.Childhood) continue;

                if (IsBackstoryCompatibleWithPawn(bs, pawn))
                {
                    result.Add(bs);
                }
            }

            return result;
        }

        private static bool IsBackstoryCompatibleWithPawn(BackstoryDef bs, Verse.Pawn pawn)
        {
            // Use HAR provider when loaded (respects AlienBackstoryDef.Approved(Pawn) for race/gender/age/xenotype)
            var provider = CAPChatInteractiveMod.Instance?.AlienProvider;
            if (provider != null)
            {
                return provider.IsBackstoryAllowed(bs, pawn);
            }

            // Vanilla RimWorld behavior – all childhood backstories are allowed
            return true;
        }
    }
}