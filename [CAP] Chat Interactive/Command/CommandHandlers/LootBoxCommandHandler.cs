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
//
// !openlootbox / lootbox count — daily lootbox open and balance
using System;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class LootBoxCommandHandler
    {
        internal static string HandleLootboxCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                var lootboxComponent = Current.Game?.GetComponent<LootBoxComponent>();
                if (lootboxComponent == null)
                    return "RICS.LBCH.NotAvailable".Translate();

                // Award daily lootboxes if eligible (side effect of chat activity)
                lootboxComponent.ProcessViewerMessage(messageWrapper);

                args = args ?? Array.Empty<string>();
                if (args.Length > 0 && args[0].Equals("count", StringComparison.OrdinalIgnoreCase))
                    return HandleLootboxCountCommand(messageWrapper, lootboxComponent);

                return HandleOpenLootboxCommand(messageWrapper, lootboxComponent);
            }
            catch (Exception ex)
            {
                Logger.Error($"[LootBox] Error handling lootbox command: {ex}");
                return "RICS.LBCH.Error".Translate();
            }
        }

        private static string HandleLootboxCountCommand(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent)
        {
            string key = ResolveViewerKey(messageWrapper);
            int count = lootboxComponent.HowManyLootboxesDoesViewerHave(key);

            if (count == 1)
                return "RICS.LBCH.CountSingular".Translate(count);
            return "RICS.LBCH.CountPlural".Translate(count);
        }

        private static string HandleOpenLootboxCommand(ChatMessageWrapper messageWrapper, LootBoxComponent lootboxComponent)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
                return "RICS.LBCH.SettingsNotAvailable".Translate();

            if (settings.LootBoxForceOpenAllAtOnce)
                return OpenAllLootboxes(messageWrapper, lootboxComponent, settings);

            return OpenSingleLootbox(messageWrapper, lootboxComponent, settings);
        }

        private static string OpenSingleLootbox(
            ChatMessageWrapper messageWrapper,
            LootBoxComponent lootboxComponent,
            CAPGlobalChatSettings settings)
        {
            string key = ResolveViewerKey(messageWrapper);
            if (lootboxComponent.HowManyLootboxesDoesViewerHave(key) <= 0)
                return "RICS.LBCH.NoLootboxes".Translate();

            Viewer viewer = Viewers.GetViewer(messageWrapper);
            if (viewer == null)
                return "RICS.LBCH.SettingsNotAvailable".Translate();

            // Use the same dictionary key as HowManyLootboxesDoesViewerHave
            key = viewer.Username ?? key;
            int coins = Rand.Range(settings.LootBoxRandomCoinRange.min, settings.LootBoxRandomCoinRange.max);
            viewer.GiveCoins(coins);

            if (lootboxComponent.ViewersLootboxes.ContainsKey(key))
            {
                lootboxComponent.ViewersLootboxes[key]--;
                if (lootboxComponent.ViewersLootboxes[key] <= 0)
                    lootboxComponent.ViewersLootboxes.Remove(key);
            }

            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
            return "RICS.LBCH.Opened".Translate(coins, currencySymbol);
        }

        private static string OpenAllLootboxes(
            ChatMessageWrapper messageWrapper,
            LootBoxComponent lootboxComponent,
            CAPGlobalChatSettings settings)
        {
            string key = ResolveViewerKey(messageWrapper);
            int lootboxCount = lootboxComponent.HowManyLootboxesDoesViewerHave(key);
            if (lootboxCount <= 0)
                return "RICS.LBCH.NoLootboxes".Translate();

            Viewer viewer = Viewers.GetViewer(messageWrapper);
            if (viewer == null)
                return "RICS.LBCH.SettingsNotAvailable".Translate();

            key = viewer.Username ?? key;

            int totalCoins = 0;
            for (int i = 0; i < lootboxCount; i++)
                totalCoins += Rand.Range(settings.LootBoxRandomCoinRange.min, settings.LootBoxRandomCoinRange.max);

            viewer.GiveCoins(totalCoins);
            lootboxComponent.ViewersLootboxes[key] = 0;
            lootboxComponent.ViewersLootboxes.Remove(key);

            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
            return "RICS.LBCH.OpenedAll".Translate(totalCoins, currencySymbol);
        }

        /// <summary>Prefer viewer username used as lootbox dictionary key.</summary>
        private static string ResolveViewerKey(ChatMessageWrapper messageWrapper)
        {
            var viewer = Viewers.GetViewer(messageWrapper);
            if (viewer != null && !string.IsNullOrEmpty(viewer.Username))
                return viewer.Username;
            return messageWrapper?.Username ?? "";
        }
    }
}
