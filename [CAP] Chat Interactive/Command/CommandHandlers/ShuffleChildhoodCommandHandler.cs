// ShuffleChildhoodCommandHandler.cs
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
// !shufflechildhood — randomize childhood backstory (race/xenotype aware)
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class ShuffleChildhoodCommandHandler
    {
        private const string ReturnDivider = " | ";

        internal static string HandleShuffledChildhood(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.MPCH.NoViewerData".Translate();

                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                if (pawn == null)
                    return "RICS.Pawn.NoPawn".Translate();

                if (pawn.Destroyed || pawn.Dead)
                {
                    var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);
                    return "RICS.Pawn.Dead".Translate()
                           + ReturnDivider
                           + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
                }

                if (pawn.story == null)
                    return "RICS.CHCH.NoStoryTracker".Translate();

                if (pawn.story.Childhood == null)
                    return "RICS.CHCH.NoChildhood".Translate();

                var cmdSettings = CommandSettingsManager.GetSettings("shufflechildhood");
                int cost = cmdSettings.GetCustom("childhoodWager", 1000);

                if (viewer.Coins < cost)
                    return "RICS.CHCH.InsufficientCoins".Translate(cost);

                BackstoryDef current = pawn.story.Childhood;

                if (current.defName == "Colonist" ||
                    (current.label?.ToLowerInvariant().Contains("colonist") ?? false) ||
                    (current.titleShort?.ToLowerInvariant().Contains("colonist") ?? false))
                {
                    return "RICS.CHCH.ColonistBackstory".Translate();
                }

                List<BackstoryDef> valid = GetCompatibleChildhoodBackstories(pawn);
                valid.RemoveAll(bs => bs == current);

                if (valid.Count == 0)
                    return "RICS.CHCH.NoValidAlternatives".Translate();

                BackstoryDef newBackstory = valid.RandomElement();
                pawn.story.Childhood = newBackstory;
                BackstoryUtility.RestoreBackstoryEffects(pawn, current, newBackstory);

                viewer.TakeCoins(cost);

                string oldLabel = MyPawnCommandHandler.StripTags(
                    current.TitleCapFor(pawn.gender) ?? current.title ?? current.defName);
                string newLabel = MyPawnCommandHandler.StripTags(
                    newBackstory.TitleCapFor(pawn.gender) ?? newBackstory.title ?? newBackstory.defName);

                var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                string currency = globalSettings?.CurrencyName?.Trim() ?? "¢";
                string coinDisplay = $"{cost} {currency}";

                // ChildBackstoryShuffled lives under ADCH key (shared OldToNew style with adulthood)
                return "RICS.ADCH.ChildBackstoryShuffled".Translate(coinDisplay)
                       + ReturnDivider
                       + "RICS.ADCH.OldToNew".Translate(oldLabel, newLabel);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ShuffleChildhood] Error in HandleShuffledChildhood: {ex}");
                return "RICS.CHCH.GenericError".Translate();
            }
        }

        private static List<BackstoryDef> GetCompatibleChildhoodBackstories(Verse.Pawn pawn)
        {
            var result = new List<BackstoryDef>();

            foreach (BackstoryDef bs in DefDatabase<BackstoryDef>.AllDefsListForReading)
            {
                if (bs.slot != BackstorySlot.Childhood)
                    continue;

                if (IsBackstoryCompatibleWithPawn(bs, pawn))
                    result.Add(bs);
            }

            return result;
        }

        private static bool IsBackstoryCompatibleWithPawn(BackstoryDef bs, Verse.Pawn pawn)
        {
            var provider = CAPChatInteractiveMod.Instance?.AlienProvider;
            if (provider != null)
                return provider.IsBackstoryAllowed(bs, pawn);

            return true;
        }
    }
}
