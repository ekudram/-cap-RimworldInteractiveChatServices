// ExtensionHttpHost.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge
// Option C: localhost-only HTTP for Local Test.

using CAP_ChatInteractive.Utilities;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CAP_ChatInteractive.Extension
{
    public sealed class ExtensionHttpHost : IDisposable
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _running;
        private int _port;

        public bool IsRunning => _running && _listener != null && _listener.IsListening;
        public int Port => _port;

        public void Start(int port)
        {
            Stop();
            _port = Math.Max(1024, Math.Min(port, 65535));

            try
            {
                // Bind loopback only — never 0.0.0.0 / +
                string prefix = $"http://127.0.0.1:{_port}/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                _cts = new CancellationTokenSource();
                _running = true;
                var token = _cts.Token;
                Task.Run(() => ListenLoop(token), token);
                Logger.Message($"[RICS Extension] LocalHttp listening on {prefix}extension/ping");
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Failed to start LocalHttp on port {_port}: {ex.Message}");
                _running = false;
                try { _listener?.Close(); } catch { }
                _listener = null;
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleContextAsync(ctx), token);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        Logger.Debug($"[RICS Extension] Listen loop: {ex.Message}");
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext ctx)
        {
            try
            {
                // CORS for browser Local Test / panel fetch
                AddCors(ctx.Response);

                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                string path = ctx.Request.Url?.AbsolutePath ?? "";
                string method = ctx.Request.HttpMethod ?? "GET";

                // Only serve /extension/*
                string norm = ExtensionRouter.NormalizePath(path);
                if (!path.ToLowerInvariant().Contains("/extension") && norm != "ping")
                {
                    // Allow /extension/ping style; reject unrelated
                    if (!path.ToLowerInvariant().StartsWith("/extension"))
                    {
                        await WriteAsync(ctx, 404, ExtensionEnvelope.Fail("NotFound", "Use /extension/… paths")).ConfigureAwait(false);
                        return;
                    }
                }

                string body = null;
                if (method == "POST" || method == "PUT")
                {
                    using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
                        body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                string devViewer = null;
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings != null && settings.TwitchExtensionAllowDevIdentity)
                {
                    devViewer = ctx.Request.Headers["X-RICS-Dev-Viewer"];
                    if (string.IsNullOrEmpty(devViewer))
                        devViewer = ctx.Request.QueryString["viewer"];
                }

                var job = new ExtensionJob
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    Method = method,
                    Path = path,
                    Body = body,
                    DevViewer = devViewer
                };

                string json = await ExtensionJobQueue.EnqueueAndWaitAsync(job).ConfigureAwait(false);
                await WriteAsync(ctx, 200, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    await WriteAsync(ctx, 500, ExtensionEnvelope.Fail("ServerError", ex.Message)).ConfigureAwait(false);
                }
                catch { /* ignore */ }
            }
        }

        private static void AddCors(HttpListenerResponse res)
        {
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-RICS-Dev-Viewer";
        }

        private static async Task WriteAsync(HttpListenerContext ctx, int status, string json)
        {
            byte[] buf = Encoding.UTF8.GetBytes(json ?? "{}");
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
            ctx.Response.Close();
        }

        public void Stop()
        {
            _running = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            _cts = null;
        }

        public void Dispose() => Stop();
    }
}
