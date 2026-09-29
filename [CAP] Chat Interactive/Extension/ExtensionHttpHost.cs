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

                // Loopback-only listener. Mono's HttpListener often leaves QueryString
                // and custom headers empty — parse RawUrl as well.
                string devViewer = ReadDevViewer(ctx.Request);

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

        private static string ReadDevViewer(HttpListenerRequest req)
        {
            if (req == null)
                return null;

            string v = HeaderValue(req, "X-RICS-Dev-Viewer")
                ?? HeaderValue(req, "X-Rics-Dev-Viewer")
                ?? HeaderValue(req, "x-rics-dev-viewer");
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();

            try
            {
                v = req.QueryString?["viewer"];
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            catch { /* Mono QueryString can throw / be empty */ }

            v = ParseQueryParam(req.Url?.Query, "viewer");
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();

            string raw = req.RawUrl;
            if (!string.IsNullOrEmpty(raw))
            {
                int q = raw.IndexOf('?');
                if (q >= 0)
                {
                    v = ParseQueryParam(raw.Substring(q), "viewer");
                    if (!string.IsNullOrWhiteSpace(v))
                        return v.Trim();
                }
            }

            return null;
        }

        private static string HeaderValue(HttpListenerRequest req, string name)
        {
            try
            {
                string v = req.Headers?[name];
                return string.IsNullOrWhiteSpace(v) ? null : v;
            }
            catch
            {
                return null;
            }
        }

        private static string ParseQueryParam(string query, string key)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(key))
                return null;
            if (query[0] == '?')
                query = query.Substring(1);
            string[] parts = query.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part))
                    continue;
                int eq = part.IndexOf('=');
                string k = eq >= 0 ? part.Substring(0, eq) : part;
                string val = eq >= 0 ? part.Substring(eq + 1) : "";
                try
                {
                    k = Uri.UnescapeDataString(k);
                    val = Uri.UnescapeDataString(val.Replace('+', ' '));
                }
                catch { /* keep raw */ }
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return val;
            }
            return null;
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
