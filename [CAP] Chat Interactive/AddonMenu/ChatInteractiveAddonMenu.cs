// File Name: ChatInteractiveAddonMenu.cs
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
// This class implements the addon menu for Chat Interactive, providing various options for managing settings, events, economy, and more.

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
        // Settings
        new FloatMenuOption("Settings", () =>
        {
            var mod = LoadedModManager.GetMod<CAPChatInteractiveMod>();
            Find.WindowStack.Add(new Dialog_ModSettings(mod));
        }),

        // Store Editor
        new FloatMenuOption("Store Editor", () =>
        {
            Find.WindowStack.Add(new Dialog_StoreEditor());
        }),

        // Trait Editor
        new FloatMenuOption("Trait Editor", () =>
        {
            Find.WindowStack.Add(new Dialog_TraitsEditor());
        }),

        // Weather Editor 
        new FloatMenuOption("Weather Editor", () =>
        {
            Find.WindowStack.Add(new Dialog_WeatherEditor());
        }),

        // Events Editor 
        new FloatMenuOption("Events Editor", () =>
        {
            Find.WindowStack.Add(new Dialog_EventsEditor());
        }),

        // Commands Management
        new FloatMenuOption("Commands", () =>
        {
            Find.WindowStack.Add(new Dialog_CommandManager());
        }),

        // Viewers Management
        new FloatMenuOption("Viewers", () =>
        {
            Find.WindowStack.Add(new Dialog_ViewerManager());
        }),

        // Pawn Race & Xenotype Settings
        new FloatMenuOption("Pawn Races", () =>
        {
            Find.WindowStack.Add(new Dialog_PawnRaceSettings());
        }),

        // NEW: Pawn Queue Management
        new FloatMenuOption("Pawn Queue", () =>
        {
            Find.WindowStack.Add(new Dialog_PawnQueue());
        }),

        new FloatMenuOption("Version History", () =>
        {
            Find.WindowStack.Add(new Dialog_RICS_VersionHistory());
        }),

        // Message Log
        new FloatMenuOption("Message Log", () =>
        {
            Find.WindowStack.Add(new Window_MessageLog());
        }),

        // Live Chat Monitor
        new FloatMenuOption("Live Chat", () =>
        {
            var existingWindow = Find.WindowStack.Windows.OfType<Window_LiveChat>().FirstOrDefault();
            if (existingWindow != null)
            {
                existingWindow.Close();
            }
            else
            {
                Find.WindowStack.Add(new Window_LiveChat());
            }
        }),

        // Connection Status
        new FloatMenuOption("Connection Status", () =>
        {
            ShowConnectionStatus();
        }),

        // Reconnect Services Submenu
        new FloatMenuOption("Reconnect Services →", () =>
        {
            ShowReconnectMenu();
        }),

        // Economy Tools
        new FloatMenuOption("Economy Tools →", () =>
        {
            ShowEconomyMenu();
        }),

        // Events Management Submenu
        new FloatMenuOption("Events →", () =>
        {
            ShowEventsMenu();
        }),

        // Help
        new FloatMenuOption("Help", () =>
        {
            Application.OpenURL("https://github.com/ekudram/-cap-RimworldInteractiveChatServices/wiki");
        })
    };
        }

        private void ShowConnectionStatus()
        {
            var mod = CAPChatInteractiveMod.Instance;
            var message = $"Twitch: {(mod.TwitchService?.IsConnected == true ? "✅ Connected" : "❌ Disconnected")}\n" +
                         $"YouTube: {(mod.YouTubeService?.IsConnected == true ? "✅ Connected" : "❌ Disconnected")}";

            Find.WindowStack.Add(new Dialog_MessageBox(message, "Connection Status"));
        }

        private void ShowReconnectMenu()
        {
            var options = new List<FloatMenuOption>();
            var mod = CAPChatInteractiveMod.Instance;

            // Twitch Reconnect
            options.Add(new FloatMenuOption("Reconnect Twitch", () =>
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    try
                    {
                        mod.TwitchService?.Disconnect();
                        System.Threading.Thread.Sleep(1000);
                        mod.TwitchService?.Connect();

                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            Messages.Message("Twitch reconnection initiated", MessageTypeDefOf.NeutralEvent);
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Twitch reconnect failed: {ex.Message}");
                    }
                }, "TwitchReconnect", false, null);
            }));

            // YouTube Reconnect
            options.Add(new FloatMenuOption("Reconnect YouTube", () =>
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    try
                    {
                        mod.YouTubeService?.Disconnect();
                        System.Threading.Thread.Sleep(1000);
                        mod.YouTubeService?.Connect();

                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            Messages.Message("YouTube reconnection initiated", MessageTypeDefOf.NeutralEvent);
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"YouTube reconnect failed: {ex.Message}");
                    }
                }, "YouTubeReconnect", false, null);
            }));

            // Reconnect All
            options.Add(new FloatMenuOption("Reconnect All Services", () =>
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    try
                    {
                        mod.TwitchService?.Disconnect();
                        mod.YouTubeService?.Disconnect();
                        System.Threading.Thread.Sleep(2000);

                        mod.TwitchService?.Connect();
                        mod.YouTubeService?.Connect();

                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            Messages.Message("All services reconnection initiated", MessageTypeDefOf.NeutralEvent);
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Reconnect all failed: {ex.Message}");
                    }
                }, "ReconnectAll", false, null);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // In ShowEconomyMenu method - add these options
        private void ShowEconomyMenu()
        {
            var options = new List<FloatMenuOption>();

            // Existing economy options...
            options.Add(new FloatMenuOption("Award Coins to Active Viewers", () =>
            {
                Viewers.AwardActiveViewersCoins();
                Messages.Message("Coins awarded to active viewers", MessageTypeDefOf.NeutralEvent);
            }));

            options.Add(new FloatMenuOption("Reset All Coins", () =>
            {
                Viewers.ResetAllCoins();
                Messages.Message("All viewer coins reset", MessageTypeDefOf.NeutralEvent);
            }));

            options.Add(new FloatMenuOption("Reset All Karma", () =>
            {
                Viewers.ResetAllKarma();
                Messages.Message("All viewer karma reset", MessageTypeDefOf.NeutralEvent);
            }));

            // NEW: Quality & Research Settings
            options.Add(new FloatMenuOption("Quality & Research Settings", () =>
            {
                CAPChatInteractiveMod.OpenQualitySettings();
            }));

            // Store Management Tools
            options.Add(new FloatMenuOption("--- Store Tools ---", null)); // Separator

            options.Add(new FloatMenuOption("Reset All Store Prices", () =>
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset all store item prices to default values?",
                    () => {
                        foreach (var item in Store.StoreInventory.AllStoreItems.Values)
                        {
                            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                            if (thingDef != null)
                            {
                                item.BasePrice = (int)(thingDef.BaseMarketValue * 1.67f);
                            }
                        }
                        Store.StoreInventory.SaveStoreToJson();
                        Messages.Message("All store prices reset to default", MessageTypeDefOf.PositiveEvent);
                    }
                ));
            }));

            options.Add(new FloatMenuOption("Enable All Store Items", () =>
            {
                foreach (var item in Store.StoreInventory.AllStoreItems.Values)
                {
                    item.Enabled = true;
                }
                Store.StoreInventory.SaveStoreToJson();
                Messages.Message("All store items enabled", MessageTypeDefOf.PositiveEvent);
            }));

            options.Add(new FloatMenuOption("View Store Statistics", () =>
            {
                var enabledItems = Store.StoreInventory.GetEnabledItems().Count();
                var disabledItems = Store.StoreInventory.AllStoreItems.Count - enabledItems;

                var message = $"Total Items: {Store.StoreInventory.AllStoreItems.Count}\n" +
                             $"Enabled: {enabledItems}\n" +
                             $"Disabled: {disabledItems}\n" +
                             $"Categories: {Store.StoreInventory.AllStoreItems.Values.Select(i => i.Category).Distinct().Count()}";

                Find.WindowStack.Add(new Dialog_MessageBox(message, "Store Statistics"));
            }));

            // Existing statistics option...
            options.Add(new FloatMenuOption("View Economy Statistics", () =>
            {
                var activeViewers = Viewers.GetActiveViewers();
                var totalCoins = 0;
                var totalKarma = 0;

                foreach (var viewer in Viewers.All)
                {
                    totalCoins += viewer.Coins;
                    totalKarma += viewer.Karma;
                }

                var message = $"Active Viewers: {activeViewers.Count}\n" +
                             $"Total Viewers: {Viewers.All.Count}\n" +
                             $"Total Coins in Circulation: {totalCoins}\n" +
                             $"Average Karma: {totalKarma / Math.Max(1, Viewers.All.Count)}";

                Find.WindowStack.Add(new Dialog_MessageBox(message, "Economy Statistics"));
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowEventsMenu()
        {
            var options = new List<FloatMenuOption>();

            // Weather Editor 
            options.Add(new FloatMenuOption("Weather Editor", () =>
            {
                Find.WindowStack.Add(new Dialog_WeatherEditor());
            }));

            // Events Editor 
            options.Add(new FloatMenuOption("Events Editor", () =>
            {
                Find.WindowStack.Add(new Dialog_EventsEditor());
            }));

            // Event Statistics
            options.Add(new FloatMenuOption("Event Statistics", () =>
            {
                var weatherCount = Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Count;
                var enabledWeather = Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values.Count(w => w.Enabled);

                var message = $"Weather Types: {weatherCount} total, {enabledWeather} enabled\n" +
                             $"Raids: System in development\n" +
                             $"Other Events: System in development";

                Find.WindowStack.Add(new Dialog_MessageBox(message, "Event Statistics"));
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}