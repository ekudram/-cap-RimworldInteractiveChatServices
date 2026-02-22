// LootBoxCommandHandler.cs
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
                // return "Lootbox system is not available.";
                return "RICS.LBCH.NotAvailable".Translate();

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
            // return $" you currently have {count} lootbox{plural}.";
            return "RICS.LBCH.Count".Translate(count, plural);
        }

        private static string HandleOpenLootboxCommand(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
                // return "Lootbox settings not available.";
                return "RICS.LBCH.SettingsNotAvailable".Translate();

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

                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                // return $" you open a lootbox and discover: {coins} {currencySymbol}!";
                return "RICS.LBCH.Opened".Translate(coins, currencySymbol);
            }
            else
            {
                // return $" you do not have any lootboxes.";
                return "RICS.LBCH.NoLootboxes".Translate();
            }
        }

        private static string OpenAllLootboxes(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent, CAPGlobalChatSettings settings)
        {
            int lootboxCount = lootboxComponent.HowManyLootboxesDoesViewerHave(messageWrapper.Username);
            if (lootboxCount <= 0)
                // return $" you do not have any lootboxes.";
                return "RICS.LBCH.NoLootboxes".Translate();

            int totalCoins = 0;
            for (int i = 0; i < lootboxCount; i++)
            {
                totalCoins += Rand.Range(settings.LootBoxRandomCoinRange.min, settings.LootBoxRandomCoinRange.max);
            }

            Viewer viewer = Viewers.GetViewer(messageWrapper);
            viewer.GiveCoins(totalCoins);
            lootboxComponent.ViewersLootboxes[viewer.Username] = 0;

            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

            // return $"@ you open all your lootboxes and discover: {totalCoins} {currencySymbol}!";
            return "RICS.LBCH.OpenedAll".Translate(totalCoins, currencySymbol);
        }
    }
}