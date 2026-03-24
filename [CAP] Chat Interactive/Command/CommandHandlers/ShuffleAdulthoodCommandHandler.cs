// ShuffleAdulthoodCommandHandler.cs
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
    internal class ShuffleAdulthoodCommandHandler
    {
        internal static string HandleShuffledAdulthood(ChatMessageWrapper messageWrapper, string[] args)
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
                return "RICS.ADCH.NoStoryTracker".Translate(); // Translation key: RICS.ADCH.NoStoryTracker – "Pawn has no story component."
            }

            if (pawn.story.Adulthood == null)
            {
                return "RICS.ADCH.NoAdulthood".Translate(); // Translation key: RICS.ADCH.NoAdulthood – "You have no adulthood backstory to shuffle."
            }

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            int cost = settings.AdulthoodWager;

            // Coin check (Viewer.Coins is the standard economy field in RICS)
            if (viewer.Coins < cost)
            {
                return "RICS.ADCH.InsufficientCoins".Translate(cost); // Translation key: RICS.ADCH.InsufficientCoins – "You need {0} coins to shuffle your adulthood backstory."
            }

            BackstoryDef current = pawn.story.Adulthood;

            // Must have disabled jobs (core purpose of the command – same rule as childhood)
            if (current.workDisables == WorkTags.None)
            {
                return "RICS.ADCH.NoDisabledJobs".Translate(); // Translation key: RICS.ADCH.NoDisabledJobs – "Your current backstory has no disabled jobs to remove."
            }

            // Get compatible adulthood backstories (race + xenotype restrictions respected via DefDatabase filter + HAR)
            List<BackstoryDef> valid = GetCompatibleAdulthoodBackstories(pawn);

            // Remove current to prevent no-op
            valid.RemoveAll(bs => bs == current);

            if (valid.Count == 0)
            {
                return "RICS.ADCH.NoValidAlternatives".Translate(); // Translation key: RICS.ADCH.NoValidAlternatives – "No other valid adulthood backstories available for your pawn's race/xenotype."
            }

            // Shuffle & apply (RimWorld limitation: stats/skills are not retroactively adjusted)
            // Shuffle & apply (RimWorld limitation: stats/skills are not retroactively adjusted)
            BackstoryDef newBackstory = valid.RandomElement();
            pawn.story.Adulthood = newBackstory;

            // Deduct cost (viewer data is persisted via GameComponent / Viewers static save)
            viewer.Coins -= cost;

            // Build viewer-friendly response (reuses StripTags from MyPawnCommandHandler)
            var report = new StringBuilder();
            string oldLabel = MyPawnCommandHandler.StripTags(current.label);
            string newLabel = MyPawnCommandHandler.StripTags(newBackstory.label);
            report.AppendLine($"🎒 Adulthood backstory shuffled for {cost}{settings.CurrencyName}!");
            report.AppendLine($"Old: {oldLabel} → New: {newLabel}");  // WHY: viewers love seeing the before/after on stream

            return report.ToString();
        }

        // Helper – uses vanilla RimWorld DefDatabase + race/xenotype filtering (HAR-aware)
        private static List<BackstoryDef> GetCompatibleAdulthoodBackstories(Verse.Pawn pawn)
        {
            List<BackstoryDef> result = new List<BackstoryDef>();

            foreach (BackstoryDef bs in DefDatabase<BackstoryDef>.AllDefsListForReading)
            {
                if (bs.slot != BackstorySlot.Adulthood) continue;

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

            // Vanilla RimWorld behavior – all adulthood backstories are allowed
            return true;
        }
    }
}