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
            if (Current.Game == null || Find.CurrentMap == null)
            {
                _cachedGameStateJson = "{\"status\":\"no_map\"}";
                return;
            }

            try
            {
                var map = Find.CurrentMap;
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
                    storyteller = Find.Storyteller?.def?.label ?? "Unknown"
                };

                // === Food & Medicine Supply Summary (improved medicine breakdown) ===
                int mealsCount = 0;
                int rawFoodCount = 0;
                var medicineBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int medicineTotal = 0;

                try
                {
                    var resourceCounter = map.resourceCounter;

                    // 1. Fast path via ResourceCounter (authoritative for tracked resources including Medicine category)
                    if (resourceCounter != null)
                    {
                        foreach (var kvp in resourceCounter.AllCountedAmounts)
                        {
                            var def = kvp.Key;
                            if (def == null || def.ingestible == null) continue;

                            int count = kvp.Value;
                            var foodType = def.ingestible.foodType;

                            // Meals (prepared/processed)
                            if ((foodType & FoodTypeFlags.Meal) != 0 ||
                                (foodType & FoodTypeFlags.Processed) != 0 ||
                                def.defName.IndexOf("Meal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                def.defName.IndexOf("Pie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                def.defName.IndexOf("Stew", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                mealsCount += count;
                                continue;
                            }

                            // Raw food
                            if (def.IsNutritionGivingIngestible &&
                                ((foodType & FoodTypeFlags.Meat) != 0 ||
                                 (foodType & FoodTypeFlags.VegetableOrFruit) != 0 ||
                                 (foodType & FoodTypeFlags.AnimalProduct) != 0 ||
                                 (foodType & FoodTypeFlags.Seed) != 0 ||
                                 (foodType & FoodTypeFlags.Plant) != 0))
                            {
                                rawFoodCount += count;
                                continue;
                            }
                        }

                        // Medicine via ResourceCounter (fast + complete for vanilla + most modded)
                        foreach (var kvp in resourceCounter.AllCountedAmounts.Where(k => k.Key.IsMedicine))
                        {
                            string key = kvp.Key.defName;
                            medicineBreakdown[key] = kvp.Value;
                            medicineTotal += kvp.Value;
                        }
                    }

                    // 2. Fallback/supplement pass over AllThings for any medicines ResourceCounter missed
                    //    (some heavily modded items may not be in the counted categories)
                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing == null || thing.Destroyed || !thing.Spawned) continue;

                        var def = thing.def;
                        if (def == null) continue;

                        int stackCount = thing.stackCount > 0 ? thing.stackCount : 1;

                        if (def.IsMedicine && !medicineBreakdown.ContainsKey(def.defName))
                        {
                            medicineBreakdown[def.defName] = stackCount;
                            medicineTotal += stackCount;
                        }
                    }
                }
                catch (Exception exFood)
                {
                    Logger.Warning($"[RICS AI] Food/Medicine cache partial failure: {exFood.Message}");
                }

                var fullState = new
                {
                    baseState.status,
                    baseState.day,
                    baseState.hour,
                    baseState.colonists,
                    baseState.threatPoints,
                    baseState.wealth,
                    baseState.season,
                    baseState.storyteller,

                    meals = mealsCount,
                    rawFood = rawFoodCount,

                    // Medicine info (kept flat total for backward compat + new breakdown for AI usefulness)
                    medicine = medicineTotal,
                    medicineBreakdown = medicineBreakdown,
                    medicineSummary = medicineBreakdown.Count == 0
                        ? "No medicine in stock"
                        : string.Join(" | ", medicineBreakdown.Select(kvp =>
                            $"{kvp.Key.Replace("Medicine", "")}: {kvp.Value}")),

                    foodSummary = $"Meals: {mealsCount} | Raw: {rawFoodCount} | Meds: {medicineTotal}"
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