// ExtensionPollClient.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge
// Option A stub: OutboundPoll — implemented later (R7). No-op for R1.

using CAP_ChatInteractive.Utilities;

namespace CAP_ChatInteractive.Extension
{
    /// <summary>
    /// Production transport: RICS polls free EBS for jobs and POSTs results.
    /// R1: stub only so TwitchExtensionTransport.OutboundPoll is selectable without crashing.
    /// </summary>
    public sealed class ExtensionPollClient
    {
        private bool _running;

        public bool IsRunning => _running;

        public void Start()
        {
            _running = true;
            Logger.Message("[RICS Extension] OutboundPoll selected — poll client is a STUB until Phase R7. Use LocalHttp for Local Test.");
        }

        public void Tick()
        {
            // Future: GET EBS poll URL with agent token; enqueue jobs; POST results.
        }

        public void Stop()
        {
            _running = false;
        }
    }
}
