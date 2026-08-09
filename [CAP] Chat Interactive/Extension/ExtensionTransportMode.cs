// ExtensionTransportMode.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge

namespace CAP_ChatInteractive.Extension
{
    /// <summary>
    /// C = LocalHttp (localhost panel testing). A = OutboundPoll (production EBS). Switchable without rewriting builders.
    /// </summary>
    public enum ExtensionTransportMode
    {
        LocalHttp = 0,
        OutboundPoll = 1
    }
}
