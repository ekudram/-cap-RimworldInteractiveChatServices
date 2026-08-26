// ChatInteractiveAddonMenu.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive aka RICS (Rimworld Interactive Chat System).
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
// Built-in RICS MenuButton content (settings, editors, reconnect, economy tools).
using CAP_ChatInteractive.Interfaces;
using CAP_ChatInteractive.Windows;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class ChatInteractiveAddonMenu : IAddonMenu
    {
        public List<FloatMenuOption> MenuOptions()
        {
            return new List<FloatMenuOption>
            {
                SafeOption("Settings", () =>
                {
                    var mod = LoadedModManager.GetMod<CAPChatInteractiveMod>();
                    if (mod != null)
                        Find.WindowStack.Add(new Dialog_ModSettings(mod));
                }),

                SafeOption("Store Editor", () => Find.WindowStack.Add(new Dialog_StoreEditor())),
                SafeOption("Trait Editor", () => Find.WindowStack.Add(new Dialog_TraitsEditor())),
                SafeOption("Weather Editor", () => Find.WindowStack.Add(new Dialog_WeatherEditor())),
                SafeOption("Events Editor", () => Find.WindowStack.Add(new Dialog_EventsEditor())),
                SafeOption("Commands", () => Find.WindowStack.Add(new Dialog_CommandManager())),
                SafeOption("Viewers", () => Find.WindowStack.Add(new Dialog_ViewerManager())),
                SafeOption("Pawn Races", () => Find.WindowStack.Add(new Dialog_PawnRaceSettings())),
                SafeOption("Pawn Queue", () => Find.WindowStack.Add(new Dialog_PawnQueue())),
                SafeOption("Version History", () => Find.WindowStack.Add(new Dialog_RICS_VersionHistory())),
                SafeOption("Message Log", () => Find.WindowStack.Add(new Window_MessageLog())),

                SafeOption("Live Chat", () =>
                {
                    var existing = Find.WindowStack.Windows.OfType<Window_LiveChat>().FirstOrDefault();
                    if (existing != null)
                        existing.Close();
                    else
                        Find.WindowStack.Add(new Window_LiveChat());
                }),

                SafeOption("Connection Status", ShowConnectionStatus),
                SafeOption("Reconnect Services →", ShowReconnectMenu),
                SafeOption("Economy Tools →", ShowEconomyMenu),
                SafeOption("Events →", ShowEventsMenu),

                SafeOption("Help", () =>
                {
                    Application.OpenURL(
                        "https://github.com/ekudram/-cap-RimworldInteractiveChatServices/wiki");
                }),
            };
        }

        private static FloatMenuOption SafeOption(string label, Action action)
        {
            return AddonButtonActions.CreateFloatOption(label, action);
        }

        private void ShowConnectionStatus()
        {
            var mod = CAPChatInteractiveMod.Instance;
            if (mod == null)
            {
                Find.WindowStack.Add(new Dialog_MessageBox("RICS mod instance not available.", "Connection Status"));
                return;
            }

            string twitch = mod.TwitchService?.IsConnected == true ? "Connected" : "Disconnected";
            string youtube = mod.YouTubeService?.IsConnected == true ? "Connected" : "Disconnected";
            string kick = mod.KickService?.IsConnected == true ? "Connected" : "Disconnected";

            string message =
                $"Twitch: {twitch}\n" +
                $"YouTube: {youtube}\n" +
                $"Kick: {kick}";

            Find.WindowStack.Add(new Dialog_MessageBox(message, "Connection Status"));
        }

        private void ShowReconnectMenu()
        {
            var options = new List<FloatMenuOption>();
            var mod = CAPChatInteractiveMod.Instance;
            if (mod == null)
                return;

            options.Add(SafeOption("Reconnect Twitch", () =>
            {
                QueueReconnect("TwitchReconnect", () =>
                {
                    mod.TwitchService?.Disconnect();
                    mod.TwitchService?.Connect();
                    Messages.Message("Twitch reconnection initiated", MessageTypeDefOf.NeutralEvent);
                });
            }));

            options.Add(SafeOption("Reconnect YouTube", () =>
            {
                QueueReconnect("YouTubeReconnect", () =>
                {
                    mod.YouTubeService?.Disconnect();
                    mod.YouTubeService?.Connect();
                    Messages.Message("YouTube reconnection initiated", MessageTypeDefOf.NeutralEvent);
                });
            }));

            // KickService has Connect only (no Disconnect API) — Connect re-enters if already open
            options.Add(SafeOption("Reconnect Kick", () =>
            {
                QueueReconnect("KickReconnect", () =>
                {
                    mod.KickService?.Connect();
                    Messages.Message("Kick reconnection initiated", MessageTypeDefOf.NeutralEvent);
                });
            }));

            options.Add(SafeOption("Reconnect All Services", () =>
            {
                QueueReconnect("ReconnectAll", () =>
                {
                    mod.TwitchService?.Disconnect();
                    mod.YouTubeService?.Disconnect();

                    mod.TwitchService?.Connect();
                    mod.YouTubeService?.Connect();
                    mod.KickService?.Connect();

                    Messages.Message("All services reconnection initiated", MessageTypeDefOf.NeutralEvent);
                });
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void QueueReconnect(string key, Action work)
        {
            LongEventHandler.QueueLongEvent(
                () =>
                {
                    try
                    {
                        work();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[AddonMenu] {key} failed: {ex}");
                    }
                },
                key,
                false,
                null);
        }

        private void ShowEconomyMenu()
        {
            var options = new List<FloatMenuOption>
            {
                SafeOption("Award Coins to Active Viewers", () =>
                {
                    Viewers.AwardActiveViewersCoins();
                    Messages.Message("Coins awarded to active viewers", MessageTypeDefOf.NeutralEvent);
                }),
                SafeOption("Reset All Coins", () =>
                {
                    Viewers.ResetAllCoins();
                    Messages.Message("All viewer coins reset", MessageTypeDefOf.NeutralEvent);
                }),
                SafeOption("Reset All Karma", () =>
                {
                    Viewers.ResetAllKarma();
                    Messages.Message("All viewer karma reset", MessageTypeDefOf.NeutralEvent);
                }),
                SafeOption("Quality & Research Settings", () => CAPChatInteractiveMod.OpenQualitySettings()),
                new FloatMenuOption("--- Store Tools ---", null),
                SafeOption("Reset All Store Prices", () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Reset all store item prices to default values?",
                        () =>
                        {
                            foreach (var item in Store.StoreInventory.AllStoreItems.Values)
                            {
                                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                                if (thingDef != null)
                                    item.BasePrice = (int)(thingDef.BaseMarketValue * 1.67f);
                            }

                            Store.StoreInventory.SaveStoreToJson();
                            Messages.Message("All store prices reset to default", MessageTypeDefOf.PositiveEvent);
                        }));
                }),
                SafeOption("Enable All Store Items", () =>
                {
                    foreach (var item in Store.StoreInventory.AllStoreItems.Values)
                        item.Enabled = true;

                    Store.StoreInventory.SaveStoreToJson();
                    Messages.Message("All store items enabled", MessageTypeDefOf.PositiveEvent);
                }),
                SafeOption("View Store Statistics", () =>
                {
                    int enabledItems = Store.StoreInventory.GetEnabledItems().Count();
                    int total = Store.StoreInventory.AllStoreItems.Count;
                    int categories = Store.StoreInventory.AllStoreItems.Values
                        .Select(i => i.Category).Distinct().Count();

                    string message =
                        $"Total Items: {total}\n" +
                        $"Enabled: {enabledItems}\n" +
                        $"Disabled: {total - enabledItems}\n" +
                        $"Categories: {categories}";

                    Find.WindowStack.Add(new Dialog_MessageBox(message, "Store Statistics"));
                }),
                SafeOption("View Economy Statistics", () =>
                {
                    var activeViewers = Viewers.GetActiveViewers();
                    int totalCoins = 0;
                    float totalKarma = 0f;

                    foreach (var viewer in Viewers.All)
                    {
                        totalCoins += viewer.Coins;
                        totalKarma += viewer.Karma;
                    }

                    int viewerCount = Math.Max(1, Viewers.All.Count);
                    string message =
                        $"Active Viewers: {activeViewers.Count}\n" +
                        $"Total Viewers: {Viewers.All.Count}\n" +
                        $"Total Coins in Circulation: {totalCoins}\n" +
                        $"Average Karma: {totalKarma / viewerCount:F2}";

                    Find.WindowStack.Add(new Dialog_MessageBox(message, "Economy Statistics"));
                }),
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowEventsMenu()
        {
            var options = new List<FloatMenuOption>
            {
                SafeOption("Weather Editor", () => Find.WindowStack.Add(new Dialog_WeatherEditor())),
                SafeOption("Events Editor", () => Find.WindowStack.Add(new Dialog_EventsEditor())),
                SafeOption("Event Statistics", () =>
                {
                    int weatherCount = Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Count;
                    int enabledWeather = Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values
                        .Count(w => w.Enabled);

                    string message =
                        $"Weather Types: {weatherCount} total, {enabledWeather} enabled\n" +
                        $"Use the Events / Weather editors for full management.";

                    Find.WindowStack.Add(new Dialog_MessageBox(message, "Event Statistics"));
                }),
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
