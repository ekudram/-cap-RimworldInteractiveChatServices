// AIChatBotService.cs
using RimWorld;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using Newtonsoft.Json;

namespace CAP_ChatInteractive.AI
{
    public class AIChatBotService : IExposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public void Start()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive || _isRunning)
                return;

            try
            {
                // RICS must listen on AIChatBotEndpoint (17888), NOT the bot's port
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
                Logger.Error($"[RICS AI] Failed to start listener on {settings?.AIChatBotEndpoint}: {ex.Message}");
                Logger.Error("   → Make sure no other program (including the Python bot) is using this port.");
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
                    string json = GetGameStateJson();
                    await SendResponseAsync(context, json, "application/json");
                    Logger.Debug("[RICS AI] Served game state to Python bot");
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

        public string GetGameStateJson()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return "{\"status\":\"no_map\"}";

                var state = new
                {
                    status = "ok",
                    day = GenDate.DaysPassed,
                    hour = GenDate.HourOfDay(Find.TickManager.TicksGame, Find.WorldGrid.LongLatOf(map.Tile).x),
                    colonists = map.mapPawns?.FreeColonists?.Count ?? 0,
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(map),
                    wealth = (int)(map.wealthWatcher?.WealthTotal ?? 0),
                    season = GenDate.Season(Find.TickManager.TicksGame, Find.WorldGrid.LongLatOf(map.Tile)).ToString(),
                    storyteller = Find.Storyteller?.def?.label ?? "Unknown"
                };

                return JsonConvert.SerializeObject(state);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Game state generation failed: {ex.Message}");
                return "{\"status\":\"error\"}";
            }
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