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

using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
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

            // Special-case Colonist backstory (born in the colony)
            if (current.defName == "Colonist" ||
                (current.label?.ToLowerInvariant().Contains("colonist") ?? false) ||
                (current.titleShort?.ToLowerInvariant().Contains("colonist") ?? false))
            {
                return "RICS.CHCH.ColonistBackstory".Translate();
            }

            // Get compatible adulthood backstories (race + xenotype restrictions respected via DefDatabase filter + HAR)
            List<BackstoryDef> valid = GetCompatibleAdulthoodBackstories(pawn);

            // Remove current to prevent no-op
            valid.RemoveAll(bs => bs == current);

            if (valid.Count == 0)
            {
                return "RICS.ADCH.NoValidAlternatives".Translate();
            }

            // Shuffle & apply (RimWorld limitation: stats/skills are not retroactively adjusted)
            BackstoryDef newBackstory = valid.RandomElement();
            pawn.story.Adulthood = newBackstory;

            // Restore any work types that are no longer blocked (fixes firefighting, etc.)
            BackstoryUtility.RestoreBackstoryEffects(pawn, current, newBackstory);

            // Deduct cost (viewer data is persisted via GameComponent / Viewers static save)
            viewer.Coins -= cost;

            // Build viewer-friendly response with safe StripTags
            var report = new StringBuilder();

            // Use TitleCapFor(pawn.gender) — this is the correct vanilla way (handles gender variants + translation)
            string oldLabel = current != null
                            ? MyPawnCommandHandler.StripTags(current.TitleCapFor(pawn.gender) ?? current.title ?? current.defName)
                            : "Unknown";

            string newLabel = newBackstory != null
                ? MyPawnCommandHandler.StripTags(newBackstory.TitleCapFor(pawn.gender) ?? newBackstory.title ?? newBackstory.defName)
                : "Unknown";

            // Add space between cost and currency symbol for readability
            string coinDisplay = $"{cost} {settings.CurrencyName.Trim()}";

            // report.AppendLine($"🎒 Adulthood backstory shuffled for {coinDisplay}!");
            report.AppendLine("RICS.ADCH.AdultBackstoryShuffled".Translate(coinDisplay));
            // report.AppendLine($"Old: {oldLabel} → New: {newLabel}");
            report.AppendLine("RICS.ADCH.OldToNew".Translate(oldLabel, newLabel));


            Logger.Debug($"[ShuffleAdulthood] Success - {oldLabel} → {newLabel} for viewer {viewer?.Username}");

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