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
            // or has multiple maps open.
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

                int colonistCount = map.mapPawns?.FreeColonistsSpawnedCount ?? 1;

                // === Grouped counts for AI bot ===
                int mealsCount = 0;
                int rawFoodCount = 0;

                int meatCount = 0;
                int vegetableCount = 0;
                int fruitCount = 0;
                int eggCount = 0;
                int otherRawFoodCount = 0;

                var medicineBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int medicineTotal = 0;

                int woodCount = 0;
                int fabricCount = 0;
                int leatherCount = 0;
                int woolCount = 0;
                int metalCount = 0;          // steel + plasteel
                int stoneBlockCount = 0;

                try
                {
                    var resourceCounter = map.resourceCounter;

                    if (resourceCounter != null)
                    {
                        foreach (var kvp in resourceCounter.AllCountedAmounts)
                        {
                            var def = kvp.Key;
                            if (def == null) continue;

                            int count = kvp.Value;

                            // === Meals ===
                            if (def.ingestible != null)
                            {
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

                                // === Raw Food Classification ===
                                if (def.IsNutritionGivingIngestible)
                                {
                                    if ((foodType & FoodTypeFlags.Meat) != 0)
                                    {
                                        meatCount += count;
                                    }
                                    else if ((foodType & FoodTypeFlags.VegetableOrFruit) != 0)
                                    {
                                        if (def.defName.IndexOf("Fruit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            def.defName.IndexOf("Berry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            def.defName.IndexOf("Agave", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            fruitCount += count;
                                        }
                                        else
                                        {
                                            vegetableCount += count;
                                        }
                                    }
                                    else if ((foodType & FoodTypeFlags.AnimalProduct) != 0)
                                    {
                                        if (def.defName.IndexOf("Egg", StringComparison.OrdinalIgnoreCase) >= 0)
                                            eggCount += count;
                                        else
                                            otherRawFoodCount += count; // milk, insect jelly, etc.
                                    }
                                }
                            }

                            // === Materials (grouped, excludes chunks/corpses/waste) ===
                            if (def.defName == "WoodLog")
                                woodCount += count;
                            else if (def.thingCategories?.Any(c => c.defName == "Textiles" || c.defName == "Fabric") == true)
                                fabricCount += count;
                            else if (def.thingCategories?.Any(c => c.defName == "Leathers") == true)
                                leatherCount += count;
                            else if (def.defName.StartsWith("Wool"))
                                woolCount += count;
                            else if (def.defName == "Steel" || def.defName == "Plasteel")
                                metalCount += count;
                            else if (def.defName.EndsWith("Block") || def.defName.Contains("Blocks"))
                                stoneBlockCount += count;

                            // === Medicine (human readable labels) ===
                            if (def.IsMedicine)
                            {
                                string key = def.label?.CapitalizeFirst() ?? def.defName;
                                medicineBreakdown[key] = count;
                                medicineTotal += count;
                            }
                        }
                    }

                    // Fallback for modded medicines not in resourceCounter
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

                // === Per-colonist status (better for small and large colonies) ===
                float mealsPerColonist = (float)mealsCount / colonistCount;
                float medsPerColonist = (float)medicineTotal / colonistCount;

                string foodStatus = mealsPerColonist > 18 ? "Abundant"
                                   : mealsPerColonist > 9 ? "Good"
                                   : "Low";

                string medicineStatus = medsPerColonist > 12 ? "Good"
                                       : medsPerColonist > 4 ? "Okay"
                                       : "Low";

                var fullState = new
                {
                    status = "ok",
                    day = GenDate.DaysPassed,
                    hour = GenDate.HourOfDay(tickManager.TicksGame, worldGrid.LongLatOf(tile).x),
                    colonists = colonistCount,
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    wealth = (int)(map.wealthWatcher?.WealthTotal ?? 0),
                    season = GenDate.Season(tickManager.TicksGame, worldGrid.LongLatOf(tile)).ToString(),
                    storyteller = Find.Storyteller?.def?.label ?? "Unknown",

                    // === Grouped food (small LLM friendly) ===
                    food = new
                    {
                        meals = mealsCount,
                        rawFood = rawFoodCount,
                        status = foodStatus,
                        breakdown = new
                        {
                            Meat = meatCount,
                            Vegetables = vegetableCount,
                            Fruit = fruitCount,
                            Eggs = eggCount,
                            Other = otherRawFoodCount
                        }
                    },

                    // === Grouped materials ===
                    materials = new
                    {
                        wood = woodCount,
                        fabric = fabricCount,
                        leather = leatherCount,
                        wool = woolCount,
                        metals = metalCount,
                        stoneBlocks = stoneBlockCount
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