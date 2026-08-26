// ExtensionService.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge

using CAP_ChatInteractive.Utilities;
using System;

namespace CAP_ChatInteractive.Extension
{
    /// <summary>
    /// Starts LocalHttp (C) and/or OutboundPoll (A) based on settings switch.
    /// </summary>
    public sealed class ExtensionService : IDisposable
    {
        private ExtensionHttpHost _http;
        private ExtensionPollClient _poll;
        private static ExtensionService _active;

        public static ExtensionService Active => _active;

        public bool IsLocalHttpRunning => _http != null && _http.IsRunning;
        public bool IsPollRunning => _poll != null && _poll.IsRunning;

        public void StartFromSettings()
        {
            Stop();

            var s = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (s == null || !s.TwitchExtensionEnabled)
            {
                Logger.Debug("[RICS Extension] Bridge disabled (TwitchExtensionEnabled=false).");
                return;
            }

            _active = this;

            var mode = (ExtensionTransportMode)s.TwitchExtensionTransport;
            if (mode == ExtensionTransportMode.LocalHttp)
            {
                _http = new ExtensionHttpHost();
                _http.Start(s.TwitchExtensionLocalPort);
            }
            else if (mode == ExtensionTransportMode.OutboundPoll)
            {
                _poll = new ExtensionPollClient();
                _poll.Start();
            }
        }

        /// <summary>Main-thread: drain job queue + poll tick.</summary>
        public void Tick()
        {
            ExtensionJobQueue.ProcessPending();
            _poll?.Tick();
        }

        public void Stop()
        {
            try { _http?.Stop(); } catch { }
            try { _poll?.Stop(); } catch { }
            _http = null;
            _poll = null;
            if (_active == this)
                _active = null;
        }

        public void Dispose() => Stop();

        public string StatusLine()
        {
            var s = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (s == null || !s.TwitchExtensionEnabled)
                return "Extension bridge: OFF";
            if ((ExtensionTransportMode)s.TwitchExtensionTransport == ExtensionTransportMode.LocalHttp)
            {
                return IsLocalHttpRunning
                    ? $"Extension LocalHttp: http://127.0.0.1:{s.TwitchExtensionLocalPort}/extension/ping"
                    : "Extension LocalHttp: failed to bind (see log)";
            }
            return IsPollRunning
                ? "Extension OutboundPoll: STUB (R7) — switch to LocalHttp for testing"
                : "Extension OutboundPoll: off";
        }
    }
}
