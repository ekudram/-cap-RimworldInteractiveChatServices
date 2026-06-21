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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using static Mono.Security.X509.X520;

namespace CAP_ChatInteractive.AI
{
    public class AIChatBotService : IExposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // === Decoupled AI command system ===
        private readonly Queue<(string requestId, ChatMessageWrapper message)> _aiCommandQueue
            = new Queue<(string, ChatMessageWrapper)>();

        private readonly ConcurrentDictionary<string, string> _aiCommandResults
            = new ConcurrentDictionary<string, string>();

        private readonly object _aiCommandLock = new object();

        // === File-based AI Command System (main thread only - stable) ===
        // Uses RimWorld's official safe config folder to avoid permission issues
        private string _aiCommandBasePath;
        private string _aiCommandIncomingPath;
        private string _aiCommandOutgoingPath;
        private DateTime _lastFilePollTime = DateTime.MinValue;

        public bool HasPendingAICommands
        {
            get
            {
                lock (_aiCommandLock)
                {
                    return _aiCommandQueue.Count > 0;
                }
            }
        }

        private string _cachedGameStateJson = "{\"status\":\"no_game\"}";

        public bool IsRunning => _isRunning;

        public void ProcessPendingAICommands()
        {
            (string requestId, ChatMessageWrapper message) item;

            lock (_aiCommandLock)
            {
                if (_aiCommandQueue.Count == 0) return;
                item = _aiCommandQueue.Dequeue();
            }

            Logger.Debug($"[RICS AI] ProcessPendingAICommands START - RequestId: {item.requestId}");

            try
            {
                string result = ChatCommandProcessor.ProcessAICommand(item.message);
                _aiCommandResults[item.requestId] = result;

                Logger.Debug($"[RICS AI] ProcessPendingAICommands SUCCESS - RequestId: {item.requestId}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS AI] Error executing AI command: {ex.Message}");
                _aiCommandResults[item.requestId] = $"Error: {ex.Message}";
            }
        }

        public void Start()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive || _isRunning)
                return;

            try
            {
                // === SAFE PATH INITIALIZATION ===
                // Always create folders inside RimWorld's official config directory
                string baseConfigPath = Path.Combine(GenFilePaths.ConfigFolderPath, "CAP_ChatInteractive", "AI_Commands");
                _aiCommandBasePath = baseConfigPath; // store for reference if needed later
                _aiCommandIncomingPath = Path.Combine(baseConfigPath, "incoming");
                _aiCommandOutgoingPath = Path.Combine(baseConfigPath, "outgoing");

                // Create directories early so the Python bot never crashes
                Directory.CreateDirectory(_aiCommandIncomingPath);
                Directory.CreateDirectory(_aiCommandOutgoingPath);

                string endpoint = settings.AIChatBotEndpoint.TrimEnd('/') + "/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(endpoint);
                _listener.Start();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                Logger.Message($"[RICS AI] ✅ Listener started on {endpoint}");
                Logger.Debug($"[RICS AI] File-based commands using safe path: {baseConfigPath}");

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
                    HandleRequestAsync(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Debug($"[RICS AI] Listener loop error: {ex.Message}");
                }
            }
        }

        private void HandleRequestAsync(HttpListenerContext context)
        {
            string path = "unknown";

            try
            {
                path = context.Request.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');

                if (path == "/gamestate" || path == "/state")
                {
                    string json = GetCachedGameStateJson();
                    SendResponse(context, json, "application/json");
                }
                else
                {
                    // All command-related endpoints have been moved to file-based (main thread only)
                    string error = "{\"error\":\"This endpoint has been removed. Use file-based commands instead.\"}";
                    SendResponse(context, error, "application/json", 404);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS AI] Request handler error on path '{path}': {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }


        private void SendResponse(HttpListenerContext context, string text, string contentType, int statusCode = 200)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(text);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS AI] SendResponse failed: {ex.Message}");
            }
        }

        public void UpdateGameStateCache()
        {
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

                // === Grouped counts (same as before) ===
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
                int metalCount = 0;
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

                            if (def.ingestible != null)
                            {
                                var foodType = def.ingestible.foodType;
                                if ((foodType & FoodTypeFlags.Meal) != 0 || (foodType & FoodTypeFlags.Processed) != 0)
                                {
                                    mealsCount += count; continue;
                                }
                                if (def.IsNutritionGivingIngestible)
                                {
                                    if ((foodType & FoodTypeFlags.Meat) != 0) meatCount += count;
                                    else if ((foodType & FoodTypeFlags.VegetableOrFruit) != 0)
                                    {
                                        if (def.defName.IndexOf("Fruit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            def.defName.IndexOf("Berry", StringComparison.OrdinalIgnoreCase) >= 0)
                                            fruitCount += count;
                                        else vegetableCount += count;
                                    }
                                    else if ((foodType & FoodTypeFlags.AnimalProduct) != 0)
                                    {
                                        if (def.defName.IndexOf("Egg", StringComparison.OrdinalIgnoreCase) >= 0) eggCount += count;
                                        else otherRawFoodCount += count;
                                    }
                                }
                            }

                            if (def.defName == "WoodLog") woodCount += count;
                            else if (def.thingCategories?.Any(c => c.defName == "Textiles" || c.defName == "Fabric") == true) fabricCount += count;
                            else if (def.thingCategories?.Any(c => c.defName == "Leathers") == true) leatherCount += count;
                            else if (def.defName.StartsWith("Wool")) woolCount += count;
                            else if (def.defName == "Steel" || def.defName == "Plasteel") metalCount += count;
                            else if (def.defName.EndsWith("Block") || def.defName.Contains("Blocks")) stoneBlockCount += count;

                            if (def.IsMedicine)
                            {
                                string key = def.label?.CapitalizeFirst() ?? def.defName;
                                medicineBreakdown[key] = count;
                                medicineTotal += count;
                            }
                        }
                    }

                    // Fallback for modded medicine
                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                        if (thing.def != null && thing.def.IsMedicine)
                        {
                            string key = thing.def.label?.CapitalizeFirst() ?? thing.def.defName;
                            if (!medicineBreakdown.ContainsKey(key))
                            {
                                medicineBreakdown[key] = thing.stackCount;
                                medicineTotal += thing.stackCount;
                            }
                        }
                    }
                }
                catch { /* partial failure is ok */ }

                float mealsPerColonist = (float)mealsCount / colonistCount;
                float medsPerColonist = (float)medicineTotal / colonistCount;

                string foodStatus = mealsPerColonist > 18 ? "Abundant" : mealsPerColonist > 9 ? "Good" : "Low";
                string medicineStatus = medsPerColonist > 12 ? "Good" : medsPerColonist > 4 ? "Okay" : "Low";

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
                    food = new
                    {
                        meals = mealsCount,
                        rawFood = rawFoodCount,
                        status = foodStatus,
                        breakdown = new { Meat = meatCount, Vegetables = vegetableCount, Fruit = fruitCount, Eggs = eggCount, Other = otherRawFoodCount }
                    },
                    materials = new { wood = woodCount, fabric = fabricCount, leather = leatherCount, wool = woolCount, metals = metalCount, stoneBlocks = stoneBlockCount },
                    medicine = new { total = medicineTotal, status = medicineStatus, breakdown = medicineBreakdown },
                    colony = new
                    {
                        name = map.Parent?.Label ?? "Unknown Colony",
                        biome = map.Biome?.label ?? "Unknown",
                        outdoorTemp = map.mapTemperature?.OutdoorTemp ?? 0f
                    }
                };

                Logger.Debug("AI Cache Complete");

                _cachedGameStateJson = JsonConvert.SerializeObject(fullState);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Cache update failed: {ex.Message}");
                _cachedGameStateJson = "{\"status\":\"error\"}";
            }
        }

        /// <summary>
        /// Polled from GameComponentTick on the main thread.
        /// Processes any new command files dropped by the Python bot.
        /// Completely thread-safe. Uses safe RimWorld config folder paths.
        /// Self-initializes paths if needed (more robust).
        /// </summary>
        public void ProcessFileBasedAICommands()
        {
            // This debug may cause Spam REmove when done testing with positive Results -- Captolamia
            // Logger.Debug("=== Start ProcessFileBasedAICommands() ===");

            // === Self-initialize paths if they are missing (very important for robustness) ===
            if (string.IsNullOrEmpty(_aiCommandIncomingPath) || string.IsNullOrEmpty(_aiCommandOutgoingPath))
            {
                try
                {
                    string baseConfigPath = Path.Combine(GenFilePaths.ConfigFolderPath, "CAP_ChatInteractive", "AI_Commands");
                    _aiCommandBasePath = baseConfigPath;
                    _aiCommandIncomingPath = Path.Combine(baseConfigPath, "incoming");
                    _aiCommandOutgoingPath = Path.Combine(baseConfigPath, "outgoing");

                    Directory.CreateDirectory(_aiCommandIncomingPath);
                    Directory.CreateDirectory(_aiCommandOutgoingPath);

                    Logger.Debug($"[RICS AI] Lazy-initialized AI command paths: {baseConfigPath}");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[RICS AI] Failed to lazy-init command paths: {ex.Message}");
                    return;
                }
            }

            try
            {
                // Throttle to every ~500ms
                if ((DateTime.Now - _lastFilePollTime).TotalMilliseconds < 120)
                    return;

                _lastFilePollTime = DateTime.Now;

                if (!Directory.Exists(_aiCommandIncomingPath))
                    Directory.CreateDirectory(_aiCommandIncomingPath);

                var files = Directory.GetFiles(_aiCommandIncomingPath, "*.json");

                if (files.Length > 0)
                {
                    Logger.Debug($"[RICS AI] Found {files.Length} command file(s) in incoming folder");
                }

                foreach (var file in files)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();   // NEW timing
                    try
                    {
                        string json = File.ReadAllText(file);
                        var data = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                        string command = data?["command"]?.ToString() ?? "";

                        if (string.IsNullOrWhiteSpace(command))
                        {
                            File.Delete(file);
                            continue;
                        }

                        var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                        if (settings == null || !settings.AIChatBotActive || !settings.AIChatBotCanExecuteCommands)
                        {
                            File.Delete(file);
                            continue;
                        }

                        Logger.Debug($"[RICS AI] Processing file command: {command}");

                        var aiMessage = new ChatMessageWrapper(
                            username: settings.AIChatBotName ?? "Masie",
                            message: command,
                            platform: "AiChatBot",
                            platformUserId: "aichatbot-file", // I think this needs to be a number
                            platformMessage: null
                        );

                        string result = ChatCommandProcessor.ProcessAICommand(aiMessage);

                        string requestId = Path.GetFileNameWithoutExtension(file);
                        string resultPath = Path.Combine(_aiCommandOutgoingPath, requestId + ".json");

                        var resultObj = new { status = "ready", result = result };
                        File.WriteAllText(resultPath, JsonConvert.SerializeObject(resultObj));

                        File.Delete(file);

                        sw.Stop();
                        Logger.Debug($"[RICS AI] File command processed in {sw.ElapsedMilliseconds} ms: {command}");
                    }
                    catch (Exception exFile)
                    {
                        sw.Stop();
                        Logger.Warning($"[RICS AI] Failed to process command file {file} after {sw.ElapsedMilliseconds} ms: {exFile.Message}");
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] File poll error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called by Python bot to check for command result (via file).
        /// Returns the result JSON or {"status":"pending"} / {"status":"not_found"}.
        /// Uses safe RimWorld config folder paths.
        /// </summary>
        public string GetFileBasedAICommandResult(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return "{\"status\":\"error\",\"message\":\"Missing requestId\"}";

            // Safety guard
            if (string.IsNullOrEmpty(_aiCommandOutgoingPath) || string.IsNullOrEmpty(_aiCommandIncomingPath))
                return "{\"status\":\"error\",\"message\":\"AI command system not initialized\"}";

            string resultPath = Path.Combine(_aiCommandOutgoingPath, requestId + ".json");

            if (File.Exists(resultPath))
            {
                try
                {
                    string content = File.ReadAllText(resultPath);
                    File.Delete(resultPath); // one-time read
                    return content;
                }
                catch
                {
                    return "{\"status\":\"error\",\"message\":\"Failed to read result\"}";
                }
            }

            // Check if still waiting in incoming folder
            string incomingPath = Path.Combine(_aiCommandIncomingPath, requestId + ".json");
            if (File.Exists(incomingPath))
                return "{\"status\":\"pending\"}";

            return "{\"status\":\"not_found\"}";
        }

        public string GetCachedGameStateJson() => _cachedGameStateJson;

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }

        public void ExposeData() { }
    }
}