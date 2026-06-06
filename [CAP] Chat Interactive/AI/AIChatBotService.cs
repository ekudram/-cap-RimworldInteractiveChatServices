// AIChatBotService.cs
using Newtonsoft.Json;
using RimWorld;
using System;
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

                // === NEW: Food & Medicine Supply Summary ===
                int mealsCount = 0;
                int rawFoodCount = 0;
                int medicineCount = 0;

                try
                {
                    var resourceCounter = map.resourceCounter;

                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing == null || thing.Destroyed || !thing.Spawned) continue;

                        var def = thing.def;
                        if (def == null || def.ingestible == null) continue;

                        int stackCount = thing.stackCount > 0 ? thing.stackCount : 1;

                        var foodType = def.ingestible.foodType;

                        // Meals (prepared / processed food)
                        if ((foodType & FoodTypeFlags.Meal) != 0 ||
                            (foodType & FoodTypeFlags.Processed) != 0 ||
                            def.defName.Contains("Meal") || def.defName.Contains("Pie") || def.defName.Contains("Stew"))
                        {
                            mealsCount += stackCount;
                            continue; // meals take priority over raw
                        }

                        // Raw food - per your request: Meat, VegetableOrFruit, AnimalProduct (milk, jelly), etc.
                        // OmnivoreHuman etc. are combinations — we check base flags
                        if (def.IsNutritionGivingIngestible &&
                            ((foodType & FoodTypeFlags.Meat) != 0 ||
                             (foodType & FoodTypeFlags.VegetableOrFruit) != 0 ||
                             (foodType & FoodTypeFlags.AnimalProduct) != 0))
                        {
                            rawFoodCount += stackCount;
                            continue;
                        }

                        // Medicine
                        if (def.IsMedicine ||
                            (def.ingestible?.outcomeDoers?.Any(o => o is IngestionOutcomeDoer_GiveHediff) == true))
                        {
                            medicineCount += stackCount;
                        }
                    }

                    // Supplement with ResourceCounter for fast core counts (handles many vanilla + common mod defs)
                    if (resourceCounter != null)
                    {
                        mealsCount = Mathf.Max(mealsCount, resourceCounter.AllCountedAmounts
                            .Where(kvp => kvp.Key.ingestible != null &&
                                         ((kvp.Key.ingestible.foodType & FoodTypeFlags.Meal) != 0 ||
                                          (kvp.Key.ingestible.foodType & FoodTypeFlags.Processed) != 0))
                            .Sum(kvp => kvp.Value));

                        rawFoodCount = Mathf.Max(rawFoodCount, resourceCounter.AllCountedAmounts
                            .Where(kvp => kvp.Key.ingestible != null &&
                                         ((kvp.Key.ingestible.foodType & (FoodTypeFlags.Meat |
                                                                          FoodTypeFlags.VegetableOrFruit |
                                                                          FoodTypeFlags.AnimalProduct |
                                                                          FoodTypeFlags.Seed |
                                                                          FoodTypeFlags.Plant)) != 0))
                            .Sum(kvp => kvp.Value));
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
                    medicine = medicineCount,

                    foodSummary = $"Meals: {mealsCount} | Raw: {rawFoodCount} | Meds: {medicineCount}"
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