// AIChatBotService.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// 
// Provides a lightweight local HTTP listener so external AI bots (like your Python Masie Lamia)
// can receive game state and chat context, then return responses.
// Runs only when AIChatBotActive = true.

using RimWorld;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace CAP_ChatInteractive.AI
{
    public class AIChatBotService : IExposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Starts the local HTTP server on the configured endpoint (default: http://127.0.0.1:17888)
        /// </summary>
        public void Start()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive || _isRunning)
                return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(settings.AIChatBotEndpoint.TrimEnd('/') + "/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                Logger.Message($"[AI ChatBot] HTTP listener started on {settings.AIChatBotEndpoint}");

                // Fire and forget background listener
                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI ChatBot] Failed to start HTTP listener: {ex.Message}");
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
                    _ = HandleRequestAsync(context); // Fire and forget per request
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    if (_isRunning)
                        Logger.Debug($"[AI ChatBot] Listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');
                string responseText = "";
                string logMessage = "";

                if (path == "/gamestate" || path == "/state")
                {
                    responseText = GetGameStateJson();
                    logMessage = "Game state requested";
                }
                else if (path == "/chat")
                {
                    if (context.Request.HttpMethod == "POST")
                    {
                        // Read the response body from the Python bot
                        using (var reader = new System.IO.StreamReader(context.Request.InputStream, Encoding.UTF8))
                        {
                            string botResponse = await reader.ReadToEndAsync();
                            responseText = botResponse.Trim();

                            logMessage = $"Received response from AI bot: {responseText}";
                            Logger.Debug($"[AI ChatBot] {logMessage}");
                        }
                    }
                    else
                    {
                        // GET request - just info
                        responseText = "{\"status\":\"ok\",\"message\":\"POST to this endpoint to send AI response back to RimWorld\"}";
                        logMessage = "Chat endpoint info requested";
                    }
                }
                else
                {
                    responseText = "{\"error\":\"Unknown endpoint. Use /gamestate or POST to /chat\"}";
                    logMessage = $"Unknown endpoint requested: {path}";
                }

                if (!string.IsNullOrEmpty(logMessage))
                {
                    Logger.Debug($"[AI ChatBot] {logMessage}");
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseText);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[AI ChatBot] Request handling error: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }


        /// <summary>
        /// Public method for other parts of RICS (like AIChatBotCommand) to get current game state as JSON.
        /// </summary>
        public string GetGameStateJson()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                    return "{\"status\":\"error\",\"message\":\"No active map\"}";

                float threatPoints = StorytellerUtility.DefaultThreatPointsNow(map);

                int colonistCount = map.mapPawns?.FreeColonists?.Count ?? 0;
                int totalPawns = map.mapPawns?.AllPawns?.Count ?? 0;

                string storytellerName = Find.Storyteller?.def?.label ?? "Unknown";

                int ticksGame = Find.TickManager.TicksGame;

                int currentDay = GenDate.DaysPassed;
                int currentHour = GenDate.HourOfDay(ticksGame, Find.WorldGrid.LongLatOf(map.Tile).x);

                string currentSeason = GenDate.Season(ticksGame, Find.WorldGrid.LongLatOf(map.Tile)).ToString();

                var json = new System.Text.StringBuilder();
                json.Append("{");
                json.Append($"\"status\":\"ok\",");
                json.Append($"\"day\":{currentDay},");
                json.Append($"\"hour\":{currentHour},");
                json.Append($"\"storyteller\":\"{storytellerName}\",");
                json.Append($"\"threatPoints\":{threatPoints:F0},");
                json.Append($"\"colonists\":{colonistCount},");
                json.Append($"\"totalPawns\":{totalPawns},");
                json.Append($"\"wealth\":{(int)(map.wealthWatcher?.WealthTotal ?? 0)},");
                json.Append($"\"mapName\":\"{map.Parent?.Label ?? "Unknown"}\",");
                json.Append($"\"season\":\"{currentSeason}\"");
                json.Append("}");

                return json.ToString();
            }
            catch (Exception ex)
            {
                Logger.Debug($"[AI ChatBot] GetGameStateJson failed: {ex.Message}");
                return "{\"status\":\"error\",\"message\":\"Failed to generate game state\"}";
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            Logger.Debug("[AI ChatBot] HTTP listener stopped");
        }

        public void ExposeData() { } // Not persisted for now
    }
}