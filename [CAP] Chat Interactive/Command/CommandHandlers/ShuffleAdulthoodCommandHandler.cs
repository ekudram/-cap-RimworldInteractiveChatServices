// File: ShuffleAdulthoodCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !shuffleadulthood — randomize adulthood backstory (race/xenotype aware)
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class ShuffleAdulthoodCommandHandler
    {
        private const string ReturnDivider = " | ";

        internal static string HandleShuffledAdulthood(ChatMessageWrapper messageWrapper, string[] args)
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
                    return "RICS.ADCH.NoStoryTracker".Translate();

                if (pawn.story.Adulthood == null)
                    return "RICS.ADCH.NoAdulthood".Translate();

                var cmdSettings = CommandSettingsManager.GetSettings("shuffleadulthood");
                int cost = cmdSettings.GetCustom("adulthoodWager", 1000);

                if (viewer.Coins < cost)
                    return "RICS.ADCH.InsufficientCoins".Translate(cost);

                BackstoryDef current = pawn.story.Adulthood;

                if (current.defName == "Colonist" ||
                    (current.label?.ToLowerInvariant().Contains("colonist") ?? false) ||
                    (current.titleShort?.ToLowerInvariant().Contains("colonist") ?? false))
                {
                    return "RICS.CHCH.ColonistBackstory".Translate();
                }

                List<BackstoryDef> valid = GetCompatibleAdulthoodBackstories(pawn);
                valid.RemoveAll(bs => bs == current);

                if (valid.Count == 0)
                    return "RICS.ADCH.NoValidAlternatives".Translate();

                BackstoryDef newBackstory = valid.RandomElement();
                pawn.story.Adulthood = newBackstory;
                BackstoryUtility.RestoreBackstoryEffects(pawn, current, newBackstory);

                viewer.TakeCoins(cost);

                string oldLabel = MyPawnCommandHandler.StripTags(
                    current.TitleCapFor(pawn.gender) ?? current.title ?? current.defName);
                string newLabel = MyPawnCommandHandler.StripTags(
                    newBackstory.TitleCapFor(pawn.gender) ?? newBackstory.title ?? newBackstory.defName);

                var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                string currency = globalSettings?.CurrencyName?.Trim() ?? "¢";
                string coinDisplay = $"{cost} {currency}";

                return "RICS.ADCH.AdultBackstoryShuffled".Translate(coinDisplay)
                       + ReturnDivider
                       + "RICS.ADCH.OldToNew".Translate(oldLabel, newLabel);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ShuffleAdulthood] Error in HandleShuffledAdulthood: {ex}");
                return "RICS.ADCH.GenericError".Translate();
            }
        }

        private static List<BackstoryDef> GetCompatibleAdulthoodBackstories(Verse.Pawn pawn)
        {
            var result = new List<BackstoryDef>();

            foreach (BackstoryDef bs in DefDatabase<BackstoryDef>.AllDefsListForReading)
            {
                if (bs.slot != BackstorySlot.Adulthood)
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
