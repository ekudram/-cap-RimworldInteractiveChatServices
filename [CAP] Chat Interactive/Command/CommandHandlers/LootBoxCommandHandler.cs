// LootBoxCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.

using System;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal class LootBoxCommandHandler
    {
        internal static string HandleLootboxCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var lootboxComponent = Current.Game?.GetComponent<LootBoxComponent>();
            if (lootboxComponent == null)
                return "Lootbox system is not available.";

            // Process viewer message to check for daily lootboxes
            lootboxComponent.ProcessViewerMessage(messageWrapper.Username);

            if (args.Length > 0 && args[0].ToLower() == "count")
            {
                return HandleLootboxCountCommand(messageWrapper, lootboxComponent);
            }

            return HandleOpenLootboxCommand(messageWrapper, lootboxComponent);
        }

        private static string HandleLootboxCountCommand(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent)
        {
            int count = lootboxComponent.HowManyLootboxesDoesViewerHave(messageWrapper.Username);
            string plural = count != 1 ? "es" : "";
            return $"@{messageWrapper.Username} you currently have {count} lootbox{plural}.";
        }

        private static string HandleOpenLootboxCommand(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
                return "Lootbox settings not available.";

            if (settings.LootBoxForceOpenAllAtOnce)
            {
                return OpenAllLootboxes(messageWrapper, lootboxComponent, settings);
            }
            else
            {
                return OpenSingleLootbox(messageWrapper, lootboxComponent, settings);
            }
        }

        private static string OpenSingleLootbox(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent, CAPGlobalChatSettings settings)
        {
            if (lootboxComponent.HowManyLootboxesDoesViewerHave(messageWrapper.Username) > 0)
            {
                int coins = Rand.Range(settings.LootBoxRandomCoinRange.min, settings.LootBoxRandomCoinRange.max);
                Viewer viewer = Viewers.GetViewer(messageWrapper);
                viewer.GiveCoins(coins);
                lootboxComponent.ViewersLootboxes[viewer.Username]--;

                return $"@{messageWrapper.Username} you open a lootbox and discover: {coins} coins!";
            }
            else
            {
                return $"@{messageWrapper.Username} you do not have any lootboxes.";
            }
        }

        private static string OpenAllLootboxes(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent, CAPGlobalChatSettings settings)
        {
            int lootboxCount = lootboxComponent.HowManyLootboxesDoesViewerHave(messageWrapper.Username);
            if (lootboxCount <= 0)
                return $"@{messageWrapper.Username} you do not have any lootboxes.";

            int totalCoins = 0;
            for (int i = 0; i < lootboxCount; i++)
            {
                totalCoins += Rand.Range(settings.LootBoxRandomCoinRange.min, settings.LootBoxRandomCoinRange.max);
            }

            Viewer viewer = Viewers.GetViewer(messageWrapper);
            viewer.GiveCoins(totalCoins);
            lootboxComponent.ViewersLootboxes[viewer.Username] = 0;

            return $"@{messageWrapper.Username} you open all your lootboxes and discover: {totalCoins} coins!";
        }
    }
}