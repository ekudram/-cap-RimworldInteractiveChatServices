// AIChatBotService.cs

// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive. aka Rimworld Interactive Chat System (RICS)
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
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.AI
{
    public class AIChatBotService : IExposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // Cache updated from main thread
        private string _cachedGameStateJson = "{\"status\":\"no_game\"}";

        public bool IsRunning => _isRunning;

        public void Start()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive || _isRunning)
                return;

            try
            {
                string endpoint = settings.AIChatBotEndpoint.TrimEnd('/') + "/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(endpoint);
                _listener.Start();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                Logger.Message($"[RICS AI] ✅ Listener successfully started on {endpoint} (Python bot should GET /gamestate here)");

                // Immediately populate the cache so the first GET /gamestate from the Python bot has real data
                // instead of the default "no_game" / "no_map" placeholder.
                UpdateGameStateCache();

                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS AI] Failed to start listener: {ex.Message}");
                _isRunning = false;
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Debug($"[RICS AI] Listener loop error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');

                if (path == "/gamestate" || path == "/state")
                {
                    string json = GetCachedGameStateJson();
                    await SendResponseAsync(context, json, "application/json");
                    Logger.Debug("[RICS AI] Served cached game state to Python bot");
                }
                else
                {
                    string error = "{\"error\":\"Unknown endpoint. Use /gamestate\"}";
                    await SendResponseAsync(context, error, "application/json", 404);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS AI] Request handler error: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }

        private async Task SendResponseAsync(HttpListenerContext context, string text, string contentType, int statusCode = 200)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }


        public void UpdateGameStateCache()
        {
            // Prefer a player home map (the actual colony) over Find.CurrentMap.
            // This fixes cases where the player is flying in a grav ship, viewing a temporary map,
            // or has multiple maps open. Find.AnyPlayerHomeMap is the idiomatic RimWorld way
            // (confirmed from decompiled Find.cs + Map.IsPlayerHome).
            Map map = Current.Game?.Maps?.FirstOrDefault(m => m.IsPlayerHome && !m.Disposed)
                   ?? Find.AnyPlayerHomeMap
                   ?? Find.CurrentMap;

            if (Current.Game == null || map == null)
            {
                _cachedGameStateJson = "{\"status\":\"no_map\"}";
                return;
            }

            try
            {
                
                var tickManager = Find.TickManager;
                var worldGrid = Find.WorldGrid;
                var tile = map.Tile;

                // Core state (unchanged)
                var baseState = new
                {
                    status = "ok",
                    day = GenDate.DaysPassed,
                    hour = GenDate.HourOfDay(tickManager.TicksGame, worldGrid.LongLatOf(tile).x),
                    colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0,
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    wealth = (int)(map.wealthWatcher?.WealthTotal ?? 0),
                    season = GenDate.Season(tickManager.TicksGame, worldGrid.LongLatOf(tile)).ToString(),
                    storyteller = Find.Storyteller?.def?.label ?? "Unknown",

                    // === New lightweight context for the Python bot ===
                    isPlayerHome = map.IsPlayerHome,
                    biome = map.Biome?.label ?? "Unknown",
                    biomeDefName = map.Biome?.defName ?? "Unknown",
                    worldTile = (int)map.Tile,

                    // Colony identity
                    colonyName = map.Parent?.Label ?? "Unknown Colony",
                    factionName = Faction.OfPlayer?.Name ?? Faction.OfPlayer?.def?.label ?? "Player",

                    // Current outdoor temperature (useful for weather/roleplay comments)
                    outdoorTemp = map.mapTemperature?.OutdoorTemp ?? 0f
                };

                // === Food & Medicine Supply (improved structure for AI) ===
                int mealsCount = 0;
                int rawFoodCount = 0;
                var medicineBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int medicineTotal = 0;

                try
                {
                    var resourceCounter = map.resourceCounter;

                    if (resourceCounter != null)
                    {
                        foreach (var kvp in resourceCounter.AllCountedAmounts)
                        {
                            var def = kvp.Key;
                            if (def == null || def.ingestible == null) continue;

                            int count = kvp.Value;
                            var foodType = def.ingestible.foodType;

                            if ((foodType & FoodTypeFlags.Meal) != 0 ||
                                (foodType & FoodTypeFlags.Processed) != 0 ||
                                def.defName.IndexOf("Meal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                def.defName.IndexOf("Pie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                def.defName.IndexOf("Stew", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                mealsCount += count;
                                continue;
                            }

                            if (def.IsNutritionGivingIngestible &&
                                ((foodType & FoodTypeFlags.Meat) != 0 ||
                                 (foodType & FoodTypeFlags.VegetableOrFruit) != 0 ||
                                 (foodType & FoodTypeFlags.AnimalProduct) != 0))
                            {
                                rawFoodCount += count;
                                continue;
                            }
                        }

                        // Medicine using human-readable labels (much better for the LLM)
                        foreach (var kvp in resourceCounter.AllCountedAmounts.Where(k => k.Key.IsMedicine))
                        {
                            string key = kvp.Key.label?.CapitalizeFirst() ?? kvp.Key.defName;
                            medicineBreakdown[key] = kvp.Value;
                            medicineTotal += kvp.Value;
                        }
                    }

                    // Fallback for any missed modded medicines
                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                        var def = thing.def;
                        if (def == null || !def.IsMedicine) continue;

                        int stackCount = thing.stackCount > 0 ? thing.stackCount : 1;
                        string key = def.label?.CapitalizeFirst() ?? def.defName;

                 
                        
                        if (!medicineBreakdown.ContainsKey(key))
                        {
                            medicineBreakdown[key] = stackCount;
                            medicineTotal += stackCount;
                        }
                    }
                }
                catch (Exception exFood)
                {
                    Logger.Warning($"[RICS AI] Food/Medicine cache partial failure: {exFood.Message}");
                }

                // Simple status helpers (very helpful for small models)
                string foodStatus = mealsCount > 500 ? "Abundant" : mealsCount > 100 ? "Good" : "Low";
                string medicineStatus = medicineTotal > 300 ? "Good" : medicineTotal > 50 ? "Okay" : "Low";

                var fullState = new
                {
                    status = "ok",
                    day = GenDate.DaysPassed,
                    hour = GenDate.HourOfDay(tickManager.TicksGame, worldGrid.LongLatOf(tile).x),
                    colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0,
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    wealth = (int)(map.wealthWatcher?.WealthTotal ?? 0),
                    season = GenDate.Season(tickManager.TicksGame, worldGrid.LongLatOf(tile)).ToString(),
                    storyteller = Find.Storyteller?.def?.label ?? "Unknown",

                    // === Improved nested structure for the AI bot ===
                    food = new
                    {
                        meals = mealsCount,
                        rawFood = rawFoodCount,
                        status = foodStatus
                    },

                    medicine = new
                    {
                        total = medicineTotal,
                        status = medicineStatus,
                        breakdown = medicineBreakdown
                    },

                    colony = new
                    {
                        name = map.Parent?.Label ?? "Unknown Colony",
                        biome = map.Biome?.label ?? "Unknown",
                        outdoorTemp = map.mapTemperature?.OutdoorTemp ?? 0f
                    }
                };

                _cachedGameStateJson = JsonConvert.SerializeObject(fullState);

            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Cache update failed: {ex.Message}");
                _cachedGameStateJson = "{\"status\":\"error\"}";
            }
        }

        public string GetCachedGameStateJson()
        {
            return _cachedGameStateJson;
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            Logger.Debug("[RICS AI] Listener stopped");
        }

        public void ExposeData() { /* No persistent state needed */ }
    }
}